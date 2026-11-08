using IntentBlazor.Verification;

namespace IntentBlazor.Tests;

public sealed class RepositoryEvidenceTests
{
    [Theory]
    [InlineData("failed")]
    [InlineData("skipped")]
    [InlineData("empty")]
    [InlineData("incomplete")]
    public void RepositoryEvidence_RejectsFailedSkippedEmptyAndIncompleteTrx(string fault)
    {
        using OperationScratch scratch = new();
        string xml = Trx;
        xml = fault switch
        {
            "failed" => xml.Replace("outcome=\"Passed\"", "outcome=\"Failed\"", StringComparison.Ordinal),
            "skipped" => xml.Replace("notExecuted=\"0\"", "notExecuted=\"1\"", StringComparison.Ordinal),
            "empty" => xml.Replace("total=\"1\"", "total=\"0\"", StringComparison.Ordinal),
            _ => xml.Replace("<UnitTestResult outcome=\"Passed\" />", "", StringComparison.Ordinal)
        };
        string path = scratch.Write("unit.trx", xml);
        Assert.Throws<InvalidDataException>(() => RepositoryEvidence.ReadTrx(scratch.Path, "unit", path));
    }

    [Fact]
    public void RepositoryEvidence_RehashesTrxAndSealedBundleAndRejectsMissingChecks()
    {
        using OperationScratch bundle = DeploymentBundleTests.Bundle(); DeploymentBundle.Seal(bundle.Path, FoundationEvidenceTests.Candidate);
        using OperationScratch scratch = new(); string unit = scratch.Write("unit.trx", Trx), browser = scratch.Write("browser.trx", Trx);
        string root = Path.GetDirectoryName(bundle.Path)!;
        RepositoryReport report = RepositoryEvidence.Capture(root, FoundationEvidenceTests.Candidate, unit, browser, bundle.Path);
        Assert.True(report.Passed); Assert.True(RepositoryEvidence.MatchesArtifacts(report, root));
        Assert.False((report with { Checks = report.Checks.RemoveAt(0) }).Passed);
        Assert.False((report with { Tests = report.Tests.RemoveAt(0) }).Passed);
        File.AppendAllText(unit, " "); Assert.False(RepositoryEvidence.MatchesArtifacts(report, root));
        File.WriteAllText(unit, Trx); Assert.True(RepositoryEvidence.MatchesArtifacts(report, root));
        File.AppendAllText(Path.Combine(bundle.Path, "IntentBlazor.Docs.dll"), "tampered");
        Assert.Throws<InvalidDataException>(() => RepositoryEvidence.MatchesArtifacts(report, root));
    }

    private const string Trx = """
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results><UnitTestResult outcome="Passed" /></Results><ResultSummary><Counters total="1" executed="1" passed="1" failed="0" notExecuted="0" /></ResultSummary></TestRun>
        """;
}
