using System.Collections.Immutable;
using System.IO.Compression;
using IntentBlazor.AI;
using IntentBlazor.Examples;
using IntentBlazor.Verification;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

public sealed class Phase7VerificationTests
{
    private const string Version = "0.1.0-preview.1";
    private const string Fingerprint = "current-source";
    private static readonly DateTimeOffset CheckedAt = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Readiness_RequiresEveryProviderAndAccountAndHostGate()
    {
        ModelEvaluation[] models = Models();
        Assert.True(Evaluate(models).Ready);
        Assert.False(Evaluate(models[..2]).Ready);
        Assert.False(Evaluate(models, policy: false).Ready);
        Assert.False(Evaluate(models, host: false).Ready);
        Assert.False(ReleaseReadiness.Evaluate(Version, Fingerprint, models, null, Packages(), true, true).Ready);
        Assert.False(ReleaseReadiness.Evaluate(Version, Fingerprint, models, Runtime(), null, true, true).Ready);
        ReadinessReport empty = ReleaseReadiness.Evaluate(Version, Fingerprint, [], null, null, false, false);
        Assert.Equal(7, empty.Gates.Length); Assert.All(empty.Gates, gate => Assert.False(gate.Passed));
        Assert.Contains("deferred", empty.Gates.Single(gate => gate.Gate == "provider:OpenAI").Detail);
    }

    [Theory]
    [InlineData("stale-source")]
    [InlineData("missing-revision")]
    [InlineData("missing-server")]
    [InlineData("old-protocol")]
    [InlineData("unsupported-data-path")]
    [InlineData("duplicate-fixture")]
    [InlineData("missing-fixture")]
    [InlineData("failed-followup")]
    [InlineData("no-service-read")]
    [InlineData("lost-draft")]
    [InlineData("missing-run")]
    [InlineData("errored-run")]
    public void Readiness_RejectsIncompleteOrStaleLiveEvidence(string fault)
    {
        ModelEvaluation[] models = Models(); ModelEvaluation run = models[0]; ModelScenarioResult scenario = run.Scenarios[0];
        models[0] = fault switch
        {
            "stale-source" => run with { SourceFingerprint = "older-source" },
            "missing-revision" => run with { ModelRevision = "" },
            "missing-server" => run with { ServerVersion = "" },
            "old-protocol" => run with { EvaluationProtocol = "0" },
            "unsupported-data-path" => run with { DataAccess = "scripted" },
            "duplicate-fixture" => run with { Scenarios = [scenario, scenario, run.Scenarios[2]] },
            "missing-fixture" => run with { Scenarios = run.Scenarios.RemoveAt(0) },
            "failed-followup" => run with { Scenarios = run.Scenarios.SetItem(0, scenario with { FollowupAccepted = false }) },
            "no-service-read" => run with { Scenarios = run.Scenarios.SetItem(0, scenario with { ObservedDataCalls = 0 }) },
            "lost-draft" => run with { Scenarios = run.Scenarios.SetItem(0, scenario with { DraftPreserved = false }) },
            "missing-run" => run with { Scenarios = run.Scenarios.SetItem(0, scenario with { InitialRun = null }) },
            "errored-run" => run with { Scenarios = run.Scenarios.SetItem(0, scenario with { InitialRun = scenario.InitialRun! with { Status = "invalid-output" } }) },
            _ => throw new ArgumentException("Unknown test case.")
        };
        ReadinessReport report = Evaluate(models);
        Assert.False(report.Ready); Assert.False(report.Gates.Single(gate => gate.Gate == "provider:Ollama").Passed);
    }

    [Fact]
    public void RuntimeBudgets_RejectMissingDuplicateForgedAndNonFiniteSamples()
    {
        RuntimeReport valid = Runtime(); Assert.True(RuntimeWorkloads.MeetsBudgets(valid));
        Assert.False(RuntimeWorkloads.MeetsBudgets(valid with { SourceFingerprint = "old", Samples = [] }));
        Assert.False(RuntimeWorkloads.MeetsBudgets(valid with { Samples = valid.Samples.SetItem(0, valid.Samples[1]) }));
        TimingSample sample = valid.Samples[0];
        foreach (TimingSample invalid in new[]
        {
            sample with { P95Milliseconds = double.NaN }, sample with { P95Milliseconds = double.PositiveInfinity },
            sample with { MedianMilliseconds = -1 }, sample with { P95Milliseconds = .5 },
            sample with { P95Milliseconds = sample.InitialBudgetMilliseconds + .01 }, sample with { Iterations = 9 },
            sample with { Iterations = 201 }, sample with { OutputCharacters = 0 },
            sample with { InitialBudgetMilliseconds = 10000 }
        }) Assert.False(RuntimeWorkloads.MeetsBudgets(valid with { Samples = valid.Samples.SetItem(0, invalid) }));
        Assert.False(ReleaseReadiness.Evaluate(Version, Fingerprint, Models(), valid with { SourceFingerprint = "old" }, Packages(), true, true).Ready);
    }

