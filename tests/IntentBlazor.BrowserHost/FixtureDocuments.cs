using System.Collections.Immutable;
using IntentBlazor.Core;

namespace IntentBlazor.BrowserHost;

public static class FixtureDocuments
{
    public static OutputContent Create(string notice = "Initial offline fixture") => new()
    {
        Data = [new()
        {
            Id = "jobs", KeyField = "id",
            Fields = ImmutableDictionary<string, DataField>.Empty.Add("id", new() { Kind = StateKind.Text })
                .Add("title", new() { Kind = StateKind.Text }).Add("days", new() { Kind = StateKind.Number })
                .Add("reference", new() { Kind = StateKind.Text }),
            Rows = new[] { ("one", "First synthetic order waiting for custom materials", 12), ("two", "Second synthetic order", 2), ("three", "Third synthetic order", 7) }
                .Select(row => ImmutableDictionary<string, System.Text.Json.JsonElement>.Empty.Add("id", JsonValues.From(row.Item1))
                    .Add("title", JsonValues.From(row.Item2)).Add("days", JsonValues.From(row.Item3))
                    .Add("reference", JsonValues.From("FixtureReferenceABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"))).ToImmutableArray()
        }],
        Document = new()
        {
            SchemaVersion = "0.3",
            Id = "browser-fixture",
            RootId = "root",
            DataSets = ["jobs"],
            State = ImmutableDictionary<string, StateDefinition>.Empty
                .Add("selected", new() { Kind = StateKind.RecordReference, DataSetId = "jobs", Nullable = true })
                .Add("notes", new() { Kind = StateKind.Text, InitialValue = JsonValues.From("") }),
            Nodes = [
                new() { Id = "root", Kind = NodeKind.Layout, Children = ["notice", "jobs", "editor", "chart"] },
                new() { Id = "notice", Kind = NodeKind.Text, Title = "Fixture result", Value = ValueBinding.Constant(notice) },
                new() { Id = "jobs", Kind = NodeKind.Table, Title = "Fixture jobs", DataSetId = "jobs", SelectionState = "selected", InteractionId = "select", Fields = [new("title", "Job"), new("days", "Days waiting"), new("reference", "Reference")] },
                new() { Id = "editor", Kind = NodeKind.Form, Title = "Fixture draft", WhenState = "selected", Children = ["selectedTitle", "notes"] },
                new() { Id = "selectedTitle", Kind = NodeKind.Text, Title = "Selected job", Value = ValueBinding.Selected("selected", "jobs", "title") },
                new() { Id = "notes", Kind = NodeKind.Input, Title = "Fixture notes", StateKey = "notes" },
                new() { Id = "chart", Kind = NodeKind.Chart, Title = "Fixture waiting days", DataSetId = "jobs", Chart = new(ChartKind.Bar, "title", "days") }
            ],
            Interactions = [new() { Id = "select", Event = UiEventKind.Select, Assignments = [new("selected", ValueBinding.EventValue())] }]
        }
    };
}
