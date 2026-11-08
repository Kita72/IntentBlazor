using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace IntentBlazor.Verification;

public sealed record ManagedFile(string Path, long Bytes, string Sha256);
public sealed record DeploymentManifest(EvidenceStamp Identity, string RuntimeIdentifier, ImmutableArray<ManagedFile> Files, string BundleHash)
{
    public string FormatVersion { get; init; } = "2";
    public string Environment { get; init; } = "Production";
    public string Mode { get; init; } = "fixture";
    public string TargetFramework { get; init; } = "net10.0";
    public bool SelfContained { get; init; }
    public long TotalBytes => Files.Sum(file => file.Bytes);
}
public sealed record DeploymentPlan(string? PreviousBundleHash, string NextBundleHash, ImmutableArray<string> Writes,
    ImmutableArray<string> Deletes, ImmutableArray<string> Preserves);

public static class DeploymentBundle
{
    public const string ManifestName = "deployment-manifest.json";
    public static DeploymentManifest Seal(string root, EvidenceStamp identity)
    {
        root = Root(root);
        if (!identity.Valid) throw new InvalidDataException("A current source identity is required to seal a bundle.");
        foreach (string file in Files(root).Where(path => Path.GetFileName(path).Equals("libSkiaSharp.pdb", StringComparison.OrdinalIgnoreCase)))
        {
            using (FileStream stream = File.OpenRead(file))
            {
                byte[] header = new byte[32];
                if (stream.Read(header) != header.Length || !Encoding.ASCII.GetString(header).StartsWith("Microsoft C/C++ MSF", StringComparison.Ordinal))
                    throw new InvalidDataException("An unrecognized native symbol file cannot be removed.");
            }
            File.Delete(file); // Only this recognized native debug artifact in the owned publish folder.
        }
        ValidateRuntime(root);
        ImmutableArray<ManagedFile> files = Inventory(root);
        DeploymentManifest manifest = new(identity, "win-x64", files, InventoryHash(files));
        ValidateManifest(manifest);
        VerificationJson.Write(Path.Combine(root, ManifestName), manifest);
        return Validate(root);
    }

    public static DeploymentManifest Validate(string root)
    {
        root = Root(root);
        DeploymentManifest manifest = ReleaseReadiness.ReadOptional<DeploymentManifest>(Path.Combine(root, ManifestName))
            ?? throw new InvalidDataException("A sealed deployment manifest is required.");
        ValidateManifest(manifest); ValidateRuntime(root);
        ImmutableArray<ManagedFile> actual = Inventory(root);
        if (actual.Length != manifest.Files.Length || !actual.SequenceEqual(manifest.Files)) throw new InvalidDataException("Bundle files differ from the sealed inventory.");
        return manifest;
    }

