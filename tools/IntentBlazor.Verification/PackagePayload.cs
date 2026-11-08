using System.IO.Compression;
using System.Security.Cryptography;

namespace IntentBlazor.Verification;

// NuGet repository signing adds .signature.p7s. Compare the entire remaining ZIP payload, not the signed container bytes.
public static class PackagePayload
{
    public static string Hash(string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count is < 1 or > 1000) throw new InvalidDataException("Unexpected package entry count.");
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase); List<ManagedFile> files = []; long total = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!DeploymentBundle.SafePath(entry.FullName) || !paths.Add(entry.FullName)) throw new InvalidDataException("Unsafe or ambiguous package entry.");
            if (entry.FullName == ".signature.p7s") continue;
            if (entry.Length is < 0 or > 67108864 || (total += entry.Length) > 268435456) throw new InvalidDataException("Package payload exceeds its bound.");
            using Stream stream = entry.Open();
            files.Add(new(entry.FullName, entry.Length, Convert.ToHexString(SHA256.HashData(stream))));
        }
        if (files.Count == 0) throw new InvalidDataException("Package payload is empty.");
        return DeploymentBundle.InventoryHash(files);
    }
}
