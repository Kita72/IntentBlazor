using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using IntentBlazor.Core;
using IntentBlazor.Runtime;
using SkiaSharp;

namespace IntentBlazor.Content;

public sealed class WorkspaceContent(IWorkspaceIdentity identity, IArtifactStore store, IEnumerable<IContentAdapter> adapters,
    ContentLimits limits, TimeProvider clock, ContentDeliveryOptions delivery) : IWorkspaceContent
{
    public ContentLimits Limits { get; } = limits;
    public ImmutableArray<ContentCapability> Capabilities { get; } = adapters.SelectMany(adapter => adapter.Capabilities).ToImmutableArray();
    public async ValueTask<ContentArtifact> ImportAsync(ContentFile file, Stream source, CancellationToken cancellationToken = default)
    {
        string owner = await Owner(cancellationToken);
        string name = FileName(file.Name), extension = Path.GetExtension(name).ToLowerInvariant();
        ContentCapability? capability = Capabilities.FirstOrDefault(item => item.Extension == extension);
        if (capability is null || (file.MediaType != "application/octet-stream" && file.MediaType.Length > 0 && file.MediaType != capability.MediaType))
            throw new ContentRejectedException("The file extension and media type are not supported together.");
        if (file.Length is < 1 || file.Length > Limits.MaximumBytes) throw new ContentRejectedException("The file exceeds the configured upload size.");
        using MemoryStream buffer = new(); byte[] chunk = new byte[65536]; int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (buffer.Length + read > Limits.MaximumBytes || buffer.Length + read > file.Length) throw new ContentRejectedException("The streamed file exceeds its declared or configured size.");
            buffer.Write(chunk, 0, read);
        }
        if (buffer.Length != file.Length) throw new ContentRejectedException("The streamed file length does not match the declaration.");
        return await Store(owner, name, capability.MediaType, buffer.ToArray(), "User upload", null, null, cancellationToken);
    }
    public async ValueTask<ContentArtifact?> DescribeAsync(ArtifactReference reference, CancellationToken cancellationToken = default)
    {
        string owner = await Owner(cancellationToken);
        ContentArtifact? artifact = await store.DescribeAsync(owner, reference, cancellationToken);
        return await Owner(cancellationToken) == owner ? artifact : null;
    }
    public string Url(ArtifactReference reference, bool download = false) =>
        $"{delivery.RoutePrefix}/{Uri.EscapeDataString(reference.Id)}/{Uri.EscapeDataString(reference.Version)}?download={(download ? "true" : "false")}&type={Uri.EscapeDataString(reference.MediaType)}";

    public async ValueTask<ContentArtifact> ResizeAsync(ArtifactReference original, int width, int height, string operationId, CancellationToken token = default)
    {
        string owner = await Owner(token);
        ContentArtifact source = await store.DescribeAsync(owner, original, token) ?? throw new ContentRejectedException("The source image is unavailable.");
        if (source.Reference.MediaType is not ("image/png" or "image/jpeg")) throw new ContentRejectedException("Only PNG and JPEG images can be resized.");
        ImageContentAdapter.CheckDimensions(width, height, Limits);
        byte[] bytes = await store.ReadAsync(owner, original, token) ?? throw new ContentRejectedException("The source image is unavailable.");
        using SKBitmap bitmap = SKBitmap.Decode(bytes);
        using SKBitmap resized = bitmap.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKFilterMode.Nearest)) ?? throw new ContentRejectedException("The resize operation failed.");
        using SKImage image = SKImage.FromBitmap(resized); using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        string id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner + ":resize:" + operationId)))[..32].ToLowerInvariant();
        return await Store(owner, Path.GetFileNameWithoutExtension(source.Name) + $"-{width}x{height}.png", "image/png", encoded.ToArray(),
            $"Nearest-neighbor resize to {width}x{height}", original, id, token);
    }
    public async ValueTask<ContentArtifact> ExportAsync(OutputDataSet data, string format, string name, CancellationToken token = default)
    {
        string owner = await Owner(token);
        var export = ContentExports.Write(data, format);
        return await Store(owner, FileName(name) + export.Extension, export.MediaType, export.Bytes, "Export from validated application dataset", null, null, token);
    }
    private async ValueTask<ContentArtifact> Store(string owner, string name, string mediaType, byte[] bytes, string provenance,
        ArtifactReference? parent, string? id, CancellationToken token)
    {
        if (bytes.Length > Limits.MaximumBytes) throw new ContentRejectedException("The generated content exceeds the configured byte limit.");
        ContentAnalysis analysis;
        try { analysis = adapters.First(adapter => adapter.Capabilities.Any(item => item.MediaType == mediaType)).Read(bytes, mediaType, Limits, token); }
        catch (Exception error) when (error is not OperationCanceledException and not ContentRejectedException and not OutOfMemoryException)
        { throw new ContentRejectedException("The content is malformed or cannot be processed within the supported format."); }
        if (await Owner(token) != owner) throw new ContentRejectedException("The identity changed while the content was processed.");
        ArtifactReference reference = new(id ?? Guid.NewGuid().ToString("N"), mediaType, Convert.ToHexString(SHA256.HashData(bytes)));
        OutputDataSet? data = analysis.StructuredData is null ? null : analysis.StructuredData with { Id = "content_" + reference.Id };
        if (data is not null)
        {
            OutputContent probe = new()
            {
                Data = [data],
                Document = new()
                {
                    Id = "content-probe",
                    RootId = "root",
                    DataSets = [data.Id],
                    Nodes = [new() { Id = "root", Kind = NodeKind.Text, Value = ValueBinding.Constant("Content preview") }]
                }
            };
            if (!new DocumentValidator().Validate(probe).Success) data = null;
        }
        ContentArtifact artifact = new(reference, FileName(name), bytes.Length, clock.GetUtcNow(), provenance, parent,
            analysis.Width, analysis.Height, analysis.Text, analysis.TextTruncated, data);
        return await store.PutAsync(owner, artifact, bytes, token);
    }
    private async ValueTask<string> Owner(CancellationToken token) =>
        await identity.GetOwnerIdAsync(token) is { Length: > 0 } owner ? owner : throw new ContentRejectedException("An authenticated content owner is required.");
    private static string FileName(string name)
    {
        string file = Path.GetFileName(name.Replace('\\', '/'));
        if (file.Length is < 1 or > 128 || file.Any(char.IsControl)) throw new ContentRejectedException("Use a bounded file name without control characters.");
        return file;
    }
}
