using System.Collections.Immutable;
using System.Text.Json;
using IntentBlazor.AI;
using IntentBlazor.Verification;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace IntentBlazor.Tests;

public sealed class FoundationProviderTests
{
    [Theory]
    [InlineData("object", true)]
    [InlineData("legacy", true)]
    [InlineData("string-in-object-slot", false)]
    [InlineData("extra-data", false)]
    [InlineData("forged-document-data", false)]
    public async Task LocalDocumentEnvelope_AcceptsValidatedObjectsAndRejectsForgedOrWronglyTypedContent(string mode, bool accepted)
    {
        OutputDocument document = TestContent.Catalog.Get("news").Document;
        string text = mode switch
        {
            "legacy" => ModelTestSupport.Envelope(),
            "string-in-object-slot" => JsonSerializer.Serialize(new { document = OutputJson.SerializeDocument(document) }),
            "extra-data" => JsonSerializer.Serialize(new { document, data = TestContent.Catalog.Get("news").Data }, OutputJson.Options),
            "forged-document-data" => JsonSerializer.Serialize(new { document = document with { DataSets = ["unrelated-private-data"] } }, OutputJson.Options),
            _ => JsonSerializer.Serialize(new { document }, OutputJson.Options)
        };
        ScriptChatClient client = new(ModelTestSupport.Text(text));
        ServiceCollection services = new(); services.AddSingleton(TestContent.Catalog);
        services.AddIntentBlazorAI(options => options.MaximumCorrections = 0)
            .UseChatClient(_ => client, new("Scripted local", "test", null, NativeTools: false) { UseDocumentObject = true })
            .AddTools<IntentBlazor.Examples.NewsDataService>();
        using ServiceProvider root = services.BuildServiceProvider(); using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        Assert.True(session.Open(TestContent.Catalog.Get("news")).Success);
        long revision = session.Revision;
        Assert.Equal(accepted, (await session.SubmitAsync("News with local object delivery")).Success);
        Assert.Equal("news-reader", session.Document!.Id);
        Assert.Equal(accepted ? revision + 1 : revision, session.Revision);
        Assert.Single(client.Calls);
        if (accepted) TestContent.Select(session, "headlines", "news-2");
    }

    [Fact]
    public async Task NativeReads_LoadEveryRequiredDatasetBeforeApplyingPresentationFormat()
    {
        ScriptChatClient client = new(
            ModelTestSupport.Tool("fixture_records", new Dictionary<string, object?> { ["dataSetId"] = "jobs" }),
            ModelTestSupport.Tool("fixture_records", new Dictionary<string, object?> { ["dataSetId"] = "staff" }),
            ModelTestSupport.Text(ModelTestSupport.Envelope("staff")));
        EvaluationData data = new(TestContent.Catalog, "staff");
        ServiceCollection services = new(); services.AddSingleton(data);
        services.AddIntentBlazorAI().UseChatClient(_ => client, new("Scripted", "test", null)).AddTools<EvaluationData>();
        using ServiceProvider root = services.BuildServiceProvider(); using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        Assert.True(session.Open(TestContent.Catalog.Get("staff")).Success);
        Assert.True((await session.SubmitAsync("Refresh both authorized datasets and keep the editor")).Success);
        Assert.Equal(2, data.Calls); Assert.Equal(3, client.Calls.Count);
        Assert.All(client.Calls.Take(2), call => { Assert.Null(call.Options!.ResponseFormat); Assert.Equal(ChatToolMode.RequireAny, call.Options.ToolMode); });
        Assert.IsType<ChatResponseFormatJson>(client.Calls[2].Options!.ResponseFormat);
        Assert.Equal(ChatToolMode.Auto, client.Calls[2].Options!.ToolMode);
        Assert.Equal(new[] { "jobs", "staff" }, session.Data.Select(set => set.Id).Order().ToArray());
        Assert.Equal("Refresh both authorized datasets and keep the editor", client.Calls[0].Messages.Last().Text);
        Assert.DoesNotContain("\"whenState\":null", client.Calls[0].Messages.First(message => message.Role == ChatRole.User).Text);
    }

