using System.Collections.Immutable;
using IntentBlazor.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

public sealed class Phase7DataRefreshTests
{
    [Fact]
    public async Task RegisteredReads_RefreshPreloadedAndEarlierResultsWithoutDuplicateDatasets()
    {
        ScriptChatClient client = new(Call("first"), Call("second"), ModelTestSupport.Text(ModelTestSupport.Envelope()));
        using ServiceProvider root = Provider(client);
        using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        Assert.True((await session.SubmitAsync("Read the latest authorized news twice")).Success);
        Assert.Equal("Refreshed 2", Assert.Single(session.Data).Rows[0]["title"].GetString());
        Assert.Equal(2, scope.ServiceProvider.GetRequiredService<RefreshData>().Calls);
        ModelRunReport report = scope.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!;
        Assert.Equal(2, report.ToolCalls);
        Assert.Equal(2, report.Tools.Length);
        Assert.All(report.Tools, tool => Assert.Equal("completed", tool.Outcome));
    }

    [Fact]
    public async Task InvalidRefresh_DoesNotPoisonPreviouslyAuthorizedData()
    {
        ScriptChatClient client = new(Call("bad"), ModelTestSupport.Text(ModelTestSupport.Envelope()));
        using ServiceProvider root = Provider(client);
        using IServiceScope scope = root.CreateScope();
        RefreshData service = scope.ServiceProvider.GetRequiredService<RefreshData>(); service.Invalid = true;
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        session.Open(TestContent.Catalog.Get("news") with { Data = [RefreshData.Initial()] });
        await session.SubmitAsync("Try the invalid refresh");
        Assert.Equal(2, Assert.Single(session.Data).Rows.Length);
        Assert.Equal("Initial preload", session.Data[0].Rows[0]["title"].GetString());
        Assert.Equal("failed", Assert.Single(scope.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!.Tools).Outcome);
        Assert.True(new DocumentValidator().Validate(new() { Document = session.Document!, Data = session.Data }).Success);
    }

    [Fact]
    public async Task IndependentPreloadSources_StillRejectDuplicateDatasetIdsBeforeCallingAModel()
    {
        ScriptChatClient client = new(ModelTestSupport.Text(ModelTestSupport.Envelope()));
        using ServiceProvider root = Provider(client, duplicateSources: true);
        using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>(); session.Open(TestContent.Catalog.Get("assets"));
        Assert.False((await session.SubmitAsync("Read ambiguous preloads")).Success);
        Assert.Contains("unique dataset IDs", Assert.Single(session.Errors).Message);
        Assert.Empty(client.Calls);
        Assert.Equal("asset-gallery", session.Document!.Id);
    }

    private static ChatResponse Call(string id) => new(new ChatMessage(ChatRole.Assistant,
        [new FunctionCallContent(id, "refresh_news", new Dictionary<string, object?>())]));
    private static ServiceProvider Provider(ScriptChatClient client, bool duplicateSources = false)
    {
        ServiceCollection services = new();
        var builder = services.AddIntentBlazorAI(options => options.MaximumCorrections = 0)
            .UseChatClient(_ => client, new("scripted", "test", null)).AddTools<RefreshData>();
        services.AddScoped<IOutputDataSource>(sp => sp.GetRequiredService<RefreshData>());
        if (duplicateSources) services.AddScoped<IOutputDataSource>(sp => sp.GetRequiredService<RefreshData>());
        return services.BuildServiceProvider(validateScopes: true);
    }

    public sealed class RefreshData : IOutputDataSource
    {
        public bool Invalid { get; set; }
        public int Calls { get; private set; }
        public static OutputDataSet Initial()
        {
            OutputDataSet source = TestContent.Catalog.Get("news").Data[0];
            return source with { Rows = source.Rows.SetItem(0, source.Rows[0].SetItem("title", JsonValues.From("Initial preload"))) };
        }
        [AiDataTool("refresh_news", "Read the latest authorized news dataset.")]
        public OutputDataSet Read()
        {
            Calls++; OutputDataSet source = Initial();
            return Invalid ? source with { Rows = source.Rows.Add(source.Rows[0]) } :
                source with { Rows = source.Rows.SetItem(0, source.Rows[0].SetItem("title", JsonValues.From("Refreshed " + Calls))) };
        }
        public ValueTask<ImmutableArray<OutputDataSet>> LoadAsync(WorkspaceSnapshot workspace, CancellationToken token) =>
            ValueTask.FromResult<ImmutableArray<OutputDataSet>>([Initial()]);
    }
}
