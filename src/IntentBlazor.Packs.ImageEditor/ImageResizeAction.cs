using System.Collections.Immutable;
using IntentBlazor.Content;
using IntentBlazor.Core;
using IntentBlazor.Runtime;

namespace IntentBlazor.Packs.ImageEditor;

public sealed class ImageResizeAction(WorkspaceContent content, IArtifactStore store, IWorkspaceIdentity identity, ImageEditorOptions options) : IWorkspaceActionHandler
{
    public async ValueTask<bool> AuthorizeAsync(ActionContext context, CancellationToken cancellationToken) =>
        await identity.GetOwnerIdAsync(cancellationToken) == context.OwnerId && context.Artifacts.Any(item => item.Id == context.ResourceId && item.Version == context.ExpectedVersion &&
            item.MediaType is "image/png" or "image/jpeg");
    public async ValueTask<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        if (!await AuthorizeAsync(context, cancellationToken)) return new(ActionStatus.Rejected, "The image operation was denied.");
        if (!context.Arguments.TryGetValue("width", out var width) || !width.TryGetInt32(out int w) || w is < 1 or > 4096 ||
            !context.Arguments.TryGetValue("height", out var height) || !height.TryGetInt32(out int h) || h is < 1 or > 4096)
            return new(ActionStatus.Rejected, "Use integer image dimensions from 1 to 4096.");
        try
        {
            ArtifactReference original = context.Artifacts.Single(item => item.Id == context.ResourceId);
            if (await store.DescribeAsync(context.OwnerId, original, cancellationToken) is null) return new(ActionStatus.Rejected, "The original image is unavailable.");
            ContentArtifact derived = await content.ResizeAsync(original, w, h, context.IdempotencyKey, cancellationToken);
            OutputDataSet row = ArtifactData.Create(content, [derived], options.DataSetId);
            var data = context.Data.Select(set => set.Id == options.DataSetId ? set with { Rows = set.Rows.Where(value => value[set.KeyField].GetString() != derived.Reference.Id).Append(row.Rows[0]).ToImmutableArray() } : set).ToImmutableArray();
            return new(ActionStatus.Succeeded, "The resized PNG preview was created. The original is retained.")
            {
                Data = data,
                Artifacts = [derived.Reference],
                Values = ImmutableDictionary<string, System.Text.Json.JsonElement>.Empty.Add(options.PreviewState, JsonValues.From(derived.Reference.Id))
                    .Add(options.WidthState, JsonValues.From(w)).Add(options.HeightState, JsonValues.From(h)),
                AcceptedStates = [options.WidthState, options.HeightState]
            };
        }
        catch (ContentRejectedException) { return new(ActionStatus.Rejected, "The image is unavailable or exceeds the configured processing limits."); }
    }
}
