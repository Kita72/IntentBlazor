using System.Text.Json;
using IntentBlazor.Verification;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace IntentBlazor.HostSmoke;

// Shared by the offline published-bundle test and the explicitly invoked public-host recorder.
public static class DocsHostSmoke
{
    public static async Task<HostObservation> CaptureAsync(IPage page, string origin, EvidenceStamp candidate)
    {
        Uri address = new(origin, UriKind.Absolute);
        if ((!address.IsLoopback && address.AbsoluteUri != "https://intentblazor.runasp.net/") ||
            address.UserInfo.Length != 0 || address.Query.Length != 0 || address.Fragment.Length != 0 || address.AbsolutePath != "/")
            throw new ArgumentException("Use the expected public origin or an owned loopback fixture.");
        string healthUrl = new Uri(address, "health/ready").AbsoluteUri;
        IAPIResponse response = await page.APIRequest.GetAsync(healthUrl, new() { Timeout = 15000, MaxRedirects = 0 });
        using JsonDocument health = JsonDocument.Parse(await response.BodyAsync()); JsonElement data = health.RootElement;
        string Value(string name) => data.GetProperty(name).GetString() ?? throw new InvalidDataException("Incomplete health response.");
        if (response.Url != healthUrl || response.Status != 200 || Value("status") != "ready" || Value("version") != candidate.Version ||
            Value("sourceRevision") != candidate.SourceRevision || Value("sourceFingerprint") != candidate.SourceFingerprint ||
            Value("mode") != "fixture" || Value("hosting") != "InteractiveServer" || Value("architecture") != "X64" || !Value("runtime").StartsWith(".NET 10.", StringComparison.Ordinal))
            throw new InvalidDataException("The host identity, runtime or mode differs from the intended IIS candidate.");
        await page.GotoAsync(new Uri(address, "explorer?fixture=staff").AbsoluteUri);
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true })).ToBeEnabledAsync();
        ILocator circuit = page.Locator(".explorer-controls");
        await Expect(circuit).ToHaveAttributeAsync("data-host-interactive", "true");
        await Expect(circuit).ToHaveAttributeAsync("data-host-source-revision", candidate.SourceRevision);
        await Expect(circuit).ToHaveAttributeAsync("data-host-source-fingerprint", candidate.SourceFingerprint);
        ILocator row = page.Locator("[data-node='jobs'] [data-row-key='job-101'] button");
        await row.ClickAsync(); await Expect(row).ToHaveAttributeAsync("aria-pressed", "true");
        ILocator notes = page.GetByLabel("Follow-up notes", new() { Exact = true });
        string? beforeEdit = await page.Locator(".ib-output").GetAttributeAsync("data-revision");
        await notes.FillAsync("Offline-compatible host smoke draft"); await notes.PressAsync("Tab");
        await Expect(page.Locator(".ib-output")).Not.ToHaveAttributeAsync("data-revision", beforeEdit!);
        string? beforeRequest = await page.Locator(".ib-output").GetAttributeAsync("data-revision");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Request", Exact = true }).FillAsync("staff");
        await page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true }).ClickAsync();
        await Expect(page.Locator(".ib-output")).Not.ToHaveAttributeAsync("data-revision", beforeRequest!);
        await Expect(notes).ToHaveValueAsync("Offline-compatible host smoke draft");
        await Expect(row).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(page.Locator(".ib-errors")).ToHaveCountAsync(0);
        string circuitRevision = await circuit.GetAttributeAsync("data-host-source-revision") ?? throw new InvalidDataException("Missing circuit source identity.");
        return new(candidate with { CheckedAt = DateTimeOffset.UtcNow }, address.AbsoluteUri, response.Status, Value("version"), Value("sourceRevision"),
            Value("sourceFingerprint"), Value("mode"), Value("hosting"), Value("architecture"), Value("runtime"), circuitRevision, DateTimeOffset.UtcNow, true, true, true);
    }
}
