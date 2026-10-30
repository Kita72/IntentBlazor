using System.Collections.Immutable;
using IntentBlazor.Core;
using IntentBlazor.Runtime;

namespace IntentBlazor.Content;

public static class ArtifactData
{
    public static OutputDataSet Create(IWorkspaceContent content, IEnumerable<ContentArtifact> artifacts, string id = "artifacts") => new()
    {
        Id = id,
        KeyField = "id",
        Fields = new Dictionary<string, DataField>
        {
            ["id"] = new() { Kind = StateKind.Text },
            ["name"] = new() { Kind = StateKind.Text },
            ["mediaType"] = new() { Kind = StateKind.Text },
            ["version"] = new() { Kind = StateKind.Text },
            ["previewUrl"] = new() { Kind = StateKind.Text },
            ["width"] = new() { Kind = StateKind.Number, Nullable = true },
            ["height"] = new() { Kind = StateKind.Number, Nullable = true },
            ["bytes"] = new() { Kind = StateKind.Number },
            ["isImage"] = new() { Kind = StateKind.Boolean }
        }.ToImmutableDictionary(),
        Rows = artifacts.Select(artifact => new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["id"] = JsonValues.From(artifact.Reference.Id),
            ["name"] = JsonValues.From(artifact.Name),
            ["mediaType"] = JsonValues.From(artifact.Reference.MediaType),
            ["version"] = JsonValues.From(artifact.Reference.Version),
            ["previewUrl"] = JsonValues.From(artifact.Reference.MediaType.StartsWith("image/", StringComparison.Ordinal) ? content.Url(artifact.Reference) : ""),
            ["width"] = JsonValues.From(artifact.Width),
            ["height"] = JsonValues.From(artifact.Height),
            ["bytes"] = JsonValues.From(artifact.Bytes),
            ["isImage"] = JsonValues.From(artifact.Reference.MediaType is "image/png" or "image/jpeg")
        }.ToImmutableDictionary()).ToImmutableArray()
    };
}
