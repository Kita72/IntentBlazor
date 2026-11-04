using IntentBlazor.Verification;

if (args.Length == 0)
{
    Console.Error.WriteLine("Commands: packages <dir> <version> <commit> <report>; runtime <root> <report>; model-local <root> <provider> <model> <revision> <server-version> <endpoint> <report> --allow-local [plain] [preloaded] [scenario=staff]; model-openai <root> <model> <revision> <api-version> <report> --allow-paid [preloaded] [scenario=staff]; model-codex <root> <model> <revision> <cli-version> <native-executable> <report> --allow-paid [scenario=staff]; readiness <root> <version> <evidence-dir> <report> [require-ready]");
    return 2;
}
try
{
    switch (args[0])
    {
        case "packages" when args.Length == 5:
            PackageReport packages = PackageAudit.Inspect(args[1], args[2], args[3]); VerificationJson.Write(args[4], packages);
            foreach (PackageCheck package in packages.Packages) Console.WriteLine($"{package.Package}: {(package.Passed ? "passed" : string.Join("; ", package.Errors))}");
            return packages.Passed ? 0 : 1;
        case "runtime" when args.Length == 3:
            RuntimeReport runtime = await RuntimeWorkloads.RunAsync(SourceFingerprint.Compute(args[1])); VerificationJson.Write(args[2], runtime);
            foreach (TimingSample sample in runtime.Samples) Console.WriteLine($"{sample.Workload}: p95={sample.P95Milliseconds:F2} ms; budget={sample.InitialBudgetMilliseconds:F0} ms");
            return RuntimeWorkloads.MeetsBudgets(runtime) ? 0 : 1;
        case "model-local" when args.Length is >= 8 and <= 12:
            string? scenarioFilter = ParseOptions(args.Skip(8), "--allow-local", "plain", "preloaded");
            if (!args.Skip(8).Contains("--allow-local")) throw new ArgumentException("Local inference is deferred. An approved suitable host must explicitly use --allow-local.");
            string reportPath = Path.GetFullPath(args[7]);
            ModelEvaluation model = await ModelEvaluationRunner.RunLocalAsync(SourceFingerprint.Compute(args[1]), args[2], args[3], args[4], args[5],
                new(args[6], UriKind.Absolute), Path.Combine(Path.GetDirectoryName(reportPath)!, Path.GetFileNameWithoutExtension(reportPath)),
                !args.Skip(8).Contains("plain"), preload: args.Skip(8).Contains("preloaded"), scenarioFilter: scenarioFilter);
            VerificationJson.Write(reportPath, model); return model.Passed ? 0 : 1;
        case "model-openai" when args.Length is >= 6 and <= 9:
            string? openaiScenario = ParseOptions(args.Skip(6), "--allow-paid", "preloaded");
            EvaluationConnection openai = EvaluationConnection.OpenAI(args[2], args[3], args[4], _ =>
                Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new InvalidOperationException("Configure the approved host credential outside source."),
                liveApproved: args.Skip(6).Contains("--allow-paid"));
            return await EvaluateAsync(openai, args[1], args[5], args.Skip(6).Contains("preloaded"), openaiScenario);
        case "model-codex" when args.Length is >= 7 and <= 9:
            string? codexScenario = ParseOptions(args.Skip(7), "--allow-paid");
            EvaluationConnection codex = EvaluationConnection.Codex(args[2], args[3], args[4], args[5],
                liveApproved: args.Skip(7).Contains("--allow-paid"));
            return await EvaluateAsync(codex, args[1], args[6], preload: true, codexScenario);
        case "readiness" when args.Length is 5 or 6:
            string root = Path.GetFullPath(args[1]), evidence = Path.GetFullPath(args[3]);
            var models = new[] { "ollama.json", "lmstudio.json", "openai.json" }
                .Select(file => ReleaseReadiness.ReadProviderObservation(Path.Combine(evidence, file))).OfType<ModelEvaluation>();
            ReadinessReport readiness = ReleaseReadiness.Evaluate(args[2], SourceFingerprint.Compute(root), models,
                ReleaseReadiness.ReadOptional<RuntimeReport>(Path.Combine(evidence, "runtime.json")),
                ReleaseReadiness.ReadOptional<PackageReport>(Path.Combine(evidence, "packages.json")),
                policyConfirmed: Environment.GetEnvironmentVariable("INTENT_NUGET_POLICY_CONFIRMED") == "true",
                hostVerified: Environment.GetEnvironmentVariable("INTENT_PUBLIC_HOST_VERIFIED") == "true");
            VerificationJson.Write(args[4], readiness);
            foreach (ReleaseFinding gate in readiness.Gates) Console.WriteLine($"{gate.Gate}: {(gate.Passed ? "passed" : "pending")} — {gate.Detail}");
            foreach (ReleaseFinding observation in readiness.ProviderObservations)
                Console.WriteLine($"optional {observation.Gate}: {(observation.Passed ? "observed" : "unverified")} — {observation.Detail}");
            return args.Length == 6 && args[5] == "require-ready" && !readiness.Ready ? 1 : 0;
        default:
            Console.Error.WriteLine("Invalid command or argument count. Run without arguments for usage."); return 2;
    }
}

catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or System.Text.Json.JsonException or System.Net.Http.HttpRequestException)
{
    Console.Error.WriteLine("Verification could not complete: " + error.GetType().Name + ". No success evidence was emitted.");
    return 1;
}

static async Task<int> EvaluateAsync(EvaluationConnection connection, string root, string path, bool preload, string? scenarioFilter)
{
    string report = Path.GetFullPath(path);
    ModelEvaluation evaluation = await ModelEvaluationRunner.RunAsync(SourceFingerprint.Compute(root), connection,
        Path.Combine(Path.GetDirectoryName(report)!, Path.GetFileNameWithoutExtension(report)), preload: preload, scenarioFilter: scenarioFilter);
    VerificationJson.Write(report, evaluation);
    return evaluation.Passed ? 0 : 1;
}

static string? ParseOptions(IEnumerable<string> arguments, params string[] allowed)
{
    string? filter = null;
    foreach (string option in arguments)
    {
        if (option.StartsWith("scenario=", StringComparison.Ordinal) && filter is null)
        { filter = option[9..]; ModelEvaluationRunner.SelectScenarios(filter); }
        else if (!allowed.Contains(option)) throw new ArgumentException("Unknown or repeated evaluation option.");
    }
    return filter;
}
