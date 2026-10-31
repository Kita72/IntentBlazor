using IntentBlazor.Core;
using IntentBlazor.Docs.Components.Documentation;
using IntentBlazor.Docs.Documentation;
using IntentBlazor.Examples;
using IntentBlazor.Runtime;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntentBlazor.Tests;

public sealed class Phase6ExplorerTests
{
    [Fact]
    public async Task PrerenderedExplorer_DisablesControlsUntilInteractiveHandlersAreReady()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddIntentBlazor();
        services.AddSingleton<ExampleCatalog>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        await using HtmlRenderer renderer = new(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            string html = (await renderer.RenderComponentAsync<DocumentExplorerView>()).ToHtmlString();
            Assert.Contains("<fieldset class=\"explorer-controls\" disabled", html);
            Assert.Contains("Open staff fixture", html);
            Assert.Contains("Connecting", html);
        });
    }

    [Theory]
    [InlineData("news")]
    [InlineData("staff")]
    [InlineData("assets")]
    public void Fixtures_RenderWithOnlyTheirHostDataAndInspectableTrees(string fixture)
    {
        using DocumentExplorer explorer = new(new ExampleCatalog());
        Assert.True(explorer.OpenFixture(fixture).Success);
        Assert.Equal(fixture, explorer.Fixture);
        Assert.Equal(explorer.Session.Document!.DataSets, explorer.Session.Data.Select(set => set.Id));
        Assert.Equal(explorer.Session.Document.Nodes.Length, explorer.Tree().Length);
        Assert.Equal(0, explorer.Tree()[0].Depth);
        Assert.Equal(explorer.Session.Document.RootId, explorer.Tree()[0].Node.Id);
        Assert.Contains(explorer.Session.Document.RootId, explorer.NodeJson(explorer.Session.Document.RootId));
        Assert.Contains("Revision", explorer.StateJson);
        Assert.False(explorer.HasJsonDraft);
    }

    [Fact]
    public void InvalidProposals_RetainOutputDataStateRevisionAndEditableJson()
    {
        using DocumentExplorer explorer = new(new ExampleCatalog());
        AiSession session = explorer.Session;
        session.Dispatch(new("headlines", UiEventKind.Select, session.Revision) { Value = JsonValues.From("news-1") });
        WorkspaceSnapshot before = session.Snapshot;
        string data = explorer.DataJson;
        Assert.False(explorer.TryInvalidSchema().Success);
        Assert.Equal("schema-version", Assert.Single(explorer.EditorResult.Errors).Code);
        Assert.Same(before.Document, session.Document);
        Assert.Equal(before.Revision, session.Revision);
        Assert.Equal(before.Values, session.Snapshot.Values);
        Assert.Equal(data, explorer.DataJson);
        Assert.Contains("99", explorer.JsonDraft);
        explorer.JsonDraft = "{\"unexpected\":true}";
        Assert.False(explorer.Render().Success);
        Assert.Equal("invalid-json", Assert.Single(explorer.EditorResult.Errors).Code);
        Assert.Equal(before.Revision, session.Revision);
    }

    [Fact]
    public void JsonDraft_SurvivesLocalEventsAndBlocksFixtureReplacementUntilExplicitRestore()
    {
        using DocumentExplorer explorer = new(new ExampleCatalog());
        string pending = explorer.JsonDraft + "\n ";
        explorer.JsonDraft = pending;
        Assert.True(explorer.Session.Dispatch(new("headlines", UiEventKind.Select, explorer.Session.Revision) { Value = JsonValues.From("news-2") }).Success);
        Assert.Equal(pending, explorer.JsonDraft);
        long revision = explorer.Session.Revision;
        Assert.False(explorer.OpenFixture("staff").Success);
        Assert.Equal("editor-dirty", Assert.Single(explorer.EditorResult.Errors).Code);
        Assert.Equal(revision, explorer.Session.Revision);
        explorer.RestoreEditor();
        Assert.False(explorer.HasJsonDraft);
        Assert.True(explorer.OpenFixture("staff").Success);
        Assert.Equal("staff", explorer.Fixture);
    }

    [Fact]
    public async Task JsonDraft_BlocksInputCommandsAndValidPresentationCannotIntroduceHostData()
    {
        using DocumentExplorer explorer = new(new ExampleCatalog());
        explorer.JsonDraft += " ";
        long revision = explorer.Session.Revision;
        Assert.False((await explorer.Session.SubmitAsync("staff")).Success);
        Assert.Equal(revision, explorer.Session.Revision);
        explorer.RestoreEditor();
        explorer.JsonDraft = OutputJson.SerializeDocument(explorer.Session.Document! with { DataSets = ["private_records"] });
        Assert.False(explorer.Render().Success);
        Assert.Equal(revision, explorer.Session.Revision);
        Assert.DoesNotContain(explorer.Session.Data, set => set.Id == "private_records");
    }

    [Fact]
    public void OutputDraft_SurvivesRenderAndFixtureSwitchAttempts()
    {
        using DocumentExplorer explorer = new(new ExampleCatalog());
        explorer.OpenFixture("staff");
        Assert.True(explorer.Session.Dispatch(new("jobs", UiEventKind.Select, explorer.Session.Revision) { Value = JsonValues.From("job-101") }).Success);
        Assert.True(explorer.Session.Change("notesInput", "Keep my local draft", explorer.Session.Revision).Success);
        long revision = explorer.Session.Revision;
        Assert.False(explorer.OpenFixture("assets").Success);
        Assert.False(explorer.Render().Success);
        Assert.Equal("dirty-state", Assert.Single(explorer.EditorResult.Errors).Code);
        Assert.Equal("Keep my local draft", explorer.Session.Snapshot.Values["notes"].GetString());
        Assert.Equal(revision, explorer.Session.Revision);
        Assert.True(explorer.Session.HasDraft);
    }

    [Fact]
    public async Task ValidEdits_RenderWithoutSharingStateAcrossExplorers()
    {
        ExampleCatalog catalog = new();
        using DocumentExplorer first = new(catalog);
        using DocumentExplorer second = new(catalog);
        first.JsonDraft = OutputJson.SerializeDocument(first.Session.Document! with { Id = "editedNews" });
        Assert.True(first.Render().Success);
        Assert.Equal("editedNews", first.Session.Document!.Id);
        Assert.NotEqual(first.Session.Document.Id, second.Session.Document!.Id);
        Assert.False(first.HasJsonDraft);
        Assert.False(first.OpenFixture("unknown").Success);
        Assert.Equal("editedNews", first.Session.Document.Id);
        Assert.True((await first.Session.SubmitAsync("assets")).Success);
        Assert.True(first.EditorResult.Success);
        Assert.Equal("assets", first.Fixture);
    }
}