    [Fact]
    public async Task NativeCorrection_RetainsAuthorizedToolDataAndReportsCodesWithoutPrivateText()
    {
        ScriptChatClient client = new(ModelTestSupport.Tool(), ModelTestSupport.Text("private-invalid-proposal"), ModelTestSupport.Text(ModelTestSupport.Envelope()));
        using ServiceProvider root = ModelTestSupport.Provider(client); using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        Assert.True(session.Open(TestContent.Catalog.Get("news")).Success);
        Assert.True((await session.SubmitAsync("Refresh news")).Success);
        Assert.Equal(3, client.Calls.Count);
        ChatMessage data = Assert.Single(client.Calls[2].Messages, message => message.Text.StartsWith("{\"hostData\":", StringComparison.Ordinal));
        using JsonDocument context = JsonDocument.Parse(data.Text);
        Assert.Equal("articles", context.RootElement.GetProperty("hostData")[0].GetProperty("id").GetString());
        Assert.Equal(2, context.RootElement.GetProperty("hostData")[0].GetProperty("rows").GetArrayLength());
        ModelRunReport report = scope.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!;
        Assert.Equal(1, report.ToolCalls); Assert.Equal("completed", report.Status);
        Assert.Equal("invalid-json", Assert.Single(Assert.Single(report.Validation).ErrorCodes));
        Assert.DoesNotContain("private-invalid-proposal", JsonSerializer.Serialize(report));
        Assert.DoesNotContain("Fictional fixture", JsonSerializer.Serialize(report));
    }

    [Fact]
    public void PaidEvaluation_RejectsMissingOptInBeforeResolvingCredentialsOrStartingCodex()
    {
        int credentials = 0;
        Assert.Throws<ArgumentException>(() => EvaluationConnection.OpenAI("model", "revision", "api", _ => { credentials++; return "test-only"; }));
        Assert.Throws<ArgumentException>(() => EvaluationConnection.Codex("model", "revision", "cli", "not-a-native-path"));
        Assert.Equal(0, credentials);
    }

    [Fact]
    public async Task OpenAIEvaluation_UsesOfficialSdkEndpointAndNeverNeedsARealKeyInTests()
    {
        RecordingHandler handler = new(); ServiceCollection services = new();
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(new TestHandlerFilter(handler));
        int credentials = 0;
        EvaluationConnection connection = EvaluationConnection.OpenAI("test-model", "test-revision", "test-api", _ => { credentials++; return "test-only-key"; }, liveApproved: true);
        connection.Register(services);
        using ServiceProvider root = services.BuildServiceProvider(); using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        Assert.Equal(0, credentials); Assert.Empty(handler.Requests);
        Assert.True((await session.SubmitAsync("A simple output")).Success);
        Assert.Equal(1, credentials);
        Assert.Equal("https://api.openai.com/v1/chat/completions", Assert.Single(handler.Requests).Uri!.AbsoluteUri);
        Assert.Equal("OpenAI", scope.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!.Provider);
    }

