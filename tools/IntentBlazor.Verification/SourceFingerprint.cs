using System.Security.Cryptography;
using System.Text;

namespace IntentBlazor.Verification;

public static class SourceFingerprint
{
    public const string Protocol = "2";
    public static string Compute(string root)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("IntentBlazor source protocol " + Protocol + "\n"));
        foreach (string path in Inputs(root))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path).Replace('\\', '/') + "\n"));
            hash.AppendData(IsText(path) ? Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal)) : File.ReadAllBytes(path));
            hash.AppendData([0]);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public static IEnumerable<string> Inputs(string root) => new[] { "src", "specs", "examples", "apps", "tools", "scripts", "tests", "docs", "verification" }
        .Where(directory => Directory.Exists(Path.Combine(root, directory))).SelectMany(directory => Walk(Path.Combine(root, directory)))
        .Concat(new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "NuGet.Config", "IntentBlazor.slnx", "LICENSE", "THIRD-PARTY-NOTICES.md", "README.md", "ROADMAP.md", "CHANGELOG.md", "CONTRIBUTING.md", "SECURITY.md", "AGENTS.md", ".editorconfig", ".gitattributes", ".gitignore" }
            .Select(file => Path.Combine(root, file)).Where(File.Exists).Select(OrdinaryFile))
        .OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal);

    private static IEnumerable<string> Walk(string directory)
    {
        if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Release inputs cannot contain linked directories.");
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            string name = Path.GetFileName(path);
            if (name is "bin" or "obj" or "artifacts" or ".git" or "node_modules" || name.EndsWith(".Local.json", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(".env", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(name).Equals(".publishsettings", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".pubxml.user", StringComparison.OrdinalIgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(name, @"\.(pfx|p12|db|sqlite|sqlite3)(-(wal|shm|journal))?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)) continue;
            FileAttributes attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Release inputs cannot contain linked paths.");
            if (attributes.HasFlag(FileAttributes.Directory)) foreach (string child in Walk(path)) yield return child;
            else yield return path;
        }
    }
    private static string OrdinaryFile(string path) => File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) ? throw new InvalidDataException("Release inputs cannot contain linked files.") : path;
    private static bool IsText(string path) => Path.GetExtension(path).ToLowerInvariant() is ".cs" or ".razor" or ".css" or ".js" or ".json" or ".csproj" or ".props" or ".targets" or ".ps1" or ".md" or ".txt" or ".slnx" or ".config" or ".xml" || Path.GetFileName(path) is "LICENSE" or ".editorconfig" or ".gitattributes";
}
