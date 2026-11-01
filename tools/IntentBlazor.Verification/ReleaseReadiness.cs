using System.Collections.Immutable;
using System.Text.Json;

namespace IntentBlazor.Verification;

public sealed record ReleaseFinding(string Gate, bool Passed, string Detail);
public sealed record ReadinessReport(string Version, string SourceFingerprint, DateTimeOffset CheckedAt, ImmutableArray<ReleaseFinding> Gates)
{
    public bool Ready => !Gates.IsDefaultOrEmpty && Gates.All(gate => gate.Passed);
}

public static class ReleaseReadiness
{
    public static ReadinessReport Evaluate(string version, string fingerprint, IEnumerable<ModelEvaluation> models,
        RuntimeReport? runtime, PackageReport? packages, bool policyConfirmed, bool hostVerified)
    {
        ModelEvaluation[] evidence = models.ToArray();
        List<ReleaseFinding> gates = [];
        foreach (string provider in new[] { "Ollama", "LM Studio", "OpenAI" })
        {
            bool verified = evidence.Any(run => run.Provider == provider && run.SourceFingerprint == fingerprint &&
                run.EvaluationProtocol == ModelEvaluation.CurrentProtocol && run.Passed && run.DataAccess is "native-tools" or "preloaded" &&
                !string.IsNullOrWhiteSpace(run.ModelRevision) && !string.IsNullOrWhiteSpace(run.ServerVersion) && !run.Scenarios.IsDefault && run.Scenarios.Length == 3 &&
                new[] { "news", "staff", "assets" }.All(fixture => run.Scenarios.Any(scenario => scenario.Fixture == fixture && scenario.Passed)));
            gates.Add(new("provider:" + provider, verified, verified ? "All shared fixtures and real application workflows passed for a recorded profile." :
                provider == "OpenAI" ? "Live OpenAI verification is deferred; scripted tests are not live evidence." : "Local inference is deferred; no current successful six-workflow live evaluation is recorded."));
        }
        var native = evidence.Where(run => run.SourceFingerprint == fingerprint && run.EvaluationProtocol == ModelEvaluation.CurrentProtocol && run.NativeToolCoverage).ToArray();
        gates.Add(new("native-service-tools", native.Any(run => run.Provider == "OpenAI") && native.Any(run => run.Provider is "Ollama" or "LM Studio"),
            "OpenAI and at least one local profile must complete both fixture and real-app workflows with observed native C# reads."));
        gates.Add(new("runtime-budgets", runtime is not null && runtime.SourceFingerprint == fingerprint && RuntimeWorkloads.MeetsBudgets(runtime),
            "Every current deterministic workload must meet its recorded initial budget."));
        gates.Add(new("package-metadata", packages is not null && packages.Version == version && packages.Passed, "Audit all eight packages and exact-commit symbols."));
        gates.Add(new("nuget-policy", policyConfirmed, "A NuGet profile and matching trusted-publishing policy must be confirmed before publication."));
        gates.Add(new("public-host", hostVerified, hostVerified ? "Public HTTPS and an interactive circuit were verified by the maintainer." : "Public HTTPS and interactive circuit verification have not been confirmed for this candidate."));
        return new(version, fingerprint, DateTimeOffset.UtcNow, [.. gates]);
    }

    public static T? ReadOptional<T>(string path)
    {
        if (!File.Exists(path)) return default;
        if (new FileInfo(path).Length > 1048576) throw new InvalidDataException("Verification evidence exceeds its size bound.");
        string json = File.ReadAllText(path);
        if (typeof(T) == typeof(ModelEvaluation))
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Provider evidence must be an object.");
            // Historical report fields are not a current protocol contract. Keep the file
            // intact and leave its gate pending instead of trying to deserialize it as v2.
            if (!document.RootElement.TryGetProperty("evaluationProtocol", out var protocol) ||
                protocol.ValueKind != JsonValueKind.String || protocol.GetString() != ModelEvaluation.CurrentProtocol) return default;
        }
        return JsonSerializer.Deserialize<T>(json, VerificationJson.Options) ?? throw new InvalidDataException("Verification evidence is empty.");
    }
}
