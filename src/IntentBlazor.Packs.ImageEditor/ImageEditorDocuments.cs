using System.Collections.Immutable;
using IntentBlazor.Core;
namespace IntentBlazor.Packs.ImageEditor;

public static class ImageEditorDocuments
{
    public static OutputContent Create(OutputDataSet artifacts, string message = "Deterministic content inspection; no model calls.")
    {
        var state = ImmutableDictionary<string, StateDefinition>.Empty
            .Add("selectedArtifact", new() { Kind = StateKind.RecordReference, DataSetId = artifacts.Id, Nullable = true })
            .Add("previewArtifact", new() { Kind = StateKind.RecordReference, DataSetId = artifacts.Id, Nullable = true })
            .Add("showOriginal", new() { Kind = StateKind.Boolean, InitialValue = JsonValues.From(false) })
            .Add("isImage", new() { Kind = StateKind.Boolean, InitialValue = JsonValues.From(false) })
            .Add("targetWidth", new() { Kind = StateKind.Number, InitialValue = JsonValues.From(32), Minimum = 1, Maximum = 4096 })
            .Add("targetHeight", new() { Kind = StateKind.Number, InitialValue = JsonValues.From(32), Minimum = 1, Maximum = 4096 });
        return new()
        {
            Data = [artifacts],
            Document = new()
            {
                SchemaVersion = "0.3",
                Id = "content-workspace",
                RootId = "root",
                Requires = ImmutableDictionary<string, string>.Empty.Add("builtin", "0.1").Add("images", "0.1"),
                DataSets = [artifacts.Id],
                State = state,
                Nodes = [
                    new() { Id = "root", Kind = NodeKind.Layout, Children = ["intro", "files", "sizes", "preview", "editor"] },
                    new() { Id = "intro", Kind = NodeKind.Markdown, Title = "Content workspace", Value = ValueBinding.Constant(message) },
                    new() { Id = "files", Kind = NodeKind.Gallery, Title = "Attachments", DataSetId = artifacts.Id, LabelField = "name", ImageField = "previewUrl", SelectionState = "selectedArtifact", InteractionId = "chooseFile" },
                    new() { Id = "sizes", Kind = NodeKind.Chart, Title = "Attachment sizes in bytes", DataSetId = artifacts.Id, Chart = new(ChartKind.Bar, "name", "bytes") },
                    new() { Id = "preview", Kind = NodeKind.Artifact, Title = "Selected artifact", WhenState = "selectedArtifact", Value = ValueBinding.FromState("previewArtifact") },
                    new() { Id = "editor", Kind = NodeKind.Extension, Title = "Image preview and resize", WhenState = "isImage", Children = ["width", "height", "resize", "clearDimensions"],
                        Extension = new("images", "0.1", "editor", JsonValues.From(new ImageEditorConfiguration("selectedArtifact", "previewArtifact", "showOriginal")), "The image editor pack is unavailable; download the original to work with it.") },
                    new() { Id = "width", Kind = NodeKind.Input, Title = "Target width", StateKey = "targetWidth" },
                    new() { Id = "height", Kind = NodeKind.Input, Title = "Target height", StateKey = "targetHeight" },
                    new() { Id = "resize", Kind = NodeKind.Button, Title = "Resize", InteractionId = "resize" },
                    new() { Id = "clearDimensions", Kind = NodeKind.Button, Title = "Clear dimension draft", InteractionId = "clear" }
                ],
                Interactions = [
                    new() { Id = "chooseFile", Event = UiEventKind.Select, Assignments = [new("selectedArtifact", ValueBinding.EventValue()), new("previewArtifact", ValueBinding.EventValue()),
                        new("isImage", ValueBinding.EventField(artifacts.Id, "isImage")), new("showOriginal", ValueBinding.Constant(false)),
                        new("targetWidth", ValueBinding.Constant(32)), new("targetHeight", ValueBinding.Constant(32))] },
                    new() { Id = "resize", Event = UiEventKind.Activate, Action = "image_resize", Resource = ValueBinding.FromState("selectedArtifact"), ExpectedVersion = ValueBinding.Selected("selectedArtifact", artifacts.Id, "version"),
                        Arguments = ImmutableDictionary<string, ValueBinding>.Empty.Add("width", ValueBinding.FromState("targetWidth")).Add("height", ValueBinding.FromState("targetHeight")) },
                    new() { Id = "clear", Event = UiEventKind.Activate, ClearDrafts = true }
                ]
            }
        };
    }
}
