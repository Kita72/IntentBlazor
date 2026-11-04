using System.Text.RegularExpressions;
using SkiaSharp;

namespace IntentBlazor.BrowserTests;

public sealed class ApplicationBrowserTests(BrowserHarness browser) : IClassFixture<BrowserHarness>
{
    [Fact]
    public Task News_UsesSourceAttributionSavesLibraryAndKeepsEachIdentityPrivate() =>
        browser.RunAsync(nameof(News_UsesSourceAttributionSavesLibraryAndKeepsEachIdentityPrivate), "news", async test =>
        {
            IPage page = test.Page; await test.SignInAsync(page);
            var rows = page.Locator("[data-node='articles'] tbody tr");
            Assert.True(await rows.CountAsync() >= 2);
            string first = (await rows.First.GetAttributeAsync("data-row-key"))!;
            string second = (await rows.Nth(1).GetAttributeAsync("data-row-key"))!;
            await rows.First.GetByRole(AriaRole.Button).ClickAsync();
            await Expect(page.Locator("[data-node='primaryTitle']")).ToBeVisibleAsync();
            string? selectedRevision = await page.Locator(".ib-output").GetAttributeAsync("data-revision");
            await page.Locator("[data-node='comparisonPick'] select").SelectOptionAsync(second);
            await Expect(page.Locator(".ib-output")).Not.ToHaveAttributeAsync("data-revision", selectedRevision!);
            await Expect(page.Locator("[aria-label='Article sources'] a").First).ToHaveAttributeAsync("href", new Regex("^https://example.com/"));
            await page.Locator("[data-node='bookmark']").ClickAsync();
            await Expect(page.Locator("[data-node='bookmarks'] tbody tr")).ToHaveCountAsync(1);
            await page.Locator("[data-node='saveComparison']").ClickAsync();
            await Expect(page.Locator("[data-node='saved'] li")).ToHaveCountAsync(1);
            await BrowserCase.SubmitAsync(page, "Compare these updates and retain my selected versions");
            await Expect(page.Locator("[data-node='comparisonPick'] select")).ToHaveValueAsync(second);
            await Expect(page.Locator($"[data-node='articles'] [data-row-key='{first}'] button")).ToHaveAttributeAsync("aria-pressed", "true");
            await page.GetByRole(AriaRole.Button, new() { Name = "Save workspace", Exact = true }).ClickAsync();
            await Expect(page.GetByText("Workspace checkpoint saved; business actions are separate.", new() { Exact = true })).ToBeVisibleAsync();
            await page.ReloadAsync();
            await Expect(page.Locator("[data-node='bookmarks'] tbody tr")).ToHaveCountAsync(1);
            await Expect(page.Locator("[data-node='saved'] li")).ToHaveCountAsync(1);
            await Expect(page.Locator("[data-node='comparisonPick'] select")).ToHaveValueAsync(second);
            var peer = await test.NewContextAsync(); var other = await peer.NewPageAsync(); await test.SignInAsync(other);
            await Expect(other.Locator("[data-node='bookmarks'] tbody tr")).ToHaveCountAsync(0);
            await Expect(other.Locator("[data-node='saved'] li")).ToHaveCountAsync(0);
            await Expect(page.Locator("[data-node='bookmarks'] tbody tr")).ToHaveCountAsync(1);
        });