    [Fact]
    public void PackageReports_RequireEightDistinctCleanPackagesAndMatchingVersion()
    {
        PackageReport valid = Packages(); Assert.True(valid.Passed);
        Assert.False((valid with { Packages = default }).Passed);
        Assert.False((valid with { Packages = valid.Packages.RemoveAt(0) }).Passed);
        Assert.False((valid with { Packages = valid.Packages.SetItem(0, valid.Packages[1]) }).Passed);
        Assert.False((valid with { Packages = valid.Packages.SetItem(0, valid.Packages[0] with { Errors = ["wrong commit"] }) }).Passed);
        Assert.False((valid with { Packages = valid.Packages.SetItem(0, valid.Packages[0] with { Errors = default }) }).Passed);
        Assert.False(ReleaseReadiness.Evaluate(Version, Fingerprint, Models(), Runtime(), valid with { Version = "0.1.0-alpha.0" }, true, true).Ready);
    }

    [Fact]
    public void PackageAudit_ReportsAbsentMalformedAndIncompleteArtifacts()
    {
        using ScratchDirectory scratch = new(); string id = PackageAudit.PackageIds[0];
        PackageReport missing = PackageAudit.Inspect(scratch.Path, Version, new('a', 40));
        Assert.False(missing.Passed); Assert.All(missing.Packages, check => Assert.Contains("Missing package.", check.Errors));
        string package = System.IO.Path.Combine(scratch.Path, $"{id}.{Version}.nupkg");
        File.WriteAllText(package, "not a zip");
        Assert.Contains("Package or symbol metadata could not be read.", PackageAudit.InspectOne(scratch.Path, id, Version, new('a', 40)).Errors);
        File.Delete(package);
        using (ZipArchive archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            using StreamWriter writer = new(archive.CreateEntry(id + ".nuspec").Open());
            writer.Write("<package><metadata><id>Different.Id</id><version>9.9.9</version><license type=\"expression\">GPL-3.0</license></metadata></package>");
        }
        PackageCheck result = PackageAudit.InspectOne(scratch.Path, id, Version, new('a', 40));
        Assert.False(result.Passed);
        Assert.Contains("Package ID does not match its artifact.", result.Errors);
        Assert.Contains("Package version does not match the candidate.", result.Errors);
        Assert.Contains("MIT license metadata is required.", result.Errors);
        Assert.Contains("A package-specific readme is required.", result.Errors);
        Assert.Contains("Repository commit does not match the build.", result.Errors);
        Assert.Contains("Missing snupkg.", result.Errors);
    }

    [Fact]
    public void Fingerprint_NormalizesLineEndingsAndIgnoresBuildOutputsButDetectsSourceAndFixtureChanges()
    {
        using ScratchDirectory a = new(); using ScratchDirectory b = new();
        foreach (string root in new[] { a.Path, b.Path })
        {
            Directory.CreateDirectory(System.IO.Path.Combine(root, "src", "Example", "obj"));
            Directory.CreateDirectory(System.IO.Path.Combine(root, "specs", "fixtures"));
            File.WriteAllText(System.IO.Path.Combine(root, "Directory.Packages.props"), "<Project />");
            File.WriteAllText(System.IO.Path.Combine(root, "src", "Example", "File.cs"), root == a.Path ? "first\r\nsecond\r\n" : "first\nsecond\n");
            File.WriteAllText(System.IO.Path.Combine(root, "specs", "fixtures", "news.json"), "{}");
        }
        string baseline = SourceFingerprint.Compute(a.Path); Assert.Equal(baseline, SourceFingerprint.Compute(b.Path));
        File.WriteAllText(System.IO.Path.Combine(a.Path, "src", "Example", "obj", "Generated.cs"), "ignored");
        Assert.Equal(baseline, SourceFingerprint.Compute(a.Path));
        File.AppendAllText(System.IO.Path.Combine(a.Path, "src", "Example", "File.cs"), "changed");
        Assert.NotEqual(baseline, SourceFingerprint.Compute(a.Path));
        File.WriteAllText(System.IO.Path.Combine(b.Path, "specs", "fixtures", "news.json"), "{\"changed\":true}");
        Assert.NotEqual(baseline, SourceFingerprint.Compute(b.Path));
    }

