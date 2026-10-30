using System.Collections.Immutable;
using IntentBlazor.Blazor;
using IntentBlazor.Core;
using IntentBlazor.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Packs.ImageEditor;

public sealed record ImageEditorConfiguration(string OriginalState, string PreviewState, string ComparisonState);
public sealed record ImageEditorEvent(bool ShowOriginal);
public sealed class ImageEditorContract : OutputExtensionContract<ImageEditorConfiguration, ImageEditorEvent>
{
    public override ExtensionDescriptor Descriptor { get; } = new("images", "0.1", "editor",
        "Image comparison and local zoom; compose ordinary input and registered resize-action children.",
        JsonValues.From(new ImageEditorConfiguration("selectedArtifact", "previewArtifact", "showOriginal")));
    public override OperationResult Validate(ImageEditorConfiguration configuration, OutputContent content)
    {
        var state = content.Document.State;
        return state.TryGetValue(configuration.OriginalState, out StateDefinition? original) && original.Kind == StateKind.RecordReference &&
            state.TryGetValue(configuration.PreviewState, out StateDefinition? preview) && preview.Kind == StateKind.RecordReference && original.DataSetId == preview.DataSetId &&
            state.TryGetValue(configuration.ComparisonState, out StateDefinition? comparison) && comparison.Kind == StateKind.Boolean &&
            new[] { configuration.OriginalState, configuration.PreviewState, configuration.ComparisonState }.Distinct().Count() == 3
            ? OperationResult.Ok : OperationResult.Fail("image-state", "Bind original and preview selections to one dataset and comparison to a distinct boolean state.");
    }
    public override ImmutableArray<string> EditableStates(ImageEditorConfiguration configuration) => [configuration.ComparisonState];
    public override ImmutableArray<ExtensionEffect> Reduce(ImageEditorConfiguration configuration, ImageEditorEvent message, ExtensionState state) =>
        [new(configuration.ComparisonState, JsonValues.From(message.ShowOriginal))];
}
public sealed record ImageEditorOptions(string DataSetId = "artifacts", string PreviewState = "previewArtifact",
    string WidthState = "targetWidth", string HeightState = "targetHeight");
public static class ImageEditorRegistration
{
    public static IServiceCollection AddIntentImageEditor(this IServiceCollection services, ImageEditorOptions? options = null)
    {
        options ??= new(); services.AddSingleton(options);
        services.AddIntentRenderer<ImageEditorContract, ImageEditorView>();
        services.AddIntentAction<ImageResizeAction>(new("image_resize", "Resize preview", options.DataSetId,
            ImmutableDictionary<string, StateDefinition>.Empty.Add("width", new() { Kind = StateKind.Number, Minimum = 1, Maximum = 4096 })
                .Add("height", new() { Kind = StateKind.Number, Minimum = 1, Maximum = 4096 })));
        return services;
    }
}
