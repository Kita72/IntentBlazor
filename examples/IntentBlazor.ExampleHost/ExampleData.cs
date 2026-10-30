using System.Collections.Immutable;
using System.Text.Json;
using IntentBlazor.Core;
using IntentBlazor.Runtime;

namespace IntentBlazor.ExampleHost;

public static class ExampleData
{
    public static WorkspaceSnapshot EmptyWorkspace { get; } = new(null, 0, ImmutableDictionary<string, JsonElement>.Empty, ImmutableDictionary<string, SortState>.Empty, []);
    public static ImmutableDictionary<string, JsonElement> Row(params (string Key, object? Value)[] cells) =>
        cells.ToImmutableDictionary(cell => cell.Key, cell => JsonValues.From(cell.Value), StringComparer.Ordinal);
    public static OutputDataSet TextSet(string id, IEnumerable<ImmutableDictionary<string, JsonElement>> rows, params string[] fields) => new()
    {
        Id = id,
        KeyField = "id",
        Rows = rows.ToImmutableArray(),
        Fields = fields.ToImmutableDictionary(name => name, _ => new DataField { Kind = StateKind.Text })
    };
    public static StateDefinition Reference(string data, bool nullable = true, string? initial = null) =>
        new() { Kind = StateKind.RecordReference, DataSetId = data, Nullable = nullable, InitialValue = JsonValues.From(initial) };
    public static string? Selected(ImmutableDictionary<string, JsonElement> values, string key) => values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    public static OutputNode Text(string id, string title, ValueBinding value) => new() { Id = id, Kind = NodeKind.Text, Title = title, Value = value };
    public static OutputNode Button(string id, string title, string interaction) => new() { Id = id, Kind = NodeKind.Button, Title = title, InteractionId = interaction };
}
