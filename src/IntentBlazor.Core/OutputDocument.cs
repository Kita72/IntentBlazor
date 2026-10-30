using System.Collections.Immutable;
using System.Text.Json;

namespace IntentBlazor.Core;

public sealed record OutputDocument
{
    public string SchemaVersion { get; init; } = "0.1";
    public string Id { get; init; } = "";
    public long Revision { get; init; } = 1;
    public string RootId { get; init; } = "";
    public ImmutableDictionary<string, string> Requires { get; init; } =
        ImmutableDictionary<string, string>.Empty.Add("builtin", "0.1");
    public ImmutableArray<string> DataSets { get; init; } = [];
    public ImmutableDictionary<string, StateDefinition> State { get; init; } = ImmutableDictionary<string, StateDefinition>.Empty;
    public ImmutableArray<OutputNode> Nodes { get; init; } = [];
    public ImmutableArray<OutputInteraction> Interactions { get; init; } = [];
}

public enum NodeKind { Layout, Card, Text, List, Table, Rating, Input, Select, Button, Image, Form, Markdown, Chart, Gallery, Artifact, Extension }
public enum StateKind { Text, Number, Boolean, RecordReference }
public enum BindingSource { Literal, State, SelectedField, EventValue, EventField }
public enum UiEventKind { Select, Change, Activate, Sort }

public sealed record StateDefinition
{
    public StateKind Kind { get; init; }
    public JsonElement InitialValue { get; init; } = JsonValues.Null;
    public bool Nullable { get; init; }
    public string? DataSetId { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
}

public sealed record ValueBinding
{
    public BindingSource Source { get; init; }
    public JsonElement Literal { get; init; } = JsonValues.Null;
    public string? StateKey { get; init; }
    public string? DataSetId { get; init; }
    public string? Field { get; init; }

    public static ValueBinding Constant<T>(T value) => new() { Literal = JsonValues.From(value) };
    public static ValueBinding FromState(string key) => new() { Source = BindingSource.State, StateKey = key };
    public static ValueBinding Selected(string key, string dataSet, string field) =>
        new() { Source = BindingSource.SelectedField, StateKey = key, DataSetId = dataSet, Field = field };
    public static ValueBinding EventValue() => new() { Source = BindingSource.EventValue };
    public static ValueBinding EventField(string dataSet, string field) =>
        new() { Source = BindingSource.EventField, DataSetId = dataSet, Field = field };
}

public sealed record OutputNode
{
    public string Id { get; init; } = "";
    public NodeKind Kind { get; init; }
    public string Title { get; init; } = "";
    public string? WhenState { get; init; }
    public ImmutableArray<string> Children { get; init; } = [];
    public bool Columns { get; init; }
    public ValueBinding? Value { get; init; }
    public string? DataSetId { get; init; }
    public string? LabelField { get; init; }
    public string? SelectionState { get; init; }
    public string? InteractionId { get; init; }
    public string? StateKey { get; init; }
    public ImmutableArray<TableColumn> Fields { get; init; } = [];
    public int RatingMaximum { get; init; } = 5;
    public string? ImageField { get; init; }
    public ChartDefinition? Chart { get; init; }
    public ExtensionNode? Extension { get; init; }
}

public enum ChartKind { Bar, Line }
public sealed record ChartDefinition(ChartKind Kind, string LabelField, string ValueField);
public sealed record ExtensionNode(string Pack, string Version, string Renderer, JsonElement Configuration, string Fallback);

public sealed record TableColumn(string Field, string Label);

public sealed record OutputInteraction
{
    public string Id { get; init; } = "";
    public UiEventKind Event { get; init; }
    public ImmutableArray<StateAssignment> Assignments { get; init; } = [];
    public bool ClearDrafts { get; init; }
    public string? Action { get; init; }
    public ValueBinding? Resource { get; init; }
    public ValueBinding? ExpectedVersion { get; init; }
    public ImmutableDictionary<string, ValueBinding> Arguments { get; init; } = ImmutableDictionary<string, ValueBinding>.Empty;
}

public sealed record ActionDefinition(string Name, string Label, string DataSetId,
    ImmutableDictionary<string, StateDefinition> Parameters);

public sealed record StateAssignment(string StateKey, ValueBinding Value);

public sealed record DataField
{
    public StateKind Kind { get; init; }
    public bool Nullable { get; init; }
}

public sealed record OutputDataSet
{
    public string Id { get; init; } = "";
    public string KeyField { get; init; } = "";
    public ImmutableDictionary<string, DataField> Fields { get; init; } = ImmutableDictionary<string, DataField>.Empty;
    public ImmutableArray<ImmutableDictionary<string, JsonElement>> Rows { get; init; } = [];
}

// Host-owned data travels separately from the presentation document.
public sealed record OutputContent
{
    public OutputDocument Document { get; init; } = new();
    public ImmutableArray<OutputDataSet> Data { get; init; } = [];
}

public sealed record DocumentError(string Code, string Path, string Message);

public sealed record OperationResult(bool Success, ImmutableArray<DocumentError> Errors)
{
    public static OperationResult Ok { get; } = new(true, []);
    public static OperationResult Fail(string code, string message) => new(false, [new(code, "", message)]);
}

public static class JsonValues
{
    public static JsonElement Null { get; } = JsonSerializer.SerializeToElement<object?>(null);
    public static JsonElement From<T>(T value) => JsonSerializer.SerializeToElement(value, OutputJson.Options);
    public static string Display(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        JsonValueKind.String => value.GetString() ?? "",
        _ => value.GetRawText()
    };
}

public static class ImageSourcePolicy
{
    public static bool IsAllowed(string? value) =>
        value is { Length: > 1 and <= 1024 } &&
        value[0] == '/' && value[1] != '/' &&
        !value.Contains('\\') && !value.Any(char.IsControl);
}
