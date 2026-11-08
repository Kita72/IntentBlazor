using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace IntentBlazor.Verification;

public sealed record PackageCheck(string Package, bool Passed, ImmutableArray<string> Errors)
{
    public string? PackageSha256 { get; init; }
    public string? SymbolsSha256 { get; init; }
}
public sealed record PackageReport(string Version, string SourceRevision, DateTimeOffset CheckedAt, ImmutableArray<PackageCheck> Packages)
{
    public EvidenceStamp? Identity { get; init; }
    public bool Passed => !string.IsNullOrWhiteSpace(Version) && System.Text.RegularExpressions.Regex.IsMatch(Version, @"^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant) &&
        EvidenceStamp.IsHash(SourceRevision, 40) && !Packages.IsDefault && Packages.Length == PackageAudit.PackageIds.Length && Packages.All(package => package is not null) &&
        PackageAudit.PackageIds.All(id => Packages.Count(package => package.Package == id) == 1) &&
        Packages.All(package => package.Passed && !package.Errors.IsDefault && package.Errors.IsEmpty &&
            EvidenceStamp.IsHash(package.PackageSha256, 64) && EvidenceStamp.IsHash(package.SymbolsSha256, 64));
}

public static class PackageAudit
{
    public static ImmutableArray<string> PackageIds { get; } =
    [
        "IntentBlazor.Core", "IntentBlazor.Runtime", "IntentBlazor.Blazor", "IntentBlazor.AI",
        "IntentBlazor.Storage.Sqlite", "IntentBlazor.Content", "IntentBlazor.Packs.ImageEditor", "IntentBlazor.Blazor.AI"
    ];
    private static readonly Guid SourceLinkKind = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");

    public static PackageReport Inspect(string directory, string version, string revision) =>
        new(version, revision, DateTimeOffset.UtcNow, PackageIds.Select(id => InspectOne(directory, id, version, revision)).ToImmutableArray());