    [Fact]
    public Task Staff_ConflictsRetainDraftsReviewedSaveSucceedsAndIdentityChangesCannotReplayOrLeakEdits() =>
        browser.RunAsync(nameof(Staff_ConflictsRetainDraftsReviewedSaveSucceedsAndIdentityChangesCannotReplayOrLeakEdits), "staff", async test =>
        {
            IPage alex = test.Page; await test.SignInAsync(alex, "alex");
            var first = alex.Locator("[data-node='jobs'] tbody tr").First;
            string key = (await first.GetAttributeAsync("data-row-key"))!;
            await first.GetByRole(AriaRole.Button).ClickAsync();
            await EditNotesAsync(alex, "Alex retained conflict draft");
            var peer = await test.NewContextAsync(); IPage sam = await peer.NewPageAsync(); await test.SignInAsync(sam, "sam");
            await sam.Locator($"[data-node='jobs'] [data-row-key='{key}'] button").ClickAsync();
            await EditNotesAsync(sam, "Sam committed first"); await sam.Locator("[data-node='save']").ClickAsync();
            await Expect(sam.Locator(".ib-output")).ToContainTextAsync("Succeeded:");
            await Expect(sam.Locator("[data-node='history'] tbody tr")).ToHaveCountAsync(1);
            await alex.Locator("[data-node='save']").ClickAsync();
            await Expect(alex.Locator(".ib-output")).ToContainTextAsync("Conflict:");
            await Expect(alex.GetByLabel("My follow-up notes", new() { Exact = true })).ToHaveValueAsync("Alex retained conflict draft");
            await BrowserCase.SubmitAsync(alex, "Refresh the latest shared assignments");
            await Expect(alex.Locator("[data-node='currentNotes']")).ToContainTextAsync("Sam committed first");
            await Expect(alex.GetByLabel("My follow-up notes", new() { Exact = true })).ToHaveValueAsync("Alex retained conflict draft");
            await alex.Locator("[data-node='rebase']").ClickAsync(); await alex.Locator("[data-node='save']").ClickAsync();
            await Expect(alex.Locator(".ib-output")).ToContainTextAsync("Succeeded:");
            await Expect(alex.Locator("[data-node='history'] tbody tr")).ToHaveCountAsync(2);
            await Expect(alex.Locator("[data-node='currentNotes']")).ToContainTextAsync("Alex retained conflict draft");
            await EditNotesAsync(alex, "Alex private scratch must not leak");
            await alex.GetByRole(AriaRole.Button, new() { Name = "Save workspace", Exact = true }).ClickAsync();
            await Expect(alex.GetByText("Workspace checkpoint saved; business actions are separate.", new() { Exact = true })).ToBeVisibleAsync();
            await alex.GetByRole(AriaRole.Button, new() { Name = "Switch identity", Exact = true }).ClickAsync();
            await test.SignInAsync(alex, "riley");
            await Expect(alex.Locator("body")).Not.ToContainTextAsync("Alex private scratch must not leak");
            await alex.Locator($"[data-node='jobs'] [data-row-key='{key}'] button").ClickAsync();
            await EditNotesAsync(alex, "Reader local draft"); await alex.Locator("[data-node='save']").ClickAsync();
            await Expect(alex.Locator(".ib-output")).ToContainTextAsync("Rejected: The application denied this operation.");
            await Expect(alex.GetByLabel("My follow-up notes", new() { Exact = true })).ToHaveValueAsync("Reader local draft");
            await Expect(alex.Locator("[data-node='history'] tbody tr")).ToHaveCountAsync(2);
            await alex.GetByRole(AriaRole.Button, new() { Name = "Switch identity", Exact = true }).ClickAsync();
            await test.SignInAsync(alex, "alex");
            await Expect(alex.GetByLabel("My follow-up notes", new() { Exact = true })).ToHaveValueAsync("Alex private scratch must not leak");
            await Expect(alex.Locator("[data-node='history'] tbody tr")).ToHaveCountAsync(2);
        });

