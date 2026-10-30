using System.Collections.Immutable;
using IntentBlazor.Core;
using IntentBlazor.ExampleHost;
using IntentBlazor.Runtime;

namespace IntentBlazor.GamingNews;

public sealed class NewsScenario(INewsSource source, NewsLibrary library, IExampleActor actor) : IExampleScenario
{
    public string InitialRequest => "Show recent updates and version-aware tips. Choose two articles to compare, then save the comparison.";
    public async ValueTask<ImmutableArray<OutputDataSet>> LoadAsync(WorkspaceSnapshot workspace, CancellationToken token)
    {
        string owner = (await actor.ReadAsync(token))?.Id ?? throw new InputRejectedException("Sign in to open a private news workspace.");
        var current = await source.ReadAsync(token);
        var snapshots = library.Comparisons(owner).SelectMany(item => new[] { item.First, item.Second }).Concat(library.Bookmarks(owner));
        var articles = current.Concat(snapshots).DistinctBy(item => item.Id).ToImmutableArray();
        string? preferred = ExampleData.Selected(workspace.Values, "preferredGame");
        var ordered = articles.OrderByDescending(item => item.Game == preferred).ThenByDescending(item => item.PublishedUtc);
        var games = ExampleData.TextSet("games", new[] { ExampleData.Row(("id", "all"), ("name", "All games")) }
            .Concat(articles.Select(item => item.Game).Distinct().Order().Select(game => ExampleData.Row(("id", game), ("name", game)))), "id", "name");
        return [NewsLibrary.Articles(ordered), games, NewsLibrary.Articles(library.Bookmarks(owner), "bookmarks"), library.Saved(owner)];
    }
    public async ValueTask<WorkspaceAccess> ReauthorizeAsync(string owner, ImmutableArray<string> ids, ImmutableArray<ArtifactReference> artifacts, CancellationToken token)
    {
        if ((await actor.ReadAsync(token))?.Id != owner || !artifacts.IsEmpty || ids.Any(id => id is not ("articles" or "games" or "bookmarks" or "saved"))) return new(false, []);
        return new(true, (await LoadAsync(ExampleData.EmptyWorkspace, token)).Where(set => ids.Contains(set.Id)).ToImmutableArray());
    }
    public async ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken token)
    {
        var data = await LoadAsync(workspace, token); string? selected = ExampleData.Selected(workspace.Values, "selectedArticle");
        string message = selected is null ? input : $"Follow-up for {selected}: {input}. Your current article and comparison are retained.";
        return NewsDocuments.Create(data, message);
    }
}
public sealed class BookmarkArticleAction(INewsSource source, NewsLibrary library, NewsScenario scenario, IExampleActor actor) : IWorkspaceActionHandler
{
    public static ActionDefinition Definition { get; } = new("bookmark_article", "Bookmark selected article", "articles", ImmutableDictionary<string, StateDefinition>.Empty);
    public ValueTask<bool> AuthorizeAsync(ActionContext context, CancellationToken token) => NewsAction.Authorize(actor, context, token);
    public ValueTask<ActionResult> ExecuteAsync(ActionContext context, CancellationToken token) => NewsAction.Save(source, library, scenario, actor, context, false, token);
}
public sealed class SaveComparisonAction(INewsSource source, NewsLibrary library, NewsScenario scenario, IExampleActor actor) : IWorkspaceActionHandler
{
    public static ActionDefinition Definition { get; } = new("save_comparison", "Save comparison", "articles", ImmutableDictionary<string, StateDefinition>.Empty
        .Add("secondArticle", new() { Kind = StateKind.Text }).Add("secondVersion", new() { Kind = StateKind.Text }));
    public ValueTask<bool> AuthorizeAsync(ActionContext context, CancellationToken token) => NewsAction.Authorize(actor, context, token);
    public ValueTask<ActionResult> ExecuteAsync(ActionContext context, CancellationToken token) => NewsAction.Save(source, library, scenario, actor, context, true, token);
}
internal static class NewsAction
{
    public static async ValueTask<bool> Authorize(IExampleActor actor, ActionContext context, CancellationToken token) =>
        await actor.ReadAsync(token) is { } current && current.Id == context.OwnerId;
    public static async ValueTask<ActionResult> Save(INewsSource source, NewsLibrary library, NewsScenario scenario, IExampleActor actor,
        ActionContext context, bool comparison, CancellationToken token)
    {
        if (!await Authorize(actor, context, token)) return new(ActionStatus.Rejected, "The private news library is unavailable.");
        var articles = await source.ReadAsync(token); GameArticle? first = articles.FirstOrDefault(item => item.Id == context.ResourceId);
        if (first is null || first.Revision != context.ExpectedVersion) return new(ActionStatus.Conflict, "The selected article changed. Refresh and review its current source/version before saving.");
        GameArticle? second = null;
        if (comparison)
        {
            string? id = ExampleData.Selected(context.Arguments, "secondArticle"), version = ExampleData.Selected(context.Arguments, "secondVersion");
            second = articles.FirstOrDefault(item => item.Id == id);
            if (second is null || second.Id == first.Id) return new(ActionStatus.Rejected, "Choose a different second article to compare.");
            if (second.Revision != version) return new(ActionStatus.Conflict, "The second article changed. Refresh and review before saving.");
        }
        if (!await Authorize(actor, context, token)) return new(ActionStatus.Rejected, "The news identity changed.");
        token.ThrowIfCancellationRequested();
        try { library.Save(context.OwnerId, context.IdempotencyKey, first, second); }
        catch (InputRejectedException error) { return new(ActionStatus.Rejected, error.Message); }
        return new(ActionStatus.Succeeded, comparison ? "The attributed comparison was saved in your private library." : "The article was bookmarked in your private library.")
        {
            AcceptedStates = comparison ? ["comparisonArticle"] : [],
            Data = await scenario.LoadAsync(ExampleData.EmptyWorkspace, token)
        };
    }
}
