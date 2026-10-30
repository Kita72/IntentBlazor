using System.Collections.Immutable;
using System.Text.Json;
using IntentBlazor.AI;
using IntentBlazor.Blazor.AI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace IntentBlazor.Tests;

public sealed class ModelSetupTests
{
    [Fact]
    public async Task AppliedProfileAndOptions_AffectTheNextGenerationAndRemainScoped()
    {
        ScriptChatClient first = new(ModelTestSupport.Text(ModelTestSupport.Envelope())), alternate = new(ModelTestSupport.Text(ModelTestSupport.Envelope()));
        using ServiceProvider root = Provider(first, alternate); using IServiceScope one = root.CreateScope(); using IServiceScope two = root.CreateScope();
        ModelSetupState setup = one.ServiceProvider.GetRequiredService<ModelSetupState>(); var snapshot = await setup.ReadAsync();
        ModelOptions next = snapshot.Effective.Copy(); next.Temperature = .4f; next.TopP = .8f; next.MaximumOutputTokens = 1024; next.Seed = 12;
        Assert.True((await setup.ApplyAsync("alternate", next, snapshot.Revision)).Success);
        Assert.True((await one.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("news")).Success);
        Assert.Empty(first.Calls); var call = Assert.Single(alternate.Calls);
        Assert.Equal("alternate-model", call.Options!.ModelId); Assert.Equal(.4f, call.Options.Temperature); Assert.Equal(.8f, call.Options.TopP);
        Assert.Equal(1024, call.Options.MaxOutputTokens); Assert.Equal(12L, call.Options.Seed);
        Assert.Equal("default", (await two.ServiceProvider.GetRequiredService<ModelSetupState>().ReadAsync()).SelectedConnection);
        ModelRunReport report = one.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!;
        Assert.Equal("alternate-model", report.Model); Assert.Equal("get_gaming_news", Assert.Single(report.Tools).Tool);
        Assert.Equal("completed", report.Tools[0].Outcome); Assert.Equal(2, report.Tools[0].Rows);
    }