    public static void ValidateManifest(DeploymentManifest manifest)
    {
        if (manifest.FormatVersion != "2" || manifest.Identity?.Valid != true || manifest.RuntimeIdentifier != "win-x64" ||
            manifest.Environment != "Production" || manifest.Mode != "fixture" || manifest.TargetFramework != "net10.0" || manifest.SelfContained ||
            manifest.Files.IsDefaultOrEmpty || manifest.Files.Length > 10000 || manifest.Files.Any(file => file is null) ||
            manifest.Files.Select(file => file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Files.Length)
            throw new InvalidDataException("Unsupported, incomplete or ambiguous deployment manifest.");
        foreach (ManagedFile file in manifest.Files)
            if (!SafePath(file.Path) || Private(file.Path) || file.Bytes < 0 || !EvidenceStamp.IsHash(file.Sha256, 64))
                throw new InvalidDataException("The deployment inventory contains an unsafe file.");
        if (manifest.BundleHash != InventoryHash(manifest.Files)) throw new InvalidDataException("The deployment inventory hash is invalid.");
    }

    public static DeploymentPlan Plan(string bundleRoot, string destination)
    {
        bundleRoot = Root(bundleRoot); destination = Root(destination);
        if (Within(bundleRoot, destination) || Within(destination, bundleRoot)) throw new InvalidDataException("Source and destination directories must not overlap.");
        DeploymentManifest next = Validate(bundleRoot);
        string priorPath = Path.Combine(destination, ManifestName);
        if (Directory.Exists(priorPath) || File.Exists(priorPath) && File.GetAttributes(priorPath).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("The destination manifest must be an ordinary file.");
        DeploymentManifest? previous = ReleaseReadiness.ReadOptional<DeploymentManifest>(priorPath);
        if (previous is not null) ValidateManifest(previous);
        Dictionary<string, ManagedFile> oldFiles = previous?.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase) ?? new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, ManagedFile> newFiles = next.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        ImmutableArray<string>.Builder writes = ImmutableArray.CreateBuilder<string>(), deletes = ImmutableArray.CreateBuilder<string>();
        foreach (ManagedFile file in next.Files)
        {
            string target = Address(destination, file.Path);
            if (Directory.Exists(target)) throw new InvalidDataException("An incoming file collides with a destination directory.");
            for (string? parent = Path.GetDirectoryName(target); parent is not null && parent.Length > destination.Length; parent = Path.GetDirectoryName(parent))
                if (File.Exists(parent)) throw new InvalidDataException("An incoming directory collides with a destination file.");
            if (File.Exists(target))
            {
                string actual = PackageAudit.FileHash(target);
                if (oldFiles.TryGetValue(file.Path, out ManagedFile? prior))
                {
                    if (prior.Path != file.Path) throw new InvalidDataException("Case-only managed renames require explicit reconciliation.");
                    if (actual != prior.Sha256) throw new InvalidDataException("A managed destination file was modified; reconcile it before deployment.");
                }
                else if (actual != file.Sha256) throw new InvalidDataException("An unmanaged destination file collides with the incoming bundle.");
            }
            writes.Add(file.Path);
        }
        foreach (ManagedFile file in oldFiles.Values.Where(file => !newFiles.ContainsKey(file.Path)))
        {
            string target = Address(destination, file.Path);
            if (Directory.Exists(target)) throw new InvalidDataException("An obsolete managed file was replaced by a directory.");
            if (!File.Exists(target)) continue;
            if (PackageAudit.FileHash(target) != file.Sha256) throw new InvalidDataException("An obsolete managed file was modified and cannot be deleted.");
            deletes.Add(file.Path);
        }
        ImmutableArray<string> preserved = Files(destination).Select(path => Relative(destination, path))
            .Where(path => !oldFiles.ContainsKey(path) && !newFiles.ContainsKey(path)).Order(StringComparer.Ordinal).ToImmutableArray();
        return new(previous?.BundleHash, next.BundleHash, writes.ToImmutable(), deletes.Order(StringComparer.Ordinal).ToImmutableArray(), preserved);
    }

    // Deliberately restricted to marked local rehearsal roots; never an uninspected remote site.
    public static DeploymentPlan ApplyRehearsal(string bundleRoot, string destination)
    {
        destination = Root(destination);
        if (!File.Exists(Path.Combine(destination, ".intentblazor-offline-fixture"))) throw new InvalidOperationException("Local application requires a marked offline rehearsal directory.");
        DeploymentPlan plan = Plan(bundleRoot, destination);
        foreach (string path in plan.Writes)
        {
            string target = Address(destination, path); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Address(Root(bundleRoot), path), target, overwrite: true);
        }
        foreach (string path in plan.Deletes) File.Delete(Address(destination, path));
        File.Copy(Path.Combine(bundleRoot, ManifestName), Path.Combine(destination, ManifestName), overwrite: true);
        return plan;
    }

