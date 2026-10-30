using System.Text.Json;
using IntentBlazor.AI;
using IntentBlazor.AssetWorkbench;
using IntentBlazor.ExampleHost;
using IntentBlazor.GamingNews;
using IntentBlazor.StaffBusiness;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace IntentBlazor.Tests;

public sealed class Phase5HostTests
{
    [Theory]
    [InlineData("news", "articles")]
    [InlineData("staff", "jobs")]
    [InlineData("assets", "artifacts")]
    public async Task OptionalModelMode_UsesTheAppsAuthorizedSourceAndDefersInferenceUntilSubmission(string kind, string expectedData)
    {
        string directory = Path.Combine(Path.GetTempPath(), "IntentBlazor-model-example-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = directory });
            builder.Configuration["Example:DataDirectory"] = directory; builder.Configuration["Ai:Provider"] = "LMStudio";
            builder.Configuration["Ai:Model"] = "phase5-test-model";
            RecordingHandler handler = new(); builder.Services.AddSingleton<IHttpMessageHandlerBuilderFilter>(new TestHandlerFilter(handler));
            builder.Services.AddExampleHost(builder.Configuration, builder.Environment, "test-" + kind, "Test", "Synthetic", kind == "staff");
            builder.Services.AddScoped<Phase5Actor>(); builder.Services.AddScoped<IExampleActor>(sp => sp.GetRequiredService<Phase5Actor>());
            builder.Services.AddScoped<IWorkspaceIdentity>(sp => sp.GetRequiredService<Phase5Actor>());
            if (kind == "news") builder.Services.AddGamingNews(builder.Configuration, directory);
            else if (kind == "staff") builder.Services.AddStaffBusiness(builder.Configuration, directory);
            else builder.Services.AddAssetWorkbench(builder.Configuration, directory);
            await using var app = builder.Build();
            if (kind == "staff") await EfStaffRepository.InitializeAsync(app.Services.GetRequiredService<IDbContextFactory<BusinessDbContext>>());
            using var scope = app.Services.CreateScope(); var services = scope.ServiceProvider;
            services.GetRequiredService<Phase5Actor>().Current = new("alex", "Alex", System.Collections.Immutable.ImmutableHashSet.Create("Dispatcher"));
            services.GetRequiredService<WorkspaceServices>().AutoSave = false;
            Assert.IsType<ModelInputProcessor>(services.GetRequiredService<IInputProcessor>());
            Assert.Same(services.GetRequiredService<IExampleScenario>(), Assert.Single(services.GetServices<IOutputDataSource>()));
            await services.GetRequiredService<ExampleWorkspaceController>().StartAsync(); Assert.Empty(handler.Requests);
            Assert.Contains(services.GetRequiredService<AiSession>().Data, set => set.Id == expectedData);
            Assert.True((await services.GetRequiredService<AiSession>().SubmitAsync("Inspect the current authorized workspace")).Success);
            var request = Assert.Single(handler.Requests); using JsonDocument body = JsonDocument.Parse(request.Body);
            Assert.Equal("phase5-test-model", body.RootElement.GetProperty("model").GetString());
            JsonElement userContent = body.RootElement.GetProperty("messages").EnumerateArray().Last(message => message.GetProperty("role").GetString() == "user").GetProperty("content");
            string text = userContent.ValueKind == JsonValueKind.String ? userContent.GetString()! : string.Concat(userContent.EnumerateArray().Select(part => part.GetProperty("text").GetString()));
            using JsonDocument context = JsonDocument.Parse(text);
            Assert.Contains(context.RootElement.GetProperty("hostData").EnumerateArray(), set => set.GetProperty("id").GetString() == expectedData);
            Assert.Equal("Connected test double", services.GetRequiredService<AiSession>().Document!.Nodes[0].Value!.Literal.GetString());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
    [Theory]
    [InlineData("news", "Recent updates")]
    [InlineData("staff", "Jobs waiting on materials")]
    [InlineData("assets", "Dimension inspection")]
    public async Task EachStandaloneScenario_UsesDefaultAiInputAndAiOutputWithCredentialFreeFixtureMode(string app, string expected)
    {
        using Phase5Harness h = await Phase5Harness.CreateAsync(app); using Phase5Scope scope = h.Scope(); await scope.StartAsync();
        var html = await scope.RenderAsync(); Assert.Contains("Request", html.Input); Assert.Contains(expected, html.Output);
        Assert.Equal(app == "assets", html.Input.Contains("Attach content")); Assert.DoesNotContain("@onclick", html.Output);
        Assert.Empty(scope.Services.GetServices<IChatClient>()); Assert.Equal("main", scope.Session.WorkspaceKey);
    }
    [Fact]
    public void SyntheticIdentities_AreServerIssuedAllowlistedAndIsolatedByApp()
    {
        ExampleHostOptions staff = new("staff", "Staff", "Synthetic", "unused", true);
        Assert.Null(staff.CreatePrincipal("arbitrary-admin")); var reader = staff.CreatePrincipal("riley")!;
        Assert.True(reader.IsInRole("Reader")); Assert.False(reader.IsInRole("Dispatcher")); Assert.Equal("riley", reader.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        ExampleHostOptions news = new("news", "News", "Synthetic", "unused", false);
        string First() => news.CreatePrincipal("private")!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        string one = First(), two = First(); Assert.StartsWith("news-", one); Assert.NotEqual(one, two); Assert.Null(news.CreatePrincipal("alex"));
    }
    [Fact]
    public async Task WorkspaceControls_ForkAndReopenRetainPrivateValuesAndDenyAnotherOwner()
    {
        using Phase5Harness h = await Phase5Harness.CreateAsync("news"); using Phase5Scope alex = h.Scope(); await alex.StartAsync();
        alex.Change("preference", "Emberfall"); var controls = alex.Services.GetRequiredService<ExampleWorkspaceController>();
        await controls.ForkAsync("comparison-copy"); Assert.Equal("comparison-copy", alex.Session.WorkspaceKey);
        using Phase5Scope reopened = h.Scope(); await reopened.StartAsync();
        await reopened.Services.GetRequiredService<ExampleWorkspaceController>().OpenAsync("comparison-copy");
        Assert.Equal("Emberfall", TestContent.Value(reopened.Session, "preferredGame")); Assert.True(reopened.Session.HasDraft);
        using Phase5Scope other = h.Scope("sam"); await other.StartAsync();
        await other.Services.GetRequiredService<ExampleWorkspaceController>().OpenAsync("comparison-copy");
        Assert.Equal("main", other.Session.WorkspaceKey); Assert.Equal("all", TestContent.Value(other.Session, "preferredGame"));
    }
    [Fact]
    public async Task UnavailableInitialSource_LeavesTheInputUsableAndCanRecoverWithoutLeakingDetails()
    {
        using Phase5Harness h = await Phase5Harness.CreateAsync("news"); using Phase5Scope scope = h.Scope();
        h.Source.Failure = new HttpRequestException("private endpoint credential detail");
        var controls = scope.Services.GetRequiredService<ExampleWorkspaceController>(); await controls.StartAsync();
        Assert.Null(scope.Session.Document); Assert.Contains("unavailable", controls.Message!); Assert.DoesNotContain("credential", controls.Message!);
        Assert.Contains("Request", (await scope.RenderAsync()).Input);
        h.Source.Failure = null; Assert.True((await scope.Session.SubmitAsync("Retry updates")).Success); Assert.NotNull(scope.Session.Document);
    }
}