    [Fact]
    public void Diagnostics_EmitOnlyRequestShapeEvenWhenMessagesContainSecretsOrValidatorPrefixes()
    {
        string body = """
            {"tools":[{},{}],"response_format":{"type":"json_schema"},"api_key":"secret-key",
             "messages":[{"role":"user","content":"The validator rejected this proposal: secret prompt"},
                         {"role":"assistant","content":"private reasoning and output"}]}
            """;
        Assert.Equal("tools=2; response_format=json_schema", EvaluationDiagnostics.Describe(body));
        Assert.Equal("tools=0; response_format=unset", EvaluationDiagnostics.Describe("{}"));
    }

    [Theory]
    [InlineData("Ollama", "https://external.example/v1/")]
    [InlineData("OpenAI", "http://localhost:1234/v1/")]
    [InlineData("LM Studio", "http://user:password@localhost:1234/v1/")]
    [InlineData("LM Studio", "http://localhost:1234/v1/?token=secret")]
    [InlineData("Ollama", "http://localhost:11434/v1")]
    [InlineData("Ollama", "ftp://localhost/v1/")]
    public void LocalEvaluation_RejectsCloudCredentialAndAmbiguousEndpoints(string provider, string endpoint) =>
        Assert.Throws<ArgumentException>(() => ModelEvaluationRunner.ValidateLocalConnection(provider, new(endpoint)));

    [Theory]
    [InlineData("news")]
    [InlineData("staff")]
    [InlineData("assets")]
    public void EvaluationFixture_UsesAuthorizedCSharpDataAndProtectsEditsAcrossFollowup(string fixture)
    {
        ExampleCatalog catalog = new(); EvaluationData data = new(catalog, fixture);
        foreach (OutputDataSet set in catalog.Get(fixture).Data) Assert.Equal(set, data.Read(set.Id));
        int authorizedCalls = data.Calls;
        Assert.Throws<ArgumentException>(() => data.Read("unrelated-private-records")); Assert.Equal(authorizedCalls, data.Calls);
        ModelEvaluationRunner.ValidateLocalConnection("Ollama", new("http://127.0.0.1:11434/v1/"));
        ServiceCollection services = new(); services.AddIntentBlazor();
        using ServiceProvider root = services.BuildServiceProvider(); using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>(); OutputContent seed = ModelEvaluationRunner.WithNotes(catalog.Get(fixture));
        Assert.True(session.Open(seed).Success); Assert.True(ModelEvaluationRunner.ExerciseSelectionAndDraft(session).Success);
        Assert.True(session.HasDraft); Assert.Equal("Protected local evaluation note", session.Snapshot.Values["evaluationDraft"].GetString());
        var selected = seed.Document.State.Keys.Where(key => seed.Document.State[key].Kind == StateKind.RecordReference)
            .ToDictionary(key => key, key => session.Snapshot.Values[key].GetRawText());
        Assert.True(session.ApplyUpdate(seed, session.Revision).Success);
        Assert.Equal("Protected local evaluation note", session.Snapshot.Values["evaluationDraft"].GetString());
        Assert.All(selected, pair => Assert.Equal(pair.Value, session.Snapshot.Values[pair.Key].GetRawText()));
    }

    private static ReadinessReport Evaluate(ModelEvaluation[] models, bool policy = true, bool host = true) =>
        ReleaseReadiness.Evaluate(Version, Fingerprint, models, Runtime(), Packages(), policy, host);
    private static PackageReport Packages() => new(Version, new('a', 40), CheckedAt,
        PackageAudit.PackageIds.Select(id => new PackageCheck(id, true, [])).ToImmutableArray());
    private static RuntimeReport Runtime() => new(Fingerprint, ".NET 10", "X64", 4, CheckedAt,
        RuntimeWorkloads.Budgets.Select(budget => new TimingSample(budget.Key, 30, 1, 2, 200, budget.Value)).ToImmutableArray());
    private static ModelEvaluation[] Models() => new[] { "Ollama", "LM Studio", "OpenAI" }.Select(provider => new ModelEvaluation(
        Fingerprint, provider, "synthetic-report-only", "test-revision", "test-server", CheckedAt, new ModelOptions(),
        new[] { "news", "staff", "assets" }.Select(fixture =>
        {
            ModelRunReport run = new("completed", provider, "synthetic-report-only", 1, 1, 0, 200, 100, 100, TimeSpan.FromSeconds(1), 200);
            return new ModelScenarioResult(fixture, true, true, 2, true, true, true, 200, 1000, 1000, 1, run, run, []);
        }).ToImmutableArray())).ToArray();

    private sealed class ScratchDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "IntentBlazor-VerificationTest-" + Guid.NewGuid().ToString("N"));
        public ScratchDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
