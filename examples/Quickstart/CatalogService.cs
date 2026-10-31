using System.Collections.Immutable;
using IntentBlazor.AI;
using IntentBlazor.Core;
using IntentBlazor.Runtime;

namespace IntentBlazor.Quickstart;

// Synthetic, public data. Replace this boundary with your own authorized query.
public sealed class CatalogService : IOutputDataSource
{
    [AiDataTool("catalog_items", "Read the synthetic item catalog.")]
    public OutputDataSet Read(string filter = "") => new()
    {
        Id = "items",
        KeyField = "id",
        Fields = new Dictionary<string, DataField>
        {
            ["id"] = new() { Kind = StateKind.Text },
            ["name"] = new() { Kind = StateKind.Text },
            ["description"] = new() { Kind = StateKind.Text }
        }.ToImmutableDictionary(),
        Rows = new[]
        {
            Row("lantern", "Brass lantern", "A portable light for the expedition."),
            Row("map", "Trail map", "A guide to the northern passes.")
        }.Where(row => JsonValues.Display(row["name"]).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToImmutableArray()
    };

    public ValueTask<ImmutableArray<OutputDataSet>> LoadAsync(WorkspaceSnapshot workspace, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ImmutableArray<OutputDataSet>>([Read()]);
    }

    private static ImmutableDictionary<string, System.Text.Json.JsonElement> Row(string id, string name, string description) =>
        new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["id"] = JsonValues.From(id),
            ["name"] = JsonValues.From(name),
            ["description"] = JsonValues.From(description)
        }.ToImmutableDictionary();
}
