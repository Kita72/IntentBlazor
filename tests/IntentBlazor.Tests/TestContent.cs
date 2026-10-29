using System.Collections.Immutable;
using System.Text.Json;

namespace IntentBlazor.Tests;

internal static class TestContent
{
    public static ExampleCatalog Catalog { get; } = new();
    public static AiSession Session(IInputProcessor? processor = null) => new(new DocumentValidator(), processor ?? new ExampleInputProcessor(Catalog));
    public static OutputContent ChangeNode(OutputContent content, string id, Func<OutputNode, OutputNode> update) =>
        content with { Document = content.Document with { Nodes = content.Document.Nodes.Select(node => node.Id == id ? update(node) : node).ToImmutableArray() } };

    public static void Select(AiSession session, string node, string key) =>
        Assert.True(session.Dispatch(new(node, UiEventKind.Select, session.Revision) { Value = JsonValues.From(key) }).Success);

    public static string Value(AiSession session, string key) => JsonValues.Display(session.Snapshot.Values[key]);

    public static OutputContent Tree(int depth) => new()
    {
        Document = new()
        {
            Id = "depth-example",
            RootId = "n0",
            Nodes = Enumerable.Range(0, depth + 1).Select(index => new OutputNode
            {
                Id = $"n{index}",
                Kind = index == depth ? NodeKind.Text : NodeKind.Layout,
                Children = index == depth ? [] : [$"n{index + 1}"],
                Value = index == depth ? ValueBinding.Constant("leaf") : null
            }).ToImmutableArray()
        }
    };

    public static OutputContent WithRows(OutputContent content, ImmutableArray<ImmutableDictionary<string, JsonElement>> rows) =>
        content with { Data = content.Data.SetItem(0, content.Data[0] with { Rows = rows }) };
}

internal sealed class DelegateInputProcessor(Func<string, WorkspaceSnapshot, CancellationToken, ValueTask<OutputContent>> handler) : IInputProcessor
{
    public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken cancellationToken) =>
        handler(input, workspace, cancellationToken);
}
