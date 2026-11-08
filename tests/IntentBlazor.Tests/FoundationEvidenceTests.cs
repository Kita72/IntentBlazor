using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using IntentBlazor.Verification;

namespace IntentBlazor.Tests;

public sealed class FoundationEvidenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
    internal static EvidenceStamp Candidate => new("0.1.0-preview.1", new('a', 40), new('B', 64), true, Now);
    internal static HostObservation Host => new(Candidate, "https://intentblazor.runasp.net/", 200, Candidate.Version, Candidate.SourceRevision,
        Candidate.SourceFingerprint, "fixture", "InteractiveServer", "X64", ".NET 10.0.14", Candidate.SourceRevision, Now, true, true, true);
    private static RuntimeReport Runtime => new(Candidate.SourceFingerprint, ".NET 10.0.14", "X64", 4, Now,
        RuntimeWorkloads.Budgets.Select(pair => new TimingSample(pair.Key, 30, 1, 2, 200, pair.Value)).ToImmutableArray())
    { Identity = Candidate };
    private static PackageReport Packages => new(Candidate.Version, Candidate.SourceRevision, Now,
        PackageAudit.PackageIds.Select(id => new PackageCheck(id, true, []) { PackageSha256 = new('C', 64), SymbolsSha256 = new('D', 64) }).ToImmutableArray())
    { Identity = Candidate };
    internal static RepositoryReport RepositoryFor(EvidenceStamp identity) => new(identity,
        [new("unit", "artifacts/unit.trx", 1, 1, 0, 0, new('A', 64)), new("browser", "artifacts/browser.trx", 1, 1, 0, 0, new('A', 64))],
        "artifacts/bundle", new('A', 64))
    { Checks = RepositoryEvidence.RequiredChecks };

    [Theory]
    [InlineData("tools/Verifier/Protocol.cs")]
    [InlineData("scripts/Verify.ps1")]
    [InlineData("apps/Docs/Service.cs")]
    [InlineData("examples/Staff/Service.cs")]
    [InlineData("tests/Regression.cs")]
    [InlineData("docs/releasing.md")]
    [InlineData("specs/fixtures/image.png")]
    [InlineData("Directory.Build.props")]
    public void Fingerprint_BindsEvaluatorServicesConfigurationAndBinaryAssets(string relative)
    {
        using OperationScratch scratch = new(); string file = scratch.Write(relative, "first");
        string before = SourceFingerprint.Compute(scratch.Path);
        File.AppendAllText(file, "changed");
        Assert.NotEqual(before, SourceFingerprint.Compute(scratch.Path));
    }

    [Theory]
    [InlineData("apps/Docs/bin/Generated.dll")]
    [InlineData("tools/Verifier/obj/generated.cs")]
    [InlineData("examples/Staff/appsettings.Local.json")]
    [InlineData("examples/Staff/.env")]
    [InlineData("apps/Docs/private.sqlite-wal")]
    [InlineData("apps/Docs/site.publishSettings")]
    public void Fingerprint_IgnoresOutputsAndPrivateLocalMaterial(string relative)
    {
        using OperationScratch scratch = new(); scratch.Write("src/Library.cs", "source");
        string before = SourceFingerprint.Compute(scratch.Path);
        scratch.Write(relative, "synthetic private local material");
        Assert.Equal(before, SourceFingerprint.Compute(scratch.Path));
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("fingerprint")]
    [InlineData("version")]
    [InlineData("protocol")]
    [InlineData("dirty")]
    [InlineData("stale")]
    [InlineData("future")]
    public void Readiness_RejectsEvidenceFromAnotherCandidateOrTime(string fault)
    {
        EvidenceStamp changed = fault switch
        {
            "revision" => Candidate with { SourceRevision = new('e', 40) },
            "fingerprint" => Candidate with { SourceFingerprint = new('E', 64) },
            "version" => Candidate with { Version = "0.2.0-preview.1" },
            "protocol" => Candidate with { Protocol = "1" },
            "dirty" => Candidate with { Clean = false },
            "stale" => Candidate with { CheckedAt = Now.AddDays(-8) },
            _ => Candidate with { CheckedAt = Now.AddMinutes(6) }
        };
        ReadinessReport report = ReleaseReadiness.Evaluate(Candidate, [], Runtime with { Identity = changed, CheckedAt = changed.CheckedAt },
            Packages with { Identity = changed, CheckedAt = changed.CheckedAt }, true, Host with { Identity = changed }, Now, RepositoryFor(changed));
        Assert.False(report.Ready);
        Assert.All(report.Gates.Where(gate => gate.Gate is "repository-checks" or "runtime-budgets" or "package-metadata" or "public-host"), gate => Assert.False(gate.Passed));
    }

    [Fact]
    public void Readiness_RequiresCleanSourceAndExplicitHostObservation()
    {
        Assert.True(ReleaseReadiness.Evaluate(Candidate, [], Runtime, Packages, true, Host, Now, RepositoryFor(Candidate)).Ready);
        EvidenceStamp dirty = Candidate with { Clean = false };
        ReadinessReport dirtyReport = ReleaseReadiness.Evaluate(dirty, [], Runtime with { Identity = dirty }, Packages with { Identity = dirty }, true,
            Host with { Identity = dirty }, Now, RepositoryFor(dirty));
        Assert.False(dirtyReport.Ready); Assert.False(Assert.Single(dirtyReport.Gates, gate => gate.Gate == "candidate-source").Passed);
        Assert.All(dirtyReport.Gates.Where(gate => gate.Gate != "candidate-source"), gate => Assert.True(gate.Passed));
        ReadinessReport missing = ReleaseReadiness.Evaluate(Candidate, [], Runtime, Packages, true, null, Now, RepositoryFor(Candidate));
        Assert.False(missing.Ready); Assert.False(Assert.Single(missing.Gates, gate => gate.Gate == "public-host").Passed);
        Assert.False(ReleaseReadiness.Evaluate(Candidate, [], Runtime with { Identity = null }, Packages with { Identity = null }, true, Host, Now, RepositoryFor(Candidate)).Ready);
        Assert.False(ReleaseReadiness.Evaluate(Candidate, [], Runtime, Packages, true, Host, Now).Ready);
    }

    [Theory]
    [InlineData("origin")]
    [InlineData("http")]
    [InlineData("loopback")]
    [InlineData("status")]
    [InlineData("revision")]
    [InlineData("fingerprint")]
    [InlineData("version")]
    [InlineData("mode")]
    [InlineData("runtime")]
    [InlineData("architecture")]
    [InlineData("circuit-revision")]
    [InlineData("circuit-stale")]
    [InlineData("request")]
    [InlineData("selection")]
    [InlineData("draft")]
    public void HostObservation_RejectsUnmatchedHealthOrIncompleteCircuit(string fault)
    {
        HostObservation changed = fault switch
        {
            "origin" => Host with { Origin = "https://other.example/" },
            "http" => Host with { Origin = "http://intentblazor.runasp.net/" },
            "loopback" => Host with { Origin = "https://127.0.0.1/" },
            "status" => Host with { StatusCode = 503 },
            "revision" => Host with { SourceRevision = new('f', 40) },
            "fingerprint" => Host with { SourceFingerprint = new('F', 64) },
            "version" => Host with { Version = "old" },
            "mode" => Host with { Mode = "configured-model" },
            "runtime" => Host with { Runtime = ".NET 11.0.0" },
            "architecture" => Host with { Architecture = "X86" },
            "circuit-revision" => Host with { CircuitSourceRevision = new('f', 40) },
            "circuit-stale" => Host with { CircuitCheckedAt = Now.AddDays(-8) },
            "request" => Host with { InteractiveRequest = false },
            "selection" => Host with { SelectionChanged = false },
            _ => Host with { DraftRetained = false }
        };
        Assert.False(changed.Matches(Candidate, Now));
    }

    [Fact]
    public void PackageEvidence_RehashesBothPackagesAndSymbolsBeforeAcceptingReport()
    {
        using OperationScratch scratch = new();
        ImmutableArray<PackageCheck> checks = PackageAudit.PackageIds.Select(id =>
        {
            string package = scratch.Write($"{id}.{Candidate.Version}.nupkg", "synthetic audited bytes");
            string symbols = scratch.Write($"{id}.{Candidate.Version}.snupkg", "synthetic symbol bytes");
            return new PackageCheck(id, true, []) { PackageSha256 = PackageAudit.FileHash(package), SymbolsSha256 = PackageAudit.FileHash(symbols) };
        }).ToImmutableArray();
        PackageReport report = Packages with { Packages = checks };
        Assert.True(PackageAudit.MatchesArtifacts(report, scratch.Path));
        Assert.False(PackageAudit.MatchesArtifacts(report with { Version = "../outside" }, scratch.Path));
        string changed = System.IO.Path.Combine(scratch.Path, $"IntentBlazor.Core.{Candidate.Version}.nupkg");
        File.AppendAllText(changed, "tampered"); Assert.False(PackageAudit.MatchesArtifacts(report, scratch.Path));
        File.WriteAllText(changed, "synthetic audited bytes"); Assert.True(PackageAudit.MatchesArtifacts(report, scratch.Path));
        File.Delete(System.IO.Path.Combine(scratch.Path, $"IntentBlazor.Core.{Candidate.Version}.snupkg"));
        Assert.False(PackageAudit.MatchesArtifacts(report, scratch.Path));
    }

    [Theory]
    [InlineData("{\"version\":\"first\",\"version\":\"second\"}")]
    [InlineData("{\"identity\":{\"clean\":true,\"clean\":false}}")]
    public void EvidenceReader_RejectsDuplicatePropertiesAtEveryDepth(string json)
    {
        using OperationScratch scratch = new(); string path = scratch.Write("evidence.json", json);
        Assert.Throws<InvalidDataException>(() => ReleaseReadiness.ReadOptional<EvidenceStamp>(path));
    }

    [Fact]
    public async Task FailedReadinessCommand_ReplacesOldSuccessWithIncompleteEvidence()
    {
        using OperationScratch scratch = new(); scratch.Write("packages.json", "malformed required report");
        string output = scratch.Write("readiness.json", "{\"ready\":true}");
        string root = FindRoot();
        ProcessStartInfo start = new("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        string framework = System.IO.Path.GetFileName(AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        foreach (string arg in new[] { System.IO.Path.Combine(root, "tools/IntentBlazor.Verification/bin/Release", framework, "IntentBlazor.Verification.dll"),
            "readiness", root, Candidate.Version, scratch.Path, output, "require-ready" }) start.ArgumentList.Add(arg);
        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); await Task.WhenAll(stdout, stderr);
        Assert.NotEqual(0, process.ExitCode);
        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal("incomplete", report.RootElement.GetProperty("status").GetString());
        Assert.False(report.RootElement.TryGetProperty("ready", out _));
    }

    private static string FindRoot()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(System.IO.Path.Combine(root.FullName, "IntentBlazor.slnx"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

internal sealed class OperationScratch : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "IntentBlazor-Operations-" + Guid.NewGuid().ToString("N"));
    public OperationScratch() => Directory.CreateDirectory(Path);
    public string Write(string relative, string content)
    {
        string file = System.IO.Path.Combine(Path, relative); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!); File.WriteAllText(file, content); return file;
    }
    public void Dispose()
    {
        if (System.IO.Path.GetDirectoryName(Path) != System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath())) ||
            !System.IO.Path.GetFileName(Path).StartsWith("IntentBlazor-Operations-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected scratch root.");
        Directory.Delete(Path, recursive: true);
    }
}
