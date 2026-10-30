using System.Collections.Immutable;
using IntentBlazor.Core;
using IntentBlazor.ExampleHost;

namespace IntentBlazor.GamingNews;

public static class NewsDocuments
{
    public static OutputContent Create(ImmutableArray<OutputDataSet> data, string message) => new()
    {
        Data = data,
        Document = new()
        {
            SchemaVersion = "0.3",
            Id = "gaming-news",
            RootId = "root",
            DataSets = data.Select(set => set.Id).ToImmutableArray(),
            Requires = ImmutableDictionary<string, string>.Empty.Add("builtin", "0.1").Add("news", "1.0"),
            State = ImmutableDictionary<string, StateDefinition>.Empty.Add("preferredGame", ExampleData.Reference("games", false, "all"))
                .Add("selectedArticle", ExampleData.Reference("articles")).Add("comparisonArticle", ExampleData.Reference("articles"))
                .Add("selectedBookmark", ExampleData.Reference("bookmarks")).Add("savedComparison", ExampleData.Reference("saved")),
            Nodes = [
                new() { Id = "root", Kind = NodeKind.Layout, Children = ["notice", "preference", "articles", "comparisonPick", "comparison", "bookmarks", "bookmarkCard", "saved", "savedCard", "clear"] },
                new() { Id = "notice", Kind = NodeKind.Markdown, Title = "News workspace", Value = ValueBinding.Constant(message) },
                new() { Id = "preference", Kind = NodeKind.Select, Title = "Prioritize game on the next request", StateKey = "preferredGame", DataSetId = "games", LabelField = "name" },
                new() { Id = "articles", Kind = NodeKind.Table, Title = "Recent updates", DataSetId = "articles", SelectionState = "selectedArticle", InteractionId = "chooseArticle",
                    Fields = [new("title", "Update"), new("game", "Game"), new("published", "Published UTC"), new("gameVersion", "Applies to version"), new("source", "Source")] },
                new() { Id = "comparisonPick", Kind = NodeKind.Select, Title = "Compare with article", StateKey = "comparisonArticle", DataSetId = "articles", LabelField = "title" },
                new() { Id = "comparison", Kind = NodeKind.Card, Title = "Current comparison", WhenState = "selectedArticle", Children = ["comparisonRows", "sources", "bookmark", "saveComparison"] },
                new() { Id = "comparisonRows", Kind = NodeKind.Layout, Columns = true, Children = ["primaryCard", "otherCard"] },
                new() { Id = "primaryCard", Kind = NodeKind.Card, Title = "Selected update", Children = ["primaryTitle", "primarySummary", "primaryTip", "primaryVersion"] },
                new() { Id = "otherCard", Kind = NodeKind.Card, Title = "Compared update", WhenState = "comparisonArticle", Children = ["otherTitle", "otherSummary", "otherTip", "otherVersion"] },
                ExampleData.Text("primaryTitle", "Selected article", ValueBinding.Selected("selectedArticle", "articles", "title")),
                ExampleData.Text("primarySummary", "Update", ValueBinding.Selected("selectedArticle", "articles", "summary")),
                ExampleData.Text("primaryTip", "Version-aware tip", ValueBinding.Selected("selectedArticle", "articles", "tip")),
                ExampleData.Text("primaryVersion", "Primary version", ValueBinding.Selected("selectedArticle", "articles", "gameVersion")),
                ExampleData.Text("otherTitle", "Comparison article", ValueBinding.Selected("comparisonArticle", "articles", "title")),
                ExampleData.Text("otherSummary", "Comparison update", ValueBinding.Selected("comparisonArticle", "articles", "summary")),
                ExampleData.Text("otherTip", "Comparison tip", ValueBinding.Selected("comparisonArticle", "articles", "tip")),
                ExampleData.Text("otherVersion", "Comparison version", ValueBinding.Selected("comparisonArticle", "articles", "gameVersion")),
                new() { Id = "sources", Kind = NodeKind.Extension, Title = "Article sources", Extension = new("news", "1.0", "citations", JsonValues.From(new NewsCitationsConfiguration("articles", ["selectedArticle", "comparisonArticle"])), "Source links are available in the host article data.") },
                ExampleData.Button("bookmark", "Bookmark", "bookmark"),
                ExampleData.Button("saveComparison", "Save comparison", "saveComparison") with { WhenState = "comparisonArticle" },
                new() { Id = "bookmarks", Kind = NodeKind.Table, Title = "My bookmarks", DataSetId = "bookmarks", SelectionState = "selectedBookmark", InteractionId = "chooseBookmark", Fields = [new("title", "Article"), new("published", "Published UTC"), new("gameVersion", "Version")] },
                new() { Id = "bookmarkCard", Kind = NodeKind.Card, Title = "Bookmarked update", WhenState = "selectedBookmark", Children = ["bookmarkTitle", "bookmarkSummary", "bookmarkTip", "bookmarkSources"] },
                ExampleData.Text("bookmarkTitle", "Article", ValueBinding.Selected("selectedBookmark", "bookmarks", "title")),
                ExampleData.Text("bookmarkSummary", "Update", ValueBinding.Selected("selectedBookmark", "bookmarks", "summary")),
                ExampleData.Text("bookmarkTip", "Version-aware tip", ValueBinding.Selected("selectedBookmark", "bookmarks", "tip")),
                new() { Id = "bookmarkSources", Kind = NodeKind.Extension, Title = "Bookmarked source", Extension = new("news", "1.0", "citations", JsonValues.From(new NewsCitationsConfiguration("bookmarks", ["selectedBookmark"])), "Source attribution remains in the bookmarked article data.") },
                new() { Id = "saved", Kind = NodeKind.List, Title = "My saved comparisons", DataSetId = "saved", LabelField = "title", SelectionState = "savedComparison", InteractionId = "chooseSaved" },
                new() { Id = "savedCard", Kind = NodeKind.Card, Title = "Saved comparison snapshot", WhenState = "savedComparison", Children = ["savedText", "savedSources"] },
                new() { Id = "savedText", Kind = NodeKind.Markdown, Title = "Original facts and versions", Value = ValueBinding.Selected("savedComparison", "saved", "details") },
                new() { Id = "savedSources", Kind = NodeKind.Extension, Title = "Saved source attribution", Extension = new("news", "1.0", "citations", JsonValues.From(new NewsCitationsConfiguration("saved", ["savedComparison"])), "Saved source URLs remain in the comparison data.") },
                ExampleData.Button("clear", "Clear local preferences and comparison draft", "clear")
            ],
            Interactions = [
                new() { Id = "chooseArticle", Event = UiEventKind.Select, Assignments = [new("selectedArticle", ValueBinding.EventValue())] },
                new() { Id = "chooseBookmark", Event = UiEventKind.Select, Assignments = [new("selectedBookmark", ValueBinding.EventValue())] },
                new() { Id = "chooseSaved", Event = UiEventKind.Select, Assignments = [new("savedComparison", ValueBinding.EventValue())] },
                new() { Id = "bookmark", Event = UiEventKind.Activate, Action = "bookmark_article", Resource = ValueBinding.FromState("selectedArticle"), ExpectedVersion = ValueBinding.Selected("selectedArticle", "articles", "revision") },
                new() { Id = "saveComparison", Event = UiEventKind.Activate, Action = "save_comparison", Resource = ValueBinding.FromState("selectedArticle"), ExpectedVersion = ValueBinding.Selected("selectedArticle", "articles", "revision"),
                    Arguments = ImmutableDictionary<string, ValueBinding>.Empty.Add("secondArticle", ValueBinding.FromState("comparisonArticle")).Add("secondVersion", ValueBinding.Selected("comparisonArticle", "articles", "revision")) },
                new() { Id = "clear", Event = UiEventKind.Activate, ClearDrafts = true }
            ]
        }
    };
}
