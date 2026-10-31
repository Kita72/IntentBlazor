using System.Net;
using System.Text.Json;
using IntentBlazor.AI;
using IntentBlazor.Docs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

public sealed class Phase7DocsHostTests
{
    [Fact]
    public async Task ProductionHost_ServesFixtureDocsWithoutModelCredentialsOrSyntheticSignIn()
    {
        await using var app = DocsHost.Build(Options(), builder => builder.WebHost.UseTestServer());
        Assert.Empty(app.Services.GetServices<Microsoft.Extensions.AI.IChatClient>());
        await app.StartAsync(); using HttpClient client = app.GetTestClient(); client.BaseAddress = new("https://localhost");
        using var health = await client.GetAsync("/health/ready"); Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using JsonDocument data = JsonDocument.Parse(await health.Content.ReadAsStringAsync());
        Assert.Equal("fixture", data.RootElement.GetProperty("mode").GetString());
        Assert.Equal("InteractiveServer", data.RootElement.GetProperty("hosting").GetString());
        Assert.StartsWith("0.1.0-preview.", data.RootElement.GetProperty("version").GetString());
        using var guide = await client.GetAsync("/docs/quickstart"); Assert.Equal(HttpStatusCode.OK, guide.StatusCode);
        Assert.Contains("Two components, one working app", await guide.Content.ReadAsStringAsync());
        using var model = await client.GetAsync("/models"); Assert.Contains("Model mode is disabled", await model.Content.ReadAsStringAsync());
        using var signin = await client.GetAsync("/workspaces/demo-signin"); Assert.Equal(HttpStatusCode.NotFound, signin.StatusCode);
        Assert.False(signin.Headers.Contains("Set-Cookie"));
        using var missing = await client.GetAsync("/docs/no-such-guide"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await app.StopAsync();
    }

    [Theory]
    [InlineData("Ollama")]
    [InlineData("LMStudio")]
    [InlineData("OpenAI")]
    [InlineData("Codex")]
    public void ProductionHost_RejectsConfiguredAnonymousModelModeBeforeAnyInference(string provider)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => DocsHost.Build(Options(), builder =>
        {
            builder.WebHost.UseTestServer(); builder.Configuration["Ai:Provider"] = provider; builder.Configuration["Ai:Model"] = "test-only";
        }));
        Assert.Contains("only be enabled in Development", error.Message);
    }

    private static WebApplicationOptions Options()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "IntentBlazor.slnx"))) root = root.Parent;
        return new()
        {
            ApplicationName = typeof(DocsHost).Assembly.GetName().Name,
            EnvironmentName = "Production",
            ContentRootPath = Path.Combine(root?.FullName ?? throw new InvalidOperationException("Repository root unavailable."), "apps", "IntentBlazor.Docs")
        };
    }
}
