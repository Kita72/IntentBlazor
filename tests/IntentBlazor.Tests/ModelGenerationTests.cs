using System.Text.Json;
using IntentBlazor.AI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntentBlazor.Tests;

public sealed class ModelGenerationTests
{
    [Fact]
    public async Task ServiceTool_ProducesAnInteractiveNewsViewWithoutDomainRazor()
    {
        ScriptChatClient client = new(ModelTestSupport.Tool(), ModelTestSupport.Text(ModelTestSupport.Envelope()));
        await using ServiceProvider root = ModelTestSupport.Provider(client);
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        Assert.True((await session.SubmitAsync("Show gaming news in a list with details")).Success);
        Assert.Equal(2, client.Calls.Count);
        Assert.Contains(client.Calls[1].Messages.SelectMany(message => message.Contents), content => content is FunctionResultContent);
        Assert.Equal("get_gaming_news", Assert.Single(client.Calls[0].Options!.Tools!).Name);
        TestContent.Select(session, "headlines", "news-2");
        await using HtmlRenderer renderer = new(scope.ServiceProvider, root.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<IntentBlazor.Blazor.AiOutput>()).ToHtmlString());
        Assert.Contains("reserve unit", html);
        Assert.Contains("Community rating", html);
        ModelRunReport report = scope.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!;
        Assert.Equal(1, report.ToolCalls);
        Assert.Equal(2, report.ModelCalls);
        Assert.Equal("completed", report.Status);
        Assert.Null(report.InputTokens);
    }

    [Fact]
    public async Task ToolRegistration_AttributesAndExplicitMethodsShareMetadataAndInvocation()
    {
        ScriptChatClient client = new(ModelTestSupport.Tool("explicit_news"), ModelTestSupport.Text(ModelTestSupport.Envelope()));
        ServiceCollection services = new();
        services.AddSingleton(TestContent.Catalog);
        services.AddIntentBlazorAI().UseChatClient(_ => client, new("test", "test", null))
            .AddTool<NewsDataService>(nameof(NewsDataService.GetArticlesAsync), "explicit_news", "Read sample news");
        using ServiceProvider root = services.BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("news")).Success);
        AIFunction tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(client.Calls[0].Options!.Tools!));
        Assert.Equal("Read sample news", tool.Description);
        Assert.NotEqual(JsonValueKind.Undefined, tool.JsonSchema.ValueKind);
        Assert.Contains("properties", tool.JsonSchema.GetRawText());
        Assert.NotNull(tool.ReturnJsonSchema);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidProposal_IsCorrectedWithinTheConfiguredLimit(bool semanticError)
    {
        string invalid = semanticError ? JsonSerializer.Serialize(new
        { documentJson = OutputJson.SerializeDocument(TestContent.Catalog.Get("news").Document with { SchemaVersion = "99" }) }) : "invalid JSON";
        ScriptChatClient client = new(ModelTestSupport.Text(invalid), ModelTestSupport.Text(ModelTestSupport.Envelope()));
        using ServiceProvider root = ModelTestSupport.Provider(client, nativeTools: false);
        using IServiceScope scope = root.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("news")).Success);
        Assert.Equal(2, client.Calls.Count);
        Assert.Contains(client.Calls[1].Messages, message => message.Text.Contains("validator rejected"));
        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!.Corrections);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("forged-data")]
    [InlineData("unknown-tool")]
    [InlineData("oversized")]
    [InlineData("injected-host-data")]
    public async Task UntrustedModelOutput_CannotReplaceTheWorkingDocument(string mode)
    {
        ChatResponse response = mode switch
        {
            "unknown-tool" => ModelTestSupport.Tool("delete_business_records"),
            "forged-data" => ModelTestSupport.Text(ModelTestSupport.Envelope("assets")),
            "oversized" => ModelTestSupport.Text(new string('x', OutputJson.MaximumBytes + 1)),
            "injected-host-data" => ModelTestSupport.Text(JsonSerializer.Serialize(new
            { documentJson = OutputJson.SerializeDocument(TestContent.Catalog.Get("news").Document), data = TestContent.Catalog.Get("news").Data })),
            _ => ModelTestSupport.Text("not JSON")
        };
        ScriptChatClient client = new(response);
        using ServiceProvider root = ModelTestSupport.Provider(client, options => options.MaximumCorrections = 0);
        using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        session.Open(TestContent.Catalog.Get("news"));
        TestContent.Select(session, "headlines", "news-1");
        long revision = session.Revision;
        Assert.False((await session.SubmitAsync("new view")).Success);
        Assert.Equal(revision, session.Revision);
        Assert.Equal("news-reader", session.Document!.Id);
        Assert.Equal("news-1", TestContent.Value(session, "selectedArticle"));
        Assert.Single(client.Calls);
    }

    [Fact]
    public async Task ModelContext_ContainsSelectionAndDraftButRejectsDestructiveReplacement()
    {
        ScriptChatClient client = new(ModelTestSupport.Text(ModelTestSupport.Envelope()));
        using ServiceProvider root = ModelTestSupport.Provider(client, nativeTools: false);
        using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        session.Open(TestContent.Catalog.Get("news"));
        TestContent.Select(session, "headlines", "news-2");
        Assert.True((await session.SubmitAsync("use my selection")).Success);
        Assert.Contains("news-2", client.Calls[0].Messages.Single(message => message.Role == ChatRole.User).Text);
        session.Open(TestContent.Catalog.Get("staff"));
        TestContent.Select(session, "jobs", "job-101");
        session.Change("notesInput", "Keep this draft", session.Revision);
        Assert.False((await session.SubmitAsync("replace this")).Success);
        Assert.Equal(2, client.Calls.Count);
        Assert.Contains("Keep this draft", client.Calls[1].Messages.Single(message => message.Role == ChatRole.User).Text);
        Assert.Equal("Keep this draft", TestContent.Value(session, "notes"));
    }

    [Theory]
    [InlineData("model-calls")]
    [InlineData("context")]
    [InlineData("tokens")]
    public async Task ModelBudgets_StopFurtherWork(string budget)
    {
        ChatResponse response = budget == "model-calls" ? ModelTestSupport.Tool() : ModelTestSupport.Text(ModelTestSupport.Envelope());
        if (budget == "tokens") response.Usage = new() { InputTokenCount = 100, OutputTokenCount = 100 };
        ScriptChatClient client = new(response);
        using ServiceProvider root = ModelTestSupport.Provider(client, options =>
        {
            options.MaximumModelCalls = 1;
            if (budget == "context") options.MaximumContextCharacters = 1024;
            if (budget == "tokens") options.MaximumReportedTokens = 128;
        });
        using IServiceScope scope = root.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("news")).Success);
        Assert.Equal(budget == "context" ? 0 : 1, client.Calls.Count);
    }

    [Fact]
    public async Task Timeout_AndCancellationReleaseBusyStateWithoutReplacingOutput()
    {
        ScriptChatClient client = new(ModelTestSupport.Text(ModelTestSupport.Envelope()))
        { BeforeResponse = token => Task.Delay(Timeout.Infinite, token) };
        using ServiceProvider root = ModelTestSupport.Provider(client, options => options.Timeout = TimeSpan.FromMilliseconds(30));
        using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        session.Open(TestContent.Catalog.Get("news"));
        Assert.False((await session.SubmitAsync("news")).Success);
        Assert.False(session.IsBusy);
        Assert.Contains("timed out", session.Errors[0].Message);
        Assert.Equal("news-reader", session.Document!.Id);
        Assert.Equal("timeout", scope.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!.Status);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.False((await session.SubmitAsync("news", cancellation.Token)).Success);
        Assert.Equal("cancelled", session.Errors[0].Code);
    }

    [Fact]
    public void ToolRegistration_RejectsNonDatasetResultsAndDuplicateNames()
    {
        IntentModelBuilder builder = new ServiceCollection().AddIntentBlazorAI();
        Assert.Throws<ArgumentException>(() => builder.AddTool<InvalidTool>(nameof(InvalidTool.Save), "save", "Write"));
        builder.AddTools<NewsDataService>();
        Assert.Throws<ArgumentException>(() => builder.AddTools<NewsDataService>());
    }

    public sealed class InvalidTool { public string Save() => "saved"; }
}
