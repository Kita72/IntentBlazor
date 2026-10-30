using System.Collections.Immutable;
using IntentBlazor.Core;

namespace IntentBlazor.Runtime;

public sealed record ContentLimits(long MaximumBytes = 8 * 1024 * 1024, int MaximumFiles = 8,
    int MaximumTextCharacters = 16000, int MaximumPixels = 16000000, int MaximumPdfPages = 20);
public sealed record ContentFile(string Name, string MediaType, long Length);
public sealed record ContentArtifact(ArtifactReference Reference, string Name, long Bytes, DateTimeOffset CreatedAt,
    string Provenance, ArtifactReference? Parent = null, int? Width = null, int? Height = null, string? Text = null,
    bool TextTruncated = false, OutputDataSet? StructuredData = null);
public sealed record ContentCapability(string MediaType, string Extension, bool Preview, bool ExtractText, bool Edit, bool Export);

// Optional seam consumed by AiInput/AiOutput and model context, without adding parser dependencies to the renderer.
public interface IWorkspaceContent
{
    ContentLimits Limits { get; }
    ImmutableArray<ContentCapability> Capabilities { get; }
    ValueTask<ContentArtifact> ImportAsync(ContentFile file, Stream source, CancellationToken cancellationToken = default);
    ValueTask<ContentArtifact?> DescribeAsync(ArtifactReference reference, CancellationToken cancellationToken = default);
    string Url(ArtifactReference reference, bool download = false);
}
