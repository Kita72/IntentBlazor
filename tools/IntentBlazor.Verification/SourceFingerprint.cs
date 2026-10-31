using System.Security.Cryptography;
using System.Text;

namespace IntentBlazor.Verification;

public static class SourceFingerprint
{
    public static string Compute(string root)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IEnumerable<string> files = new[] { "src", "specs/fixtures" }
            .SelectMany(directory => Directory.EnumerateFiles(Path.Combine(root, directory), "*", SearchOption.AllDirectories))
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj") &&
                Path.GetExtension(path) is ".cs" or ".razor" or ".css" or ".csproj" or ".json")
            .Append(Path.Combine(root, "Directory.Packages.props"));
        foreach (string path in files.OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path).Replace('\\', '/') + "\n"));
            hash.AppendData(Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal)));
            hash.AppendData([0]);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
