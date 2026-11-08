using System.Collections.Immutable;
using System.Xml;
using System.Xml.Linq;

namespace IntentBlazor.Verification;

public sealed record TestEvidence(string Suite, string Path, int Total, int Passed, int Failed, int Skipped, string Sha256)
{
    public bool Complete => Total > 0 && Total == Passed && Failed == 0 && Skipped == 0 && EvidenceStamp.IsHash(Sha256, 64) && DeploymentBundle.SafePath(Path);
}
public sealed record RepositoryReport(EvidenceStamp Identity, ImmutableArray<TestEvidence> Tests, string BundleDirectory, string BundleHash)
{
    // Emitted only after Verify.ps1 completes its ordered restore/build/format/docs/consumer/native/browser checks.
    public ImmutableArray<string> Checks { get; init; } = [];
    public bool Passed => Identity is not null && Identity.Valid && !Tests.IsDefault && Tests.Length == 2 && Tests.All(test => test is not null && test.Complete) &&
        Tests.Count(test => test.Suite == "unit") == 1 && Tests.Count(test => test.Suite == "browser") == 1 &&
        !Checks.IsDefault && Checks.Order(StringComparer.Ordinal).SequenceEqual(RepositoryEvidence.RequiredChecks.Order(StringComparer.Ordinal)) &&
        DeploymentBundle.SafePath(BundleDirectory) && EvidenceStamp.IsHash(BundleHash, 64);
}

public static class RepositoryEvidence
{
    public static ImmutableArray<string> RequiredChecks { get; } = ["locked-restore", "release-build", "format", "documentation", "quickstart-consumer",
        "example-consumers", "production-host-consumer", "package-audit", "iis-native", "iis-browser-rollback", "host-recorder-compile"];
    public static RepositoryReport Capture(string root, EvidenceStamp identity, string unitTrx, string browserTrx, string bundle)
    {
        DeploymentManifest manifest = DeploymentBundle.Validate(bundle);
        if (!manifest.Identity.Matches(identity, DateTimeOffset.UtcNow)) throw new InvalidDataException("The tested bundle belongs to another candidate.");
        RepositoryReport report = new(identity, [ReadTrx(root, "unit", unitTrx), ReadTrx(root, "browser", browserTrx)], Relative(root, bundle), manifest.BundleHash) { Checks = RequiredChecks };
        if (!report.Passed) throw new InvalidDataException("Repository verification is incomplete.");
        return report;
    }
    public static bool MatchesArtifacts(RepositoryReport report, string root)
    {
        if (!report.Passed) return false;
        foreach (TestEvidence test in report.Tests)
        {
            string path = Address(root, test.Path);
            if (!File.Exists(path) || PackageAudit.FileHash(path) != test.Sha256) return false;
        }
        DeploymentManifest manifest = DeploymentBundle.Validate(Address(root, report.BundleDirectory));
        return manifest.BundleHash == report.BundleHash && manifest.Identity.Matches(report.Identity, DateTimeOffset.UtcNow);
    }
    public static TestEvidence ReadTrx(string root, string suite, string path)
    {
        using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16777216 });
        XDocument trx = XDocument.Load(reader); XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement counters = trx.Root?.Element(ns + "ResultSummary")?.Element(ns + "Counters") ?? throw new InvalidDataException("TRX has no counters.");
        int Count(string name) => int.TryParse(counters.Attribute(name)?.Value, out int result) && result >= 0 ? result : throw new InvalidDataException("Invalid TRX counter.");
        TestEvidence evidence = new(suite, Relative(root, path), Count("total"), Count("passed"), Count("failed"), Count("notExecuted"), PackageAudit.FileHash(path));
        XElement[] results = trx.Descendants(ns + "UnitTestResult").ToArray();
        if (!evidence.Complete || Count("executed") != evidence.Total || results.Length != evidence.Total || results.Any(result => result.Attribute("outcome")?.Value != "Passed"))
            throw new InvalidDataException("TRX is empty, failed, skipped or incomplete.");
        return evidence;
    }
    private static string Relative(string root, string path)
    {
        string relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path)).Replace('\\', '/');
        if (!DeploymentBundle.SafePath(relative)) throw new InvalidDataException("Verification artifacts must be inside the repository.");
        return relative;
    }
    private static string Address(string root, string relative)
    {
        if (!DeploymentBundle.SafePath(relative)) throw new InvalidDataException("Invalid verification artifact path.");
        string full = Path.GetFullPath(Path.Combine(root, relative));
        for (string? path = full; path is not null && path.Length >= Path.GetFullPath(root).Length; path = Path.GetDirectoryName(path))
            if ((File.Exists(path) || Directory.Exists(path)) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Verification artifacts cannot traverse links.");
        return full;
    }
}