    [Fact]
    public Task Staff_LostActionReplyReconnectsWithoutRepeatingTheCommittedBusinessWrite() =>
        browser.RunAsync(nameof(Staff_LostActionReplyReconnectsWithoutRepeatingTheCommittedBusinessWrite), "staff", async test =>
        {
            IPage page = test.Page; await test.SignInAsync(page, "alex");
            string key = (await page.Locator("[data-node='jobs'] tbody tr").First.GetAttributeAsync("data-row-key"))!;
            await page.Locator($"[data-node='jobs'] [data-row-key='{key}'] button").ClickAsync();
            await EditNotesAsync(page, "Committed while the reply is lost");
            var peer = await test.NewContextAsync(); var observer = await peer.NewPageAsync(); await test.SignInAsync(observer, "sam");
            test.SuppressActionReply();
            await page.Locator("[data-node='save']").ClickAsync();
            await BrowserCase.SubmitAsync(observer, "Read the committed shared operation");
            using var committed = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (await observer.Locator("[data-node='history'] tbody tr").CountAsync() == 0)
            {
                committed.Token.ThrowIfCancellationRequested();
                await Task.Delay(100, committed.Token);
                await observer.ReloadAsync();
                await Expect(observer.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true })).ToBeEnabledAsync();
            }
            await Expect(observer.Locator("[data-node='history'] tbody tr")).ToHaveCountAsync(1);
            Assert.True(test.SuppressedFrames > 0);
            await test.DisconnectAndReconnectAsync();
            await Expect(page.Locator(".ib-output")).ToContainTextAsync("Succeeded:");
            await Expect(page.Locator("[data-node='history'] tbody tr")).ToHaveCountAsync(1);
            await page.ReloadAsync();
            await Expect(page.Locator("[data-node='history'] tbody tr")).ToHaveCountAsync(1);
            await page.Locator($"[data-node='jobs'] [data-row-key='{key}'] button").ClickAsync();
            await Expect(page.Locator("[data-node='currentNotes']")).ToContainTextAsync("Committed while the reply is lost");
        });

    [Fact]
    public Task Assets_ImportRejectBoundsResizeDownloadRealPngAndRetainPrivateOriginal() =>
        browser.RunAsync(nameof(Assets_ImportRejectBoundsResizeDownloadRealPngAndRetainPrivateOriginal), "assets", async test =>
        {
            IPage page = test.Page; await test.SignInAsync(page);
            await page.GetByRole(AriaRole.Button, new() { Name = "Add original sample assets", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Select version gold-tile.png", Exact = true }).ClickAsync();
            byte[] original = await DownloadAsync(test, "original.png"); AssertImage(original, 64, 64);
            string originalUrl = (await page.Locator(".ib-image-editor img").GetAttributeAsync("src"))!;
            await BrowserCase.ChangeAsync(page, "Target width", "16");
            await BrowserCase.ChangeAsync(page, "Target height", "24");
            await page.GetByRole(AriaRole.Button, new() { Name = "Resize preview", Exact = true }).ClickAsync();
            await Expect(page.Locator(".ib-image-editor")).ToContainTextAsync("16 × 24");
            byte[] derived = await DownloadAsync(test, "derived.png"); AssertImage(derived, 16, 24);
            Assert.False(original.SequenceEqual(derived));
            string derivedUrl = (await page.Locator(".ib-image-editor img").GetAttributeAsync("src"))!;
            Assert.NotEqual(originalUrl, derivedUrl);
            await page.GetByRole(AriaRole.Button, new() { Name = "Show original", Exact = true }).ClickAsync();
            await Expect(page.Locator(".ib-image-editor img")).ToHaveAttributeAsync("src", originalUrl);
            Assert.Equal(original, await DownloadAsync(test, "original-again.png"));
            await page.GetByRole(AriaRole.Button, new() { Name = "Show preview", Exact = true }).ClickAsync();
            await Expect(page.Locator(".ib-image-editor img")).ToHaveAttributeAsync("src", derivedUrl);
            await Expect(page.Locator("[data-node='preview']")).ToContainTextAsync("resize");
            var peer = await test.NewContextAsync(); var other = await peer.NewPageAsync(); await test.SignInAsync(other);
            var denied = await peer.APIRequest.GetAsync(new Uri(new Uri(test.Server.Url), derivedUrl).ToString());
            Assert.True(denied.Status is 403 or 404);
            await page.GetByRole(AriaRole.Button, new() { Name = "Save workspace", Exact = true }).ClickAsync();
            await Expect(page.GetByText("Workspace checkpoint saved; business actions are separate.", new() { Exact = true })).ToBeVisibleAsync();
            int attachments = await page.Locator(".ib-attachments li").CountAsync();
            await ReloadForUploadAsync(page);
            await page.GetByLabel("Attach content").SetInputFilesAsync(new FilePayload { Name = "invalid.png", MimeType = "image/png", Buffer = [1, 2, 3] });
            await Expect(page.Locator(".ib-attachments")).ToContainTextAsync("The upload was rejected.");
            await Expect(page.Locator(".ib-attachments li")).ToHaveCountAsync(attachments);
            string limits = await page.Locator(".ib-attachments small").InnerTextAsync();
            int maximumBytes = int.Parse(Regex.Match(limits, @"(\d+) KiB each").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) * 1024;
            byte[] oversized = new byte[maximumBytes + 1]; original.CopyTo(oversized, 0);
            await ReloadForUploadAsync(page);
            await page.GetByLabel("Attach content").SetInputFilesAsync(new FilePayload { Name = "oversized.png", MimeType = "image/png", Buffer = oversized });
            await Expect(page.Locator(".ib-attachments")).ToContainTextAsync("The upload was rejected.");
            await Expect(page.Locator(".ib-attachments li")).ToHaveCountAsync(attachments);
            int maximum = int.Parse(Regex.Match(limits, @"Up to (\d+) files").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            await ReloadForUploadAsync(page);
            await page.GetByLabel("Attach content").SetInputFilesAsync(Enumerable.Range(0, maximum + 1)
                .Select(index => new FilePayload { Name = $"bounded-{index}.png", MimeType = "image/png", Buffer = original }));
            await Expect(page.Locator(".ib-attachments")).ToContainTextAsync("The upload was rejected.");
            await Expect(page.Locator(".ib-attachments li")).ToHaveCountAsync(attachments);
            await page.GetByLabel("Attach content").SetInputFilesAsync(new FilePayload { Name = "uploaded.png", MimeType = "image/png", Buffer = original });
            await Expect(page.Locator(".ib-attachments")).ToContainTextAsync("Attached uploaded.png.");
            await Expect(page.Locator(".ib-attachments li")).ToHaveCountAsync(attachments + 1);
            await BrowserCase.SubmitAsync(page, "Inspect imported images and retain the current version");
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Select version uploaded.png", Exact = true })).ToBeVisibleAsync();
        });

    [Fact]
    public Task PackedQuickstart_PublicRendererHandlesRealEventsAndRetainsDraftThroughFollowup() =>
        browser.RunAsync(nameof(PackedQuickstart_PublicRendererHandlesRealEventsAndRetainsDraftThroughFollowup), "packed", async test =>
        {
            await BrowserCase.SubmitAsync(test.Page, "Open the synthetic catalog");
            var lantern = test.Page.GetByRole(AriaRole.Button, new() { Name = "Brass lantern", Exact = true });
            await Expect(lantern).ToHaveAttributeAsync("aria-pressed", "false");
            await lantern.ClickAsync(); await Expect(lantern).ToHaveAttributeAsync("aria-pressed", "true");
            var increase = test.Page.GetByRole(AriaRole.Button, new() { Name = "Increase score", Exact = true });
            await increase.FocusAsync(); await increase.PressAsync("Enter");
            await Expect(test.Page.GetByRole(AriaRole.Meter, new() { Name = "Your score", Exact = true })).ToHaveAttributeAsync("value", "4");
            await increase.PressAsync("Space");
            await Expect(test.Page.GetByRole(AriaRole.Meter, new() { Name = "Your score", Exact = true })).ToHaveAttributeAsync("value", "5");
            await BrowserCase.SubmitAsync(test.Page, "Keep my local score");
            await Expect(test.Page.GetByRole(AriaRole.Meter, new() { Name = "Your score", Exact = true })).ToHaveAttributeAsync("value", "5");
            await Expect(test.Page.Locator(".ib-draft")).ToBeVisibleAsync();
        });

    private static async Task EditNotesAsync(IPage page, string text)
    {
        await BrowserCase.ChangeAsync(page, "My follow-up notes", text);
        await Expect(page.Locator(".ib-draft")).ToBeVisibleAsync();
    }
    private static async Task ReloadForUploadAsync(IPage page)
    {
        // Remove the prior identical rejection notice so a previous result cannot
        // satisfy the assertion for the next upload attempt.
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true })).ToBeEnabledAsync();
        await Expect(page.Locator(".ib-attachments")).Not.ToContainTextAsync("The upload was rejected.");
    }
    private static async Task<byte[]> DownloadAsync(BrowserCase test, string name)
    {
        IDownload download = await test.Page.RunAndWaitForDownloadAsync(() =>
            test.Page.GetByRole(AriaRole.Link, new() { Name = "Download displayed image", Exact = true }).ClickAsync());
        string path = Path.Combine(test.Server.DataDirectory, name); await download.SaveAsAsync(path);
        Assert.Null(await download.FailureAsync()); return await File.ReadAllBytesAsync(path);
    }
    private static void AssertImage(byte[] bytes, int width, int height)
    {
        using SKBitmap image = SKBitmap.Decode(bytes); Assert.NotNull(image);
        Assert.Equal(width, image.Width); Assert.Equal(height, image.Height);
    }
}