    [Fact]
    public async Task CodexEvaluation_UsesPreloadAndARestrictedIndependentProcessRequest()
    {
        string executable = Path.Combine(Path.GetTempPath(), OperatingSystem.IsWindows() ? "codex.exe" : "codex");
        EvaluationConnection connection = EvaluationConnection.Codex("test-codex", "revision", "cli", executable, liveApproved: true);
        ScriptCodexRunner runner = new(JsonSerializer.Serialize(new { type = "item.completed", item = new { type = "agent_message", text = ModelTestSupport.Envelope() } }),
            "{\"type\":\"turn.completed\"}");
        ServiceCollection services = new(); services.AddSingleton(TestContent.Catalog); services.AddSingleton<ICodexProcessRunner>(runner);
        connection.Register(services).AddTools<IntentBlazor.Examples.NewsDataService>();
        using ServiceProvider root = services.BuildServiceProvider(); using IServiceScope scope = root.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("News")).Success);
        Assert.False(connection.NativeTools); Assert.Null(connection.Endpoint);
        Assert.Contains("Fictional fixture", runner.Request!.Prompt);
        string[] args = CodexProcessRunner.CreateStartInfo(new() { ExecutablePath = executable, Model = "test-codex" }, Path.GetTempPath(), "schema.json").ArgumentList.ToArray();
        Assert.Contains("--ephemeral", args); Assert.Contains("--ignore-user-config", args); Assert.Contains("--ignore-rules", args);
        Assert.Contains("read-only", args); Assert.Contains("shell_tool", args); Assert.Contains("apps", args); Assert.Contains("web_search=\"disabled\"", args);
        Assert.Contains("project_doc_max_bytes=0", args); Assert.Contains("memories", args); Assert.Contains("plugins", args); Assert.Contains("skip_host_skill_discovery", args);
        Assert.DoesNotContain("resume", args);
    }

    [Theory]
    [InlineData("news", true)]
    [InlineData("staff", true)]
    [InlineData("assets", true)]
    [InlineData("news", false)]
    [InlineData("staff", false)]
    [InlineData("assets", false)]
    public async Task ApplicationEvaluation_UsesRealServicesAndActionsWithDefaultComponents(string application, bool preload)
    {
        EchoWorkspaceClient client = new(native: !preload);
        EvaluationConnection connection = EvaluationConnection.Local("Ollama", "scripted-only", "test-revision", "test-server", new("http://localhost:11434/v1/"));
        ApplicationScenarioResult result = await ApplicationEvaluationRunner.RunAsync(application, connection, new(), preload,
            configureTestTransport: services => services.AddScoped<IChatClient>(_ => client));
        Assert.True(result.Passed, JsonSerializer.Serialize(result, VerificationJson.Options));
        int reads = preload ? 2 : application switch { "news" => 8, "staff" => 6, _ => 2 };
        Assert.Equal(reads, result.Model.ObservedDataCalls); Assert.Equal(preload ? 2 : reads + 2, client.Calls);
        Assert.True(result.Model.InitialAccepted); Assert.True(result.Model.FollowupAccepted); Assert.True(result.Model.DraftPreserved); Assert.True(result.Model.SelectionPreserved);
        Assert.All(ApplicationEvaluationRunner.RequiredChecks(application), key => Assert.True(result.Checks[key], key));
        if (!preload)
        {
            Assert.All(new[] { result.Model.InitialRun, result.Model.FollowupRun }, run =>
            {
                Assert.Equal(reads / 2, run!.ToolCalls);
                Assert.All(run.Tools, tool => Assert.Equal("completed", tool.Outcome));
            });
        }
    }

    [Fact]
    public void ApplicationEvidence_CannotPassFromAnAcceptedGreetingOrAnIncompleteActionChecklist()
    {
        ModelRunReport run = new("completed", "test", "model", 1, 0, 0, 200, null, null, TimeSpan.Zero, null);
        ModelScenarioResult model = new("assets", true, true, 2, true, true, true, 200, 1, 1, 1, run, run, []);
        Assert.False(new ApplicationScenarioResult(model, ImmutableDictionary<string, bool>.Empty.Add("resize", true)).Passed);
        Assert.False(new ApplicationScenarioResult(model with { LocalInteraction = false }, ApplicationEvaluationRunner.RequiredChecks("assets").ToImmutableDictionary(key => key, _ => true)).Passed);
        Assert.Null((model with { InitialAccepted = false }).TimeToAcceptedOutputMilliseconds);
        Assert.Equal(1, model.TimeToAcceptedOutputMilliseconds);
    }

    [Fact]
    public void EvaluationMeasurements_RecordContextGrowthWithoutRetainingMessageOrToolContent()
    {
        const string body = """{"messages":[{"content":"private-prompt"}],"tools":[{"description":"private-description"}],"stream":true} """;
        EvaluationRequestMeasurement measurement = EvaluationDiagnostics.Measure(body, "followup");
        Assert.Equal("followup", measurement.Phase); Assert.True(measurement.Stream);
        Assert.Equal("[{\"content\":\"private-prompt\"}]".Length, measurement.SerializedMessageCharacters);
        Assert.Equal("[{\"description\":\"private-description\"}]".Length, measurement.SerializedToolCharacters);
        Assert.DoesNotContain("private", JsonSerializer.Serialize(measurement));
        Assert.Equal(new("initial", 0, 0, false), EvaluationDiagnostics.Measure("{}", "initial"));
    }

    [Theory]
    [InlineData("news")]
    [InlineData("staff")]
    [InlineData("assets")]
    public async Task ApplicationReads_RejectUnknownDatasetsAndExpiredIdentityWithoutSuccessfulReadEvidence(string application)
    {
        EvaluationConnection connection = EvaluationConnection.Local("Ollama", "scripted-only", "revision", "server", new("http://localhost:11434/v1/"));
        await using ApplicationEvaluationHost host = await ApplicationEvaluationHost.CreateAsync(application, connection, new(), preload: true);
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        ApplicationEvaluationData data = scope.ServiceProvider.GetRequiredService<ApplicationEvaluationData>();
        await Assert.ThrowsAsync<InputRejectedException>(() => data.ReadAsync("unrelated-private-records", default));
        scope.ServiceProvider.GetRequiredService<EvaluationActor>().Current = null;
        await Assert.ThrowsAsync<InputRejectedException>(() => data.ReadAsync("articles", default));
        Assert.Equal(0, data.Calls);
    }

    [Fact]
    public void EvaluationScenarioSelection_SeparatesPartialDiagnosticsAndRejectsUnknownNames()
    {
        Assert.Equal(new[] { "news", "staff", "assets" }, ModelEvaluationRunner.SelectScenarios(null));
        foreach (string name in new[] { "news", "staff", "assets" }) Assert.Equal(name, Assert.Single(ModelEvaluationRunner.SelectScenarios(name)));
        Assert.Throws<ArgumentException>(() => ModelEvaluationRunner.SelectScenarios(""));
        Assert.Throws<ArgumentException>(() => ModelEvaluationRunner.SelectScenarios("unknown"));
    }

    private sealed class EchoWorkspaceClient(bool native = false) : IChatClient
    {
        public int Calls { get; private set; }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); Calls++;
            using JsonDocument context = JsonDocument.Parse(messages.First(message => message.Role == ChatRole.User).Text);
            string document = context.RootElement.GetProperty("workspace").GetProperty("document").GetRawText();
            if (native)
            {
                var loaded = messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Select(content => content.Result)
                    .OfType<JsonElement>().Where(element => element.ValueKind == JsonValueKind.Object && element.TryGetProperty("id", out _))
                    .Select(element => element.GetProperty("id").GetString()).ToHashSet();
                string? needed = context.RootElement.GetProperty("workspace").GetProperty("document").GetProperty("dataSets").EnumerateArray()
                    .Select(element => element.GetString()!).FirstOrDefault(id => !loaded.Contains(id));
                if (needed is not null) return Task.FromResult(ModelTestSupport.Tool("application_records", new Dictionary<string, object?> { ["dataSetId"] = needed }));
            }
            return Task.FromResult(ModelTestSupport.Text("{\"document\":" + document + "}"));
        }
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (ChatMessage message in response.Messages)
                yield return new() { Role = message.Role, Contents = message.Contents, FinishReason = ChatFinishReason.Stop };
        }
        public object? GetService(Type type, object? key = null) => key is null && type.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }
}