    public static PackageCheck InspectOne(string directory, string id, string version, string revision)
    {
        List<string> errors = [];
        string packagePath = Path.Combine(directory, $"{id}.{version}.nupkg");
        string symbolPath = Path.Combine(directory, $"{id}.{version}.snupkg");
        if (!File.Exists(packagePath)) return new(id, false, ["Missing package."]);
        try
        {
            using ZipArchive package = ZipFile.OpenRead(packagePath);
            ZipArchiveEntry? specification = package.Entries.SingleOrDefault(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
            if (specification is null) errors.Add("Missing nuspec.");
            else
            {
                using Stream stream = specification.Open();
                XDocument xml = XDocument.Load(stream);
                XElement? metadata = xml.Root?.Elements().SingleOrDefault(element => element.Name.LocalName == "metadata");
                string? Value(string name) => metadata?.Elements().SingleOrDefault(element => element.Name.LocalName == name)?.Value;
                XElement? repository = metadata?.Elements().SingleOrDefault(element => element.Name.LocalName == "repository");
                XElement? license = metadata?.Elements().SingleOrDefault(element => element.Name.LocalName == "license");
                Require(Value("id") == id, "Package ID does not match its artifact.");
                Require(Value("version") == version, "Package version does not match the candidate.");
                Require(license?.Attribute("type")?.Value == "expression" && license.Value == "MIT", "MIT license metadata is required.");
                Require(Value("readme") == "README.md" && Read(package, "README.md")?.Contains(id, StringComparison.Ordinal) == true,
                    "A package-specific readme is required.");
                Require(Read(package, "LICENSE")?.Contains("MIT License", StringComparison.Ordinal) == true, "The license file is missing.");
                Require(repository?.Attribute("type")?.Value == "git" && repository.Attribute("url")?.Value == "https://github.com/Kita72/IntentBlazor",
                    "Repository metadata is missing or incorrect.");
                Require(revision.Length == 40 && repository?.Attribute("commit")?.Value == revision, "Repository commit does not match the build.");
                Require(!string.IsNullOrWhiteSpace(Value("description")), "Package description is required.");
                var dependencies = metadata?.Descendants().Where(element => element.Name.LocalName == "dependency") ?? [];
                Require(dependencies.Where(dependency => dependency.Attribute("id")?.Value.StartsWith("IntentBlazor.", StringComparison.Ordinal) == true)
                    .All(dependency => dependency.Attribute("version")?.Value.Trim('[', ']') == version), "Framework dependency versions do not match.");
            }
            Require(package.GetEntry($"lib/net10.0/{id}.dll") is not null, "The net10.0 assembly is missing.");
            Require(!package.Entries.Any(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal) && entry.FullName.EndsWith(".pdb", StringComparison.Ordinal)),
                "Portable symbols belong in the symbol package.");
            Require(!package.Entries.Any(entry => entry.FullName.Contains(".env", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.EndsWith(".db", StringComparison.OrdinalIgnoreCase) || entry.FullName.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)),
                "Unexpected private/configuration artifact.");
            if (!File.Exists(symbolPath)) errors.Add("Missing snupkg.");
            else
            {
                using ZipArchive symbols = ZipFile.OpenRead(symbolPath);
                ZipArchiveEntry? pdb = symbols.GetEntry($"lib/net10.0/{id}.pdb");
                if (pdb is null) errors.Add("Missing portable PDB.");
                else
                {
                    using MemoryStream memory = new(); using Stream input = pdb.Open(); input.CopyTo(memory); memory.Position = 0;
                    Require(HasSourceLink(memory, revision), "Portable PDB does not link to the exact source commit.");
                }
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or BadImageFormatException or JsonException or System.Xml.XmlException or InvalidOperationException or KeyNotFoundException)
        { errors.Add("Package or symbol metadata could not be read."); }
        return new(id, errors.Count == 0, [.. errors])
        {
            PackageSha256 = File.Exists(packagePath) ? FileHash(packagePath) : null,
            SymbolsSha256 = File.Exists(symbolPath) ? FileHash(symbolPath) : null
        };

        void Require(bool condition, string message) { if (!condition) errors.Add(message); }
    }

    public static bool MatchesArtifacts(PackageReport report, string directory) => report.Passed && report.Packages.All(package =>
        Matches(Path.Combine(directory, $"{package.Package}.{report.Version}.nupkg"), package.PackageSha256) &&
        Matches(Path.Combine(directory, $"{package.Package}.{report.Version}.snupkg"), package.SymbolsSha256));
    private static bool Matches(string path, string? expected) => File.Exists(path) && FileHash(path) == expected;
    public static string FileHash(string path) { using FileStream stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }

    public static bool HasSourceLink(Stream portablePdb, string revision)
    {
        using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(portablePdb, MetadataStreamOptions.LeaveOpen);
        MetadataReader reader = provider.GetMetadataReader();
        foreach (CustomDebugInformationHandle handle in reader.CustomDebugInformation)
        {
            CustomDebugInformation info = reader.GetCustomDebugInformation(handle);
            if (reader.GetGuid(info.Kind) != SourceLinkKind) continue;
            using JsonDocument data = JsonDocument.Parse(reader.GetBlobBytes(info.Value));
            JsonElement documents = data.RootElement.GetProperty("documents");
            return documents.ValueKind == JsonValueKind.Object && documents.EnumerateObject().Any() &&
                documents.EnumerateObject().All(item => item.Value.GetString() is { } value &&
                    value.StartsWith($"https://raw.githubusercontent.com/Kita72/IntentBlazor/{revision}/", StringComparison.OrdinalIgnoreCase));
        }
        return false;
    }

    private static string? Read(ZipArchive archive, string path)
    {
        ZipArchiveEntry? entry = archive.GetEntry(path);
        if (entry is null || entry.Length > 262144) return null;
        using StreamReader reader = new(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