    public static string InventoryHash(IEnumerable<ManagedFile> files) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',
        files.OrderBy(file => file.Path, StringComparer.Ordinal).Select(file => file.Path + "\0" + file.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0" + file.Sha256)))));

    public static bool SafePath(string path) => !string.IsNullOrWhiteSpace(path) && path.Length <= 512 && !Path.IsPathRooted(path) &&
        !path.Contains('\\') && !path.Any(character => char.IsControl(character) || "<>:\"|?*".Contains(character)) && path.Split('/').All(part => part.Length > 0 && part is not ("." or "..") &&
            part == part.TrimEnd(' ', '.') && !Reserved(part.Split('.')[0]));
    private static bool Reserved(string name) => new[] { "CON", "PRN", "AUX", "NUL" }.Contains(name, StringComparer.OrdinalIgnoreCase) ||
        name.Length == 4 && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && name[3] is >= '1' and <= '9';
    private static bool Within(string path, string parent) => path.Equals(parent, StringComparison.OrdinalIgnoreCase) || path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static bool Private(string path)
    {
        string name = Path.GetFileName(path);
        return path.Equals(ManifestName, StringComparison.OrdinalIgnoreCase) || path.Split('/').Any(part => part.Equals("App_Data", StringComparison.OrdinalIgnoreCase) || part.Equals("keys", StringComparison.OrdinalIgnoreCase)) ||
            name.StartsWith(".env", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".Local.json", StringComparison.OrdinalIgnoreCase) || name.Equals("app_offline.htm", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".publishsettings", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".pubxml.user", StringComparison.OrdinalIgnoreCase) ||
            name.Equals(".intentblazor-offline-fixture", StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(name, @"\.(pfx|p12|db|sqlite|sqlite3)(-(wal|shm|journal))?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
    private static string Root(string root)
    {
        string result = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (result == Path.GetPathRoot(result) || !Directory.Exists(result)) throw new InvalidDataException("An existing bounded application directory is required.");
        for (DirectoryInfo? current = new(result); current is not null; current = current.Parent)
            if (current.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Deployment paths cannot traverse linked directories.");
        return result;
    }
    private static string Address(string root, string relative)
    {
        if (!SafePath(relative)) throw new InvalidDataException("Invalid managed path.");
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        for (string? current = path; current is not null && current.Length >= root.Length; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Managed paths cannot traverse links.");
        return path;
    }
    private static IEnumerable<string> Files(string root, bool top = true)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(root))
        {
            FileAttributes attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Deployment inventories cannot contain linked files.");
            if (Path.GetFileName(path).Equals(ManifestName, StringComparison.OrdinalIgnoreCase))
            {
                if (attributes.HasFlag(FileAttributes.Directory)) throw new InvalidDataException("A manifest cannot be a directory.");
                if (!top) throw new InvalidDataException("Nested manifests are not supported.");
                continue;
            }
            if (attributes.HasFlag(FileAttributes.Directory)) foreach (string child in Files(path, top: false)) yield return child;
            else yield return path;
        }
    }
    private static string Relative(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');
    private static ImmutableArray<ManagedFile> Inventory(string root) => Files(root).Select(path => new ManagedFile(Relative(root, path), new FileInfo(path).Length, PackageAudit.FileHash(path)))
        .OrderBy(file => file.Path, StringComparer.Ordinal).ToImmutableArray();

    private static void ValidateRuntime(string root)
    {
        foreach (string file in new[] { "web.config", "IntentBlazor.Docs.dll", "IntentBlazor.Docs.deps.json", "IntentBlazor.Docs.runtimeconfig.json", "libSkiaSharp.dll" })
            if (!File.Exists(Address(root, file))) throw new InvalidDataException("The IIS runtime bundle is incomplete.");
        if (Files(root).Any(path => Relative(root, path).StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Equals("libSkiaSharp.pdb", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Use a RID-specific IIS bundle without native debug artifacts.");
        using XmlReader reader = XmlReader.Create(Address(root, "web.config"), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        XDocument web = XDocument.Load(reader); XElement? aspnet = web.Descendants("aspNetCore").SingleOrDefault();
        if (aspnet?.Attribute("hostingModel")?.Value != "inprocess" || aspnet.Attribute("processPath")?.Value != "dotnet" ||
            !(aspnet.Attribute("arguments")?.Value ?? "").EndsWith("IntentBlazor.Docs.dll", StringComparison.Ordinal) ||
            !aspnet.Descendants("environmentVariable").Any(item => item.Attribute("name")?.Value == "ASPNETCORE_ENVIRONMENT" && item.Attribute("value")?.Value == "Production"))
            throw new InvalidDataException("Expected framework-dependent Production IIS configuration.");
        using JsonDocument runtime = JsonDocument.Parse(File.ReadAllText(Address(root, "IntentBlazor.Docs.runtimeconfig.json")));
        if (runtime.RootElement.GetProperty("runtimeOptions").GetProperty("tfm").GetString() != "net10.0") throw new InvalidDataException("The IIS baseline remains net10.0.");
        using FileStream native = File.OpenRead(Address(root, "libSkiaSharp.dll")); using BinaryReader binary = new(native);
        if (native.Length < 64 || binary.ReadUInt16() != 0x5A4D) throw new InvalidDataException("Invalid native Windows dependency.");
        native.Position = 0x3c; int offset = binary.ReadInt32();
        if (offset < 64 || offset > native.Length - 6) throw new InvalidDataException("Invalid native Windows header.");
        native.Position = offset;
        if (binary.ReadUInt32() != 0x00004550 || binary.ReadUInt16() != 0x8664) throw new InvalidDataException("The native dependency does not match win-x64.");
    }
}
