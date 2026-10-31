using System.Collections.Immutable;
using System.Diagnostics;
using IntentBlazor.AI;
using IntentBlazor.Blazor;
using IntentBlazor.Core;
using IntentBlazor.Examples;
using IntentBlazor.Runtime;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace IntentBlazor.Verification;

public sealed record ModelScenarioResult(string Fixture, bool InitialAccepted, bool FollowupAccepted, int ObservedDataCalls,
    bool LocalInteraction, bool DraftPreserved, bool SelectionPreserved, int RenderedCharacters, double FirstUsefulOutputMilliseconds,
    double FollowupMilliseconds, double LocalInteractionMilliseconds, ModelRunReport? InitialRun, ModelRunReport? FollowupRun,
    ImmutableArray<string> Errors)
{
    public ImmutableArray<string> Diagnostics { get; init; } = [];
    public bool Passed => InitialAccepted && FollowupAccepted && ObservedDataCalls >= 2 && LocalInteraction && DraftPreserved && SelectionPreserved &&
        InitialRun?.Status == "completed" && FollowupRun?.Status == "completed" && RenderedCharacters > 0 && !Errors.IsDefault && Errors.IsEmpty;
}
public sealed record ModelEvaluation(string SourceFingerprint, string Provider, string Model, string ModelRevision,
    string ServerVersion, DateTimeOffset CheckedAt, ModelOptions Options, ImmutableArray<ModelScenarioResult> Scenarios)
{
    public string DataAccess { get; init; } = "native-tools";
    public string EvaluationProtocol { get; init; } = "1";
}

public sealed class EvaluationData(ExampleCatalog catalog, string fixture) : IOutputDataSource
{
    public int Calls { get; private set; }
    [AiDataTool("fixture_records", "Read one authorized synthetic dataset named in the current fixture document. No application writes.")]
    public OutputDataSet Read(string dataSetId)
    {
        OutputDataSet data = catalog.Get(fixture).Data.SingleOrDefault(set => set.Id == dataSetId)
            ?? throw new ArgumentException("That dataset is not authorized for this synthetic fixture.");
        Calls++;
        return data;
    }
    public ValueTask<ImmutableArray<OutputDataSet>> LoadAsync(WorkspaceSnapshot workspace, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(catalog.Get(fixture).Data.Select(set => Read(set.Id)).ToImmutableArray());
    }
}

