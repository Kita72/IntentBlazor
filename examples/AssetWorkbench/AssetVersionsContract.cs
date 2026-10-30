using System.Collections.Immutable;
using IntentBlazor.Core;

namespace IntentBlazor.AssetWorkbench;

public sealed record AssetVersionsConfiguration(string OriginalState, string PreviewState, string ComparisonState, string ImageState);
public sealed record AssetVersionEvent(string OriginalId, string VersionId, bool IsImage);
public sealed class AssetVersionsContract : OutputExtensionContract<AssetVersionsConfiguration, AssetVersionEvent>
{
    public override ExtensionDescriptor Descriptor { get; } = new("assets", "1.0", "versions", "Select an owned artifact version and its retained original for the image-editor pack.", JsonValues.From(new AssetVersionsConfiguration("selectedArtifact", "previewArtifact", "showOriginal", "isImage")));
    public override OperationResult Validate(AssetVersionsConfiguration configuration, OutputContent content)
    {
        var state = content.Document.State;
        return new[] { configuration.OriginalState, configuration.PreviewState, configuration.ComparisonState, configuration.ImageState }.Distinct().Count() == 4 &&
            state.TryGetValue(configuration.OriginalState, out var original) && original.Kind == StateKind.RecordReference &&
            state.TryGetValue(configuration.PreviewState, out var preview) && preview.Kind == StateKind.RecordReference && original.DataSetId == preview.DataSetId &&
            state.TryGetValue(configuration.ComparisonState, out var comparison) && comparison.Kind == StateKind.Boolean &&
            state.TryGetValue(configuration.ImageState, out var image) && image.Kind == StateKind.Boolean
                ? OperationResult.Ok : OperationResult.Fail("asset-version-state", "Use two compatible artifact selections and two distinct boolean states.");
    }
    public override ImmutableArray<string> EditableStates(AssetVersionsConfiguration configuration) => [configuration.OriginalState, configuration.PreviewState, configuration.ComparisonState, configuration.ImageState];
    public override ImmutableArray<ExtensionEffect> Reduce(AssetVersionsConfiguration configuration, AssetVersionEvent message, ExtensionState state) =>
        [new(configuration.OriginalState, JsonValues.From(message.OriginalId)), new(configuration.PreviewState, JsonValues.From(message.VersionId)),
            new(configuration.ComparisonState, JsonValues.From(false)), new(configuration.ImageState, JsonValues.From(message.IsImage))];
}
