using System.Collections.Immutable;
using IntentBlazor.Core;
using IntentBlazor.Runtime;

namespace IntentBlazor.Quickstart;

// Every input opens this authored document. This is not a simulated model response.
public sealed class CatalogInputProcessor(CatalogService catalog) : IInputProcessor
{
    public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new OutputContent
        {
            Data = [catalog.Read()],
            Document = new()
            {
                SchemaVersion = "0.3",
                Id = "catalog",
                RootId = "root",
                DataSets = ["items"],
                Requires = ImmutableDictionary<string, string>.Empty.Add("builtin", "0.1").Add("sample", "1.0"),
                State = ImmutableDictionary<string, StateDefinition>.Empty
                    .Add("selected", new() { Kind = StateKind.RecordReference, DataSetId = "items", Nullable = true })
                    .Add("score", new() { Kind = StateKind.Number, InitialValue = JsonValues.From(3), Minimum = 0, Maximum = 10 }),
                Nodes =
                [
                    new() { Id = "root", Kind = NodeKind.Layout, Children = ["itemsList", "detail", "scoreEditor", "scoreValue", "clear"] },
                    new() { Id = "itemsList", Kind = NodeKind.List, Title = "Synthetic catalog", DataSetId = "items",
                        LabelField = "name", SelectionState = "selected", InteractionId = "choose" },
                    new() { Id = "detail", Kind = NodeKind.Text, Title = "Description", WhenState = "selected",
                        Value = ValueBinding.Selected("selected", "items", "description") },
                    new() { Id = "scoreEditor", Kind = NodeKind.Extension, Title = "Local score",
                        Extension = new("sample", "1.0", "score", JsonValues.From(new ScoreConfiguration("score")), "Score editor unavailable.") },
                    new() { Id = "scoreValue", Kind = NodeKind.Rating, Title = "Your score", RatingMaximum = 10, Value = ValueBinding.FromState("score") },
                    new() { Id = "clear", Kind = NodeKind.Button, Title = "Clear score draft", InteractionId = "clearDraft" }
                ],
                Interactions =
                [
                    new() { Id = "choose", Event = UiEventKind.Select, Assignments = [new("selected", ValueBinding.EventValue())] },
                    new() { Id = "clearDraft", Event = UiEventKind.Activate, ClearDrafts = true }
                ]
            }
        });
    }
}
