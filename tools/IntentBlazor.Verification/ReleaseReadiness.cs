using System.Collections.Immutable;
using System.Text.Json;

namespace IntentBlazor.Verification;

public sealed record ReleaseFinding(string Gate, bool Passed, string Detail);
public sealed record ReadinessReport(string Version, string SourceFingerprint, DateTimeOffset CheckedAt, ImmutableArray<ReleaseFinding> Gates)
{
    public EvidenceStamp? Identity { get; init; }
    public ImmutableArray<ReleaseFinding> ProviderObservations { get; init; } = [];
    public bool Ready => !Gates.IsDefaultOrEmpty && Gates.All(gate => gate.Passed);
}

public static class ReleaseReadiness
{
    public static ReadinessReport Evaluate(EvidenceStamp candidate, IEnumerable<ModelEvaluation> models,
        RuntimeReport? runtime, PackageReport? packages, bool policyConfirmed, HostObservation? host, DateTimeOffset? checkedAt = null, RepositoryReport? repository = null)
    {
        DateTimeOffset now = checkedAt ?? DateTimeOffset.UtcNow;
        string version = candidate.Version, fingerprint = candidate.SourceFingerprint;
        ModelEvaluation[] evidence = models.ToArray();
        List<ReleaseFinding> gates = [];
        List<ReleaseFinding> observations = [];
        foreach (string provider in new[] { "Ollama", "LM Studio", "OpenAI" })
        {
            bool verified = evidence.Any(run => run.Provider == provider && run.SourceFingerprint == fingerprint &&
                run.EvaluationProtocol == ModelEvaluation.CurrentProtocol && run.Passed && run.DataAccess is "native-tools" or "preloaded" &&
                !string.IsNullOrWhiteSpace(run.ModelRevision) && !string.IsNullOrWhiteSpace(run.ServerVersion) && !run.Scenarios.IsDefault && run.Scenarios.Length == 3 &&
                new[] { "news", "staff", "assets" }.All(fixture => run.Scenarios.Any(scenario => scenario.Fixture == fixture && scenario.Passed)));
            observations.Add(new("provider:" + provider, verified, verified ? "Optional live observation: all six workflows passed for a recorded profile." :
                "No complete current live profile is recorded. Live model evaluation is optional and does not block a framework release."));
        }
        var native = evidence.Where(run => run.SourceFingerprint == fingerprint && run.EvaluationProtocol == ModelEvaluation.CurrentProtocol && run.NativeToolCoverage).ToArray();
        observations.Add(new("native-service-tools", native.Any(run => run.Provider == "OpenAI") && native.Any(run => run.Provider is "Ollama" or "LM Studio"),
            "Optional live observation of native C# reads in OpenAI and a local profile; offline framework tool-routing checks remain required in verification."));
        gates.Add(new("candidate-source", candidate.Valid && candidate.Clean, "Release requires a clean exact source revision and current fingerprint protocol."));
        gates.Add(new("repository-checks", repository?.Passed == true && repository.Identity.Matches(candidate, now),
            "Require the complete current local verification receipt, passing TRX artifacts and the tested sealed IIS bundle."));
        gates.Add(new("runtime-budgets", runtime is not null && runtime.Identity?.Matches(candidate, now) == true && runtime.CheckedAt == runtime.Identity.CheckedAt &&
            runtime.SourceFingerprint == fingerprint && runtime.Runtime?.StartsWith(".NET 10.", StringComparison.Ordinal) == true && RuntimeWorkloads.MeetsBudgets(runtime),
            "Every current deterministic workload must meet its recorded initial budget."));
        gates.Add(new("package-metadata", packages is not null && packages.Identity?.Matches(candidate, now) == true && packages.CheckedAt == packages.Identity.CheckedAt &&
            packages.SourceRevision == candidate.SourceRevision && packages.Version == version && packages.Passed, "Audit all eight packages/symbols with matching candidate identity and current artifact hashes."));
        gates.Add(new("nuget-policy", policyConfirmed, "A NuGet owner and explicitly selected publishing method must be confirmed before publication."));
        gates.Add(new("public-host", host?.Matches(candidate, now) == true, "Require matching current HTTPS health identity and recorded request/selection/draft circuit evidence; environment booleans cannot verify a host."));
        return new(version, fingerprint, now, [.. gates]) { Identity = candidate, ProviderObservations = [.. observations] };
    }

    public static ModelEvaluation? ReadProviderObservation(string path)
    {
        try
        {
            ModelEvaluation? run = ReadOptional<ModelEvaluation>(path);
            if (run is null || run.Scenarios.IsDefault || run.Applications.IsDefault ||
                run.Scenarios.Any(item => item is null) ||
                run.Applications.Any(item => item is null || item.Model is null || item.Checks is null)) return null;
            foreach (ModelScenarioResult scenario in run.Scenarios.Concat(run.Applications.Select(item => item.Model)))
                foreach (var report in new[] { scenario.InitialRun, scenario.FollowupRun })
                    if (report is not null && !report.Tools.IsDefault && report.Tools.Any(tool => tool is null)) return null;
            return run;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            // Unreadable optional evidence must neither certify a profile nor block
            // the independent framework checks. Strict readers still report errors.
            return null;
        }
    }

    public static T? ReadOptional<T>(string path)
    {
        if (!File.Exists(path)) return default;
        if (new FileInfo(path).Length > 1048576) throw new InvalidDataException("Verification evidence exceeds its size bound.");
        string json = File.ReadAllText(path);
        using (JsonDocument checkedJson = JsonDocument.Parse(json)) RejectDuplicates(checkedJson.RootElement);
        if (typeof(T) == typeof(ModelEvaluation))
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Provider evidence must be an object.");
            // Historical report fields are not a current protocol contract. Keep the file
            // intact without claiming a current observation from an older protocol.
            if (!document.RootElement.TryGetProperty("evaluationProtocol", out var protocol) ||
                protocol.ValueKind != JsonValueKind.String || protocol.GetString() != ModelEvaluation.CurrentProtocol) return default;
        }
        return JsonSerializer.Deserialize<T>(json, VerificationJson.Options) ?? throw new InvalidDataException("Verification evidence is empty.");
    }

    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw new InvalidDataException("Verification evidence contains duplicate properties."); RejectDuplicates(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (JsonElement item in value.EnumerateArray()) RejectDuplicates(item);
    }
}
