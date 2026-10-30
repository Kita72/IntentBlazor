using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using IntentBlazor.Runtime;

namespace IntentBlazor.Content;

public sealed record ArtifactStorageOptions(string RootPath, long MaximumOwnerBytes = 64 * 1024 * 1024, int MaximumArtifacts = 128);
public interface IArtifactStore
{
    ValueTask<ContentArtifact> PutAsync(string owner, ContentArtifact artifact, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);
    ValueTask<ContentArtifact?> DescribeAsync(string owner, ArtifactReference reference, CancellationToken cancellationToken = default);
    ValueTask<byte[]?> ReadAsync(string owner, ArtifactReference reference, CancellationToken cancellationToken = default);
    ValueTask<ImmutableArray<ContentArtifact>> ListAsync(string owner, CancellationToken cancellationToken = default);
    ValueTask<int> CleanupAsync(string owner, DateTimeOffset before, IReadOnlyCollection<ArtifactReference> retained, CancellationToken cancellationToken = default);
}

// A single-instance file adapter; alternative stores implement the same ownership and immutable-write contract.
public sealed class FileArtifactStore : IArtifactStore
{
    private readonly ArtifactStorageOptions _options;
    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public FileArtifactStore(ArtifactStorageOptions options)
    {
        if (options.MaximumOwnerBytes is < 1 or > 1024L * 1024 * 1024 || options.MaximumArtifacts is < 1 or > 10000)
            throw new ArgumentException("Artifact storage quotas are outside supported bounds.");
        _root = Path.GetFullPath(options.RootPath); _options = options;
        Directory.CreateDirectory(_root); CheckDirectory(_root);
    }
    public async ValueTask<ContentArtifact> PutAsync(string owner, ContentArtifact artifact, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        string directory = Address(owner, artifact.Reference);
        if (artifact.Bytes != bytes.Length || artifact.Reference.Version != Convert.ToHexString(SHA256.HashData(bytes.Span)))
            throw new ContentRejectedException("Artifact metadata does not match its bytes.");
        string metadata = WorkspaceJson.Write(artifact);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ContentArtifact? existing = await DescribeAsync(owner, artifact.Reference, cancellationToken);
            if (existing is not null) return existing;
            ImmutableArray<ContentArtifact> all = await ListAsync(owner, cancellationToken);
            if (all.Length >= _options.MaximumArtifacts || all.Sum(item => item.Bytes) + bytes.Length > _options.MaximumOwnerBytes)
                throw new ContentRejectedException("The owner's artifact storage quota is full.");
            Directory.CreateDirectory(directory); CheckDirectory(directory);
            string blob = Path.Combine(directory, "content.bin"), manifest = Path.Combine(directory, "metadata.json");
            string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await File.WriteAllBytesAsync(temporary, bytes.ToArray(), cancellationToken);
                File.Move(temporary, blob, overwrite: true);
                await File.WriteAllTextAsync(temporary, metadata, cancellationToken);
                File.Move(temporary, manifest, overwrite: false);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return artifact;
        }
        finally { _gate.Release(); }
    }
    public async ValueTask<ContentArtifact?> DescribeAsync(string owner, ArtifactReference reference, CancellationToken cancellationToken = default)
    {
        string directory = Address(owner, reference), file = Path.Combine(directory, "metadata.json");
        if (!File.Exists(file)) return null;
        CheckDirectory(directory);
        if (new FileInfo(file).Length > WorkspaceJson.MaximumBytes) throw new ContentRejectedException("Artifact metadata is oversized.");
        ContentArtifact artifact = WorkspaceJson.Read<ContentArtifact>(await File.ReadAllTextAsync(file, cancellationToken));
        if (artifact.Reference != reference || artifact.Bytes < 0 || artifact.Bytes > _options.MaximumOwnerBytes)
            throw new ContentRejectedException("Artifact metadata is inconsistent.");
        return artifact;
    }
    public async ValueTask<byte[]?> ReadAsync(string owner, ArtifactReference reference, CancellationToken cancellationToken = default)
    {
        ContentArtifact? artifact = await DescribeAsync(owner, reference, cancellationToken);
        if (artifact is null) return null;
        string blob = Path.Combine(Address(owner, reference), "content.bin");
        if (!File.Exists(blob) || new FileInfo(blob).Length != artifact.Bytes) throw new ContentRejectedException("Artifact content is unavailable.");
        byte[] bytes = await File.ReadAllBytesAsync(blob, cancellationToken);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != reference.Version) throw new ContentRejectedException("Artifact content failed its integrity check.");
        return bytes;
    }
    public async ValueTask<ImmutableArray<ContentArtifact>> ListAsync(string owner, CancellationToken cancellationToken = default)
    {
        string directory = OwnerDirectory(owner); if (!Directory.Exists(directory)) return [];
        CheckDirectory(directory); var artifacts = ImmutableArray.CreateBuilder<ContentArtifact>();
        foreach (string entry in Directory.EnumerateDirectories(directory).Take(_options.MaximumArtifacts + 1))
        {
            CheckDirectory(entry);
            foreach (string version in Directory.EnumerateDirectories(entry).Take(_options.MaximumArtifacts + 1))
            {
                cancellationToken.ThrowIfCancellationRequested(); CheckDirectory(version);
                string file = Path.Combine(version, "metadata.json");
                if (!File.Exists(file)) continue;
                if (new FileInfo(file).Length > WorkspaceJson.MaximumBytes) throw new ContentRejectedException("Artifact metadata is oversized.");
                ContentArtifact artifact = WorkspaceJson.Read<ContentArtifact>(await File.ReadAllTextAsync(file, cancellationToken));
                if (Address(owner, artifact.Reference) != version) throw new ContentRejectedException("Artifact metadata has an invalid address.");
                artifacts.Add(artifact);
                if (artifacts.Count > _options.MaximumArtifacts) throw new ContentRejectedException("The owner's artifact count exceeds its configured quota.");
            }
        }
        return artifacts.ToImmutable();
    }
    public async ValueTask<int> CleanupAsync(string owner, DateTimeOffset before, IReadOnlyCollection<ArtifactReference> retained, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            int removed = 0;
            ImmutableArray<ContentArtifact> artifacts = await ListAsync(owner, cancellationToken);
            HashSet<ArtifactReference> keep = new(retained);
            Queue<ArtifactReference> pending = new(retained);
            while (pending.TryDequeue(out ArtifactReference? reference))
                if (artifacts.FirstOrDefault(item => item.Reference == reference)?.Parent is { } parent && keep.Add(parent)) pending.Enqueue(parent);
            foreach (ContentArtifact artifact in artifacts)
                if (artifact.CreatedAt < before && !keep.Contains(artifact.Reference))
                {
                    string directory = Address(owner, artifact.Reference); CheckDirectory(directory);
                    if (!directory.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new ContentRejectedException("Invalid cleanup path.");
                    Directory.Delete(directory, recursive: true); removed++;
                    string parent = Path.GetDirectoryName(directory)!;
                    if (!Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
                }
            return removed;
        }
        finally { _gate.Release(); }
    }
    private string OwnerDirectory(string owner)
    {
        WorkspaceJson.CheckAddress(owner, "artifacts");
        return Path.Combine(_root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner))));
    }
    private string Address(string owner, ArtifactReference reference)
    {
        if (!Guid.TryParseExact(reference.Id, "N", out _) || reference.Version.Length != 64 || !reference.Version.All(char.IsAsciiHexDigit) ||
            reference.MediaType.Length is < 1 or > 128) throw new ContentRejectedException("Invalid artifact reference.");
        string directory = Path.Combine(OwnerDirectory(owner), reference.Id, reference.Version);
        for (string? current = directory; current is not null && current.StartsWith(_root, StringComparison.Ordinal); current = Path.GetDirectoryName(current))
            if (Directory.Exists(current)) CheckDirectory(current);
        return directory;
    }
    private static void CheckDirectory(string path)
    { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new ContentRejectedException("Artifact storage cannot follow symbolic links."); }
}
