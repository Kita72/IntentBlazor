using System.Collections.Immutable;
using IntentBlazor.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

public sealed class ModelLimitTests
{
    [Theory]
    [InlineData("timeout")]
    [InlineData("calls")]
    [InlineData("tool-calls")]
    [InlineData("corrections")]
    [InlineData("context")]
    [InlineData("output-tokens")]
    [InlineData("total-tokens")]
    [InlineData("temperature")]
    public async Task Configuration_RejectsInvalidLimitsBeforeInference(string setting)
    {
        ScriptChatClient client = new(ModelTestSupport.Text("unused"));
        using ServiceProvider root = ModelTestSupport.Provider(client, options =>
        {
            switch (setting)
            {
                case "timeout": options.Timeout = TimeSpan.Zero; break;
                case "calls": options.MaximumModelCalls = 0; break;
                case "tool-calls": options.MaximumToolCalls = 0; break;
                case "corrections": options.MaximumCorrections = 3; break;
                case "context": options.MaximumContextCharacters = 262145; break;
                case "output-tokens": options.MaximumOutputTokens = 127; break;
                case "total-tokens": options.MaximumReportedTokens = 127; break;
                default: options.Temperature = float.NaN; break;
            }
        });
        using IServiceScope scope = root.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("Hi")).Success);
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task ToolCallLimit_CannotBeBypassedByAnotherModelRound()
    {
        ScriptChatClient client = new(ModelTestSupport.Tool(), ModelTestSupport.Tool());
        using ServiceProvider root = ModelTestSupport.Provider(client, options => options.MaximumToolCalls = 1);
        using IServiceScope scope = root.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("news")).Success);
        Assert.Equal(2, client.Calls.Count);
        ModelRunReport report = scope.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!;
        Assert.Equal(1, report.ToolCalls);
        Assert.Equal(2, report.ModelCalls);
    }

    [Fact]
    public async Task JsonModeFallback_StillValidatesDocumentsAndReportsActualUsage()
    {
        ChatResponse response = ModelTestSupport.Text(ModelTestSupport.Envelope());
        response.Usage = new() { InputTokenCount = 37, OutputTokenCount = 19 };
        ScriptChatClient client = new(response);
        using ServiceProvider root = ModelTestSupport.Provider(client, options => options.UseJsonSchema = false, nativeTools: false);
        using IServiceScope scope = root.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("news")).Success);
        Assert.IsType<ChatResponseFormatJson>(client.Calls[0].Options!.ResponseFormat);
        ModelRunReport report = scope.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!;
        Assert.Equal(37, report.InputTokens);
        Assert.Equal(19, report.OutputTokens);
        Assert.True(report.Characters > 0);
    }

    [Fact]
    public async Task CodexProfile_PreloadsAuthorizedDataWithoutPassingNativeTools()
    {
        ScriptChatClient client = new(ModelTestSupport.Text(ModelTestSupport.Envelope()));
        using ServiceProvider root = ModelTestSupport.Provider(client, nativeTools: false);
        using IServiceScope scope = root.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("news")).Success);
        Assert.Empty(client.Calls[0].Options!.Tools!);
        Assert.Contains("Fictional fixture:", client.Calls[0].Messages.Single(message => message.Role == ChatRole.User).Text);
        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!.ToolCalls);
    }

    [Fact]
    public async Task DataSources_AreScopedAndNeverAcceptModelSuppliedRows()
    {
        ScriptChatClient client = new(ModelTestSupport.Text(ModelTestSupport.Envelope()));
        ServiceCollection services = new();
        services.AddIntentBlazorAI().UseChatClient(_ => client, new("test", "test", null)).AddDataSource<ScopedNewsSource>();
        using ServiceProvider root = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope one = root.CreateScope();
        using IServiceScope two = root.CreateScope();
        AiSession first = one.ServiceProvider.GetRequiredService<AiSession>();
        AiSession second = two.ServiceProvider.GetRequiredService<AiSession>();
        Assert.True((await first.SubmitAsync("news")).Success);
        Assert.True((await second.SubmitAsync("news")).Success);
        string firstPrompt = client.Calls[0].Messages.Single(message => message.Role == ChatRole.User).Text;
        string secondPrompt = client.Calls[1].Messages.Single(message => message.Role == ChatRole.User).Text;
        Assert.NotEqual(firstPrompt, secondPrompt);
        string firstId = Assert.IsType<ScopedNewsSource>(Assert.Single(one.ServiceProvider.GetServices<IOutputDataSource>())).ScopeId;
        string secondId = Assert.IsType<ScopedNewsSource>(Assert.Single(two.ServiceProvider.GetServices<IOutputDataSource>())).ScopeId;
        Assert.Contains(firstId, firstPrompt);
        Assert.Contains(secondId, secondPrompt);
        Assert.DoesNotContain(firstId, secondPrompt);
        Assert.DoesNotContain(secondId, firstPrompt);
        Assert.NotSame(first, second);
        TestContent.Select(first, "headlines", "news-1");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, second.Snapshot.Values["selectedArticle"].ValueKind);
    }

    public sealed class ScopedNewsSource : IOutputDataSource
    {
        public string ScopeId { get; } = Guid.NewGuid().ToString("N");
        public ValueTask<ImmutableArray<OutputDataSet>> LoadAsync(WorkspaceSnapshot workspace, CancellationToken cancellationToken)
        {
            OutputDataSet data = TestContent.Catalog.Get("news").Data[0];
            return ValueTask.FromResult<ImmutableArray<OutputDataSet>>([data with
            { Rows = data.Rows.SetItem(0, data.Rows[0].SetItem("body", JsonValues.From(ScopeId))) }]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UsageBudget_HandlesTotalOnlyAndRejectsNegativeCounts(bool negative)
    {
        ChatResponse response = ModelTestSupport.Text(ModelTestSupport.Envelope());
        response.Usage = negative ? new() { InputTokenCount = -1 } : new() { TotalTokenCount = 129 };
        ScriptChatClient client = new(response);
        using ServiceProvider root = ModelTestSupport.Provider(client, options => options.MaximumReportedTokens = 128, nativeTools: false);
        using IServiceScope scope = root.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("news")).Success);
        Assert.Single(client.Calls);
        Assert.Equal(negative ? null : 129L, scope.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!.TotalTokens);
    }
}