public static class ModelEvaluationRunner
{
    public static async Task<ModelEvaluation> RunLocalAsync(string fingerprint, string provider, string model, string revision,
        string serverVersion, Uri endpoint, string outputDirectory, bool useJsonSchema = true, bool preload = false, CancellationToken token = default)
    {
        ValidateLocalConnection(provider, endpoint);
        if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(revision) || string.IsNullOrWhiteSpace(serverVersion))
            throw new ArgumentException("Record model identity/revision and the server version.");
        ExampleCatalog catalog = new();
        ModelOptions options = new()
        {
            Timeout = TimeSpan.FromSeconds(120),
            MaximumModelCalls = 6,
            MaximumToolCalls = 8,
            MaximumCorrections = 1,
            MaximumOutputTokens = 6000,
            MaximumReportedTokens = 48000,
            MaximumContextCharacters = 60000,
            Temperature = .1f,
            UseJsonSchema = useJsonSchema
        };
        List<ModelScenarioResult> scenarios = [];
        foreach (string fixture in new[] { "news", "staff", "assets" })
        {
            token.ThrowIfCancellationRequested();
            Console.WriteLine($"Evaluating {provider}: {fixture} (synthetic, bounded, local only)");
            ServiceCollection services = new(); services.AddLogging();
            EvaluationDiagnostics diagnostics = new(); services.AddSingleton<IHttpMessageHandlerBuilderFilter>(diagnostics);
            EvaluationData data = new(catalog, fixture); services.AddSingleton(data);
            IntentModelBuilder ai = services.AddIntentBlazorAI(next => Copy(options, next));
            if (preload) services.AddScoped<IOutputDataSource>(_ => data); else ai.AddTools<EvaluationData>();
            if (provider == "Ollama") ai.UseOllama(model, endpoint); else ai.UseLMStudio(model, endpoint);
            await using ServiceProvider container = services.BuildServiceProvider();
            await using AsyncServiceScope scope = container.CreateAsyncScope();
            AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
            ModelInputProcessor processor = scope.ServiceProvider.GetRequiredService<ModelInputProcessor>();
            OutputContent seed = WithNotes(catalog.Get(fixture));
            if (!session.Open(seed).Success) throw new InvalidOperationException("The authored evaluation seed is invalid.");
            List<string> errors = [];
            long start = Stopwatch.GetTimestamp();
            string dataInstruction = preload ? "Use the hostData already loaded by the authorized C# service for datasets " :
                "Call fixture_records to load the authorized datasets ";
            OperationResult initial = await session.SubmitAsync(
                dataInstruction + string.Join(", ", seed.Document.DataSets) +
                ". Return a complete interactive workspace using their actual records: keep the existing list/table selection and selected details, " +
                "and the visible text input bound to evaluationDraft. Keep the state and node IDs stable. You may rearrange the layout. " +
                "Use schema 0.1 and builtin 0.1. Do not invent records or perform application writes.", token);
            double firstMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            ModelRunReport? firstRun = processor.LastRun;
            bool interacted = false, preserved = false, selectionPreserved = false, followupAccepted = false;
            double localMilliseconds = 0, followupMilliseconds = 0; ModelRunReport? followupRun = null; int rendered = 0;
            if (!initial.Success) errors.AddRange(initial.Errors.Select(error => error.Code));
            if (!initial.Success) Console.WriteLine("Input outcome: " + string.Join("; ", initial.Errors.Select(error => error.Message)));
            else
            {
                var interaction = ExerciseSelectionAndDraft(session);
                interacted = interaction.Success; localMilliseconds = interaction.Milliseconds;
                if (!interacted) errors.Add("missing-or-invalid-interaction");
                else
                {
                    var selectionBefore = session.Document!.State.Where(pair => pair.Value.Kind == StateKind.RecordReference &&
                        session.Snapshot.Values[pair.Key].ValueKind == System.Text.Json.JsonValueKind.String)
                        .ToDictionary(pair => pair.Key, pair => session.Snapshot.Values[pair.Key].GetString());
                    start = Stopwatch.GetTimestamp();
                    OperationResult followup = await session.SubmitAsync(
                        (preload ? "Use the current hostData loaded by the C# service. " : "Call fixture_records again for the current authorized datasets. ") + "Show a useful follow-up arrangement. " +
                        "Keep my selected record and visible evaluationDraft input, including their existing state/node identities. " +
                        "Do not discard or hide my direct edit. Return the complete schema 0.1 document.", token);
                    followupMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds; followupRun = processor.LastRun;
                    followupAccepted = followup.Success;
                    if (!followup.Success) errors.AddRange(followup.Errors.Select(error => error.Code));
                    preserved = session.Snapshot.Values.TryGetValue("evaluationDraft", out var value) && value.GetString() == "Protected local evaluation note" &&
                        session.Snapshot.DirtyStates.Contains("evaluationDraft") && session.Document!.Nodes.Any(node => node.Kind == NodeKind.Input && node.StateKey == "evaluationDraft");
                    selectionPreserved = selectionBefore.Count > 0 && selectionBefore.All(pair => session.Snapshot.Values.TryGetValue(pair.Key, out var selected) &&
                        selected.ValueKind == System.Text.Json.JsonValueKind.String && selected.GetString() == pair.Value);
                }
                await using HtmlRenderer renderer = new(scope.ServiceProvider, container.GetRequiredService<ILoggerFactory>());
                rendered = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<AiOutput>()).ToHtmlString().Length);
                VerificationJson.Write(Path.Combine(outputDirectory, fixture + "-document.json"), session.Document);
            }
            if (data.Calls < 2) errors.Add("insufficient-observed-data-service-calls");
            if (initial.Success && !preserved) errors.Add("draft-not-preserved");
            ModelScenarioResult scenario = new(fixture, initial.Success, followupAccepted, data.Calls, interacted, preserved, selectionPreserved, rendered,
                firstMilliseconds, followupMilliseconds, localMilliseconds, firstRun, followupRun, [.. errors.Distinct()])
            { Diagnostics = [.. diagnostics.Observations] };
            scenarios.Add(scenario); Console.WriteLine($"{fixture}: {(scenario.Passed ? "passed" : "not verified")}; C# reads={data.Calls}; errors={string.Join(',', scenario.Errors)}");
            VerificationJson.Write(Path.Combine(outputDirectory, "partial-evaluation.json"),
                new ModelEvaluation(fingerprint, provider, model, revision, serverVersion, DateTimeOffset.UtcNow, options, [.. scenarios])
                { DataAccess = preload ? "preloaded" : "native-tools" });
        }
        return new(fingerprint, provider, model, revision, serverVersion, DateTimeOffset.UtcNow, options, [.. scenarios])
        { DataAccess = preload ? "preloaded" : "native-tools" };
    }

    public static void ValidateLocalConnection(string provider, Uri endpoint)
    {
        if (provider is not ("Ollama" or "LM Studio") || !endpoint.IsAbsoluteUri || !endpoint.IsLoopback ||
            endpoint.Scheme is not ("http" or "https") || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 ||
            endpoint.Fragment.Length != 0 || !endpoint.AbsolutePath.EndsWith('/'))
            throw new ArgumentException("This opt-in runner accepts only an explicit loopback Ollama or LM Studio endpoint, without credentials.");
    }

    public static OutputContent WithNotes(OutputContent content) => content with
    {
        Document = content.Document with
        {
            State = content.Document.State.Add("evaluationDraft", new() { Kind = StateKind.Text, InitialValue = JsonValues.From("") }),
            Nodes = content.Document.Nodes.Select(node => node.Id == content.Document.RootId ? node with { Children = node.Children.Add("evaluationNotes") } : node)
                .Append(new OutputNode { Id = "evaluationNotes", Kind = NodeKind.Input, Title = "Evaluation note", StateKey = "evaluationDraft" }).ToImmutableArray()
        }
    };

    public static (bool Success, double Milliseconds) ExerciseSelectionAndDraft(AiSession session)
    {
        long start = Stopwatch.GetTimestamp();
        OutputNode? selector = session.Document!.Nodes.FirstOrDefault(node => node.Kind is NodeKind.List or NodeKind.Table && node.InteractionId is not null && session.IsVisible(node));
        OutputNode? editor = session.Document.Nodes.FirstOrDefault(node => node.Kind == NodeKind.Input && node.StateKey == "evaluationDraft" && session.IsVisible(node));
        if (selector is null || editor is null || session.Rows(selector).IsEmpty) return (false, 0);
        string key = session.RecordKey(selector, session.Rows(selector)[0]);
        OperationResult selected = session.Dispatch(new(selector.Id, UiEventKind.Select, session.Revision) { Value = JsonValues.From(key) });
        OperationResult edited = session.Change(editor.Id, "Protected local evaluation note", session.Revision);
        bool selection = session.Document.State.Any(pair => pair.Value.Kind == StateKind.RecordReference && pair.Value.DataSetId == selector.DataSetId &&
            session.Snapshot.Values[pair.Key].ValueKind == System.Text.Json.JsonValueKind.String && session.Snapshot.Values[pair.Key].GetString() == key);
        return (selected.Success && edited.Success && selection && session.Snapshot.DirtyStates.Contains("evaluationDraft"), Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }

    private static void Copy(ModelOptions source, ModelOptions target)
    {
        target.Timeout = source.Timeout; target.MaximumModelCalls = source.MaximumModelCalls; target.MaximumToolCalls = source.MaximumToolCalls;
        target.MaximumCorrections = source.MaximumCorrections; target.MaximumOutputTokens = source.MaximumOutputTokens;
        target.MaximumReportedTokens = source.MaximumReportedTokens; target.MaximumContextCharacters = source.MaximumContextCharacters;
        target.Temperature = source.Temperature; target.UseJsonSchema = source.UseJsonSchema;
    }
}