    [Theory]
    [InlineData("denied")]
    [InlineData("stale")]
    [InlineData("profile")]
    [InlineData("budget")]
    [InlineData("invalid")]
    [InlineData("advanced")]
    public async Task SettingsGuards_RejectUnauthorizedStaleUnsupportedAndExcessiveChanges(string kind)
    {
        ScriptChatClient client = new(ModelTestSupport.Text(ModelTestSupport.Envelope()));
        using ServiceProvider root = Provider(client); using IServiceScope scope = root.CreateScope();
        ModelSetupState setup = scope.ServiceProvider.GetRequiredService<ModelSetupState>(); MutableAiAccess access = scope.ServiceProvider.GetRequiredService<MutableAiAccess>();
        AiSetupSnapshot before = await setup.ReadAsync(); ModelOptions next = before.Effective.Copy(); string profile = "default"; long revision = before.Revision;
        switch (kind)
        {
            case "denied": access.Policy = access.Policy with { CanEdit = false }; break;
            case "stale": revision--; break;
            case "profile": profile = "not-registered"; break;
            case "budget": next.MaximumOutputTokens = 9000; break;
            case "invalid": next.Temperature = float.NaN; break;
            default: access.Policy = access.Policy with { AllowAdvanced = false }; next.TopP = .5f; break;
        }
        Assert.False((await setup.ApplyAsync(profile, next, revision)).Success);
        AiSetupSnapshot after = await setup.ReadAsync(); Assert.Equal(before.Revision, after.Revision); Assert.Equal(before.Effective.MaximumOutputTokens, after.Effective.MaximumOutputTokens);
        Assert.Null(after.Effective.TopP); Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task PermissionRevocationDuringApply_PreventsInstallingTheSettings()
    {
        using ServiceProvider root = Provider(new(ModelTestSupport.Text(ModelTestSupport.Envelope()))); using IServiceScope scope = root.CreateScope();
        ModelSetupState setup = scope.ServiceProvider.GetRequiredService<ModelSetupState>(); MutableAiAccess access = scope.ServiceProvider.GetRequiredService<MutableAiAccess>();
        AiSetupSnapshot before = await setup.ReadAsync(); int reads = 0;
        access.BeforeRead = () => { if (++reads == 2) access.Policy = access.Policy with { AllowedConnections = ImmutableHashSet<string>.Empty }; };
        Assert.Equal("setup-denied", Assert.Single((await setup.ApplyAsync("default", before.Effective, before.Revision)).Errors).Code);
        Assert.Equal(before.Revision, (await setup.ReadAsync()).Revision);
    }

    [Theory]
    [InlineData("output")]
    [InlineData("context")]
    [InlineData("advanced")]
    public async Task PolicyReductionAfterApply_RejectsTheNextGenerationBeforeContactingTheModel(string change)
    {
        ScriptChatClient client = new(ModelTestSupport.Text(ModelTestSupport.Envelope()));
        using ServiceProvider root = Provider(client); using IServiceScope scope = root.CreateScope();
        ModelSetupState setup = scope.ServiceProvider.GetRequiredService<ModelSetupState>(); AiSetupSnapshot before = await setup.ReadAsync();
        ModelOptions next = before.Effective.Copy(); next.TopP = .7f;
        Assert.True((await setup.ApplyAsync("default", next, before.Revision)).Success);
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>(); Assert.True(session.Open(TestContent.Catalog.Get("news")).Success);
        long revision = session.Revision;
        MutableAiAccess access = scope.ServiceProvider.GetRequiredService<MutableAiAccess>();
        access.Policy = change switch
        {
            "output" => access.Policy with { MaximumOutputTokens = next.MaximumOutputTokens - 1 },
            "context" => access.Policy with { MaximumContextCharacters = next.MaximumContextCharacters - 1 },
            _ => access.Policy with { AllowAdvanced = false }
        };
        Assert.False((await session.SubmitAsync("Use the applied settings")).Success);
        Assert.Empty(client.Calls); Assert.Equal(revision, session.Revision); Assert.Equal("news-reader", session.Document!.Id);
    }

    [Fact]
    public async Task ProbeConnectionRevokedDuringResponse_DoesNotPublishTheCapabilityResult()
    {
        ScriptChatClient client = new(ModelTestSupport.Text("{}"));
        using ServiceProvider root = Provider(client); using IServiceScope scope = root.CreateScope();
        MutableAiAccess access = scope.ServiceProvider.GetRequiredService<MutableAiAccess>();
        client.BeforeResponse = _ => { access.Policy = access.Policy with { AllowedConnections = ImmutableHashSet<string>.Empty }; return Task.CompletedTask; };
        ModelSetupState setup = scope.ServiceProvider.GetRequiredService<ModelSetupState>();
        ModelProbeResult result = await setup.ProbeAsync();
        Assert.False(result.Connected); Assert.False(result.ValidPresentation); Assert.Contains("changed", result.Message);
        Assert.Null(setup.LastProbe); Assert.Single(client.Calls);
    }

    [Theory]
    [InlineData("temperature")]
    [InlineData("sampling")]
    [InlineData("reasoning")]
    public async Task UnsupportedAdapterOptions_AreRejectedAndShownInTheSetupUi(string option)
    {
        using ServiceProvider root = Provider(new(ModelTestSupport.Text(ModelTestSupport.Envelope()))); using IServiceScope scope = root.CreateScope();
        ModelSetupState setup = scope.ServiceProvider.GetRequiredService<ModelSetupState>(); AiSetupSnapshot snapshot = await setup.ReadAsync(); ModelOptions next = snapshot.Effective.Copy();
        if (option == "temperature") next.Temperature = .2f; else if (option == "sampling") next.TopP = .6f; else next.ReasoningEffort = "high";
        Assert.Equal("setup-unsupported", Assert.Single((await setup.ApplyAsync("cli", next, snapshot.Revision)).Errors).Code);
        Assert.True((await setup.ApplyAsync("cli", snapshot.Effective, snapshot.Revision)).Success);
        await using HtmlRenderer renderer = new(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            string html = (await renderer.RenderComponentAsync<AiSetup>()).ToHtmlString();
            Assert.Contains("temperature unsupported", html); Assert.Contains("output-token control unsupported", html);
            Assert.Contains("disabled", html); Assert.DoesNotContain("test-only-key", html);
            Assert.DoesNotContain("@onclick", html); Assert.DoesNotContain("@onchange", html); Assert.DoesNotContain("@bind", html);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExplicitProbe_DistinguishesConnectionFromValidPresentationAndNeverInvokesTools(bool valid)
    {
        string document = JsonSerializer.Serialize(new { documentJson = "{\"id\":\"probe\",\"rootId\":\"root\",\"nodes\":[{\"id\":\"root\",\"kind\":\"text\",\"value\":{\"literal\":\"Connected\"}}]}" });
        ScriptChatClient client = new(ModelTestSupport.Text(valid ? document : "[]"));
        using ServiceProvider root = Provider(client); using IServiceScope scope = root.CreateScope();
        ModelSetupState setup = scope.ServiceProvider.GetRequiredService<ModelSetupState>(); Assert.Empty(client.Calls);
        ModelProbeResult result = await setup.ProbeAsync(); Assert.True(result.Connected); Assert.Equal(valid, result.ValidPresentation); Assert.False(result.ToolUseVerified);
        var call = Assert.Single(client.Calls); Assert.Null(call.Options!.Tools); Assert.DoesNotContain("Fictional fixture", string.Concat(call.Messages.Select(item => item.Text)));
        scope.ServiceProvider.GetRequiredService<MutableAiAccess>().Policy = new();
        Assert.False((await setup.ProbeAsync()).Connected); Assert.Single(client.Calls);
    }

    [Fact]
    public async Task ProviderSpecificReasoningAndSampling_ReachTheRealSdkRequestBody()
    {
        ServiceCollection services = new(); RecordingHandler handler = new();
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(new TestHandlerFilter(handler));
        services.AddIntentBlazorAI(options => { options.TopP = .7f; options.FrequencyPenalty = .2f; options.PresencePenalty = .3f; options.Seed = 42; options.ReasoningEffort = "low"; })
            .UseOpenAI("test-model", _ => "test-only-key", supportsReasoningEffort: true);
        using ServiceProvider root = services.BuildServiceProvider(); using IServiceScope scope = root.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("Build output")).Success);
        using JsonDocument body = JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        Assert.Equal(.7, body.RootElement.GetProperty("top_p").GetDouble(), 3); Assert.Equal(42, body.RootElement.GetProperty("seed").GetInt64());
        Assert.Equal("low", body.RootElement.GetProperty("reasoning_effort").GetString());
        Assert.Equal(.2, body.RootElement.GetProperty("frequency_penalty").GetDouble(), 3); Assert.Equal(.3, body.RootElement.GetProperty("presence_penalty").GetDouble(), 3);
    }

    [Fact]
    public async Task NamedProviderPreset_UsesItsOwnHostConfiguredTransportWithoutResolvingDefaultCredentials()
    {
        ServiceCollection services = new(); services.AddLogging(); RecordingHandler handler = new();
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(new TestHandlerFilter(handler));
        services.AddIntentBlazorAI().UseOpenAI("frontier", _ => throw new InvalidOperationException("Default credential must not be resolved."))
            .AddLMStudioPreset("local", "local-model", new("http://localhost:5234/v1/"));
        services.AddIntentAiSetup<MutableAiAccess>(); using ServiceProvider root = services.BuildServiceProvider(); using IServiceScope scope = root.CreateScope();
        scope.ServiceProvider.GetRequiredService<MutableAiAccess>().Policy = new(true, true) { AllowedConnections = ImmutableHashSet.Create("default", "local") };
        ModelSetupState setup = scope.ServiceProvider.GetRequiredService<ModelSetupState>(); AiSetupSnapshot snapshot = await setup.ReadAsync();
        Assert.True((await setup.ApplyAsync("local", snapshot.Effective, snapshot.Revision)).Success);
        Assert.True((await scope.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("Show output")).Success);
        var request = Assert.Single(handler.Requests); Assert.Equal("http://localhost:5234/v1/chat/completions", request.Uri!.ToString());
        using JsonDocument body = JsonDocument.Parse(request.Body); Assert.Equal("local-model", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(8000, body.RootElement.GetProperty("max_tokens").GetInt32()); Assert.Equal("Bearer lm-studio", request.Authorization);
    }

    [Fact]
    public async Task Probe_RechecksRevokedPermissionBeforeSendingAndSanitizesFailures()
    {
        ScriptChatClient client = new(ModelTestSupport.Text("{}")); using ServiceProvider root = Provider(client); using IServiceScope scope = root.CreateScope();
        MutableAiAccess access = scope.ServiceProvider.GetRequiredService<MutableAiAccess>(); int reads = 0;
        access.BeforeRead = () => { if (++reads == 2) access.Policy = access.Policy with { CanProbe = false }; };
        ModelSetupState setup = scope.ServiceProvider.GetRequiredService<ModelSetupState>(); Assert.False((await setup.ProbeAsync()).Connected); Assert.Empty(client.Calls);
        access.BeforeRead = null; access.Policy = access.Policy with { CanProbe = true };
        client.BeforeResponse = _ => throw new InvalidOperationException("private upstream secret");
        ModelProbeResult result = await setup.ProbeAsync(); Assert.False(result.Connected); Assert.DoesNotContain("private", result.Message); Assert.DoesNotContain("secret", result.Message);
    }
    private static ServiceProvider Provider(ScriptChatClient client, ScriptChatClient? alternate = null)
    {
        ServiceCollection services = new(); services.AddLogging(); services.AddSingleton(TestContent.Catalog);
        services.AddIntentBlazorAI().UseChatClient(_ => client, new("Scripted", "default-model", null, NativeTools: false)).AddTools<NewsDataService>();
        services.AddIntentAiSetup<MutableAiAccess>();
        services.AddIntentModelPreset(new("alternate", new("Scripted alternate", "alternate-model", null, NativeTools: false), _ => alternate ?? client));
        services.AddIntentModelPreset(new("cli", new("CLI test adapter", "cli-model", null, NativeTools: false, SupportsOutputTokenLimit: false, SupportsTemperature: false,
            SupportsAdvancedSampling: false), _ => client));
        return services.BuildServiceProvider(validateScopes: true);
    }
    public sealed class MutableAiAccess : IAiSetupAccess
    {
        public AiSetupPolicy Policy { get; set; } = new(true, true, true, true, true)
        { AllowedConnections = ImmutableHashSet.Create("default", "alternate", "cli") };
        public Action? BeforeRead { get; set; }
        public ValueTask<AiSetupPolicy> GetPolicyAsync(CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); BeforeRead?.Invoke(); return ValueTask.FromResult(Policy); }
    }
}
