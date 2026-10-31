using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using IntentBlazor.Blazor;
using IntentBlazor.Core;
using IntentBlazor.Examples;
using IntentBlazor.Runtime;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntentBlazor.Verification;

public sealed record TimingSample(string Workload, int Iterations, double MedianMilliseconds, double P95Milliseconds, int OutputCharacters, double InitialBudgetMilliseconds)
{
    public bool WithinBudget => Iterations is >= 10 and <= 200 && OutputCharacters > 0 &&
        double.IsFinite(MedianMilliseconds) && double.IsFinite(P95Milliseconds) && MedianMilliseconds >= 0 &&
        P95Milliseconds >= MedianMilliseconds && P95Milliseconds <= InitialBudgetMilliseconds;
}
public sealed record RuntimeReport(string SourceFingerprint, string Runtime, string Architecture, int LogicalProcessors,
    DateTimeOffset CheckedAt, ImmutableArray<TimingSample> Samples);

public static class RuntimeWorkloads
{
    public static ImmutableDictionary<string, double> Budgets { get; } = new Dictionary<string, double>
    {
        ["news:open-and-render"] = 250,
        ["news:select-and-render"] = 100,
        ["staff:open-and-render"] = 250,
        ["staff:select-and-render"] = 100,
        ["assets:open-and-render"] = 250,
        ["assets:select-and-render"] = 100,
        ["250-rows:validate-and-render"] = 500,
        ["251-rows:reject-and-retain"] = 500,
        ["staff:compatible-followup-and-render"] = 250
    }.ToImmutableDictionary(StringComparer.Ordinal);

    public static bool MeetsBudgets(RuntimeReport report) => !report.Samples.IsDefault && report.Samples.Length == Budgets.Count &&
        Budgets.All(budget => report.Samples.Count(sample => sample.Workload == budget.Key && sample.InitialBudgetMilliseconds == budget.Value && sample.WithinBudget) == 1);

    public static async Task<RuntimeReport> RunAsync(string fingerprint, int iterations = 30)
    {
        if (iterations is < 10 or > 200) throw new ArgumentOutOfRangeException(nameof(iterations));
        ServiceCollection services = new(); services.AddLogging(); services.AddIntentBlazor();
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        await using HtmlRenderer renderer = new(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        List<TimingSample> samples = [];
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<AiOutput>();
            ExampleCatalog catalog = new();
            foreach (string fixture in new[] { "news", "staff", "assets" })
            {
                OutputContent content = catalog.Get(fixture);
                samples.Add(Measure(fixture + ":open-and-render", iterations, () =>
                {
                    Require(session.Open(content)); return output.ToHtmlString().Length;
                }, 250));
                OutputNode selector = content.Document.Nodes.First(node => node.Kind is NodeKind.List or NodeKind.Table && node.InteractionId is not null);
                string key = content.Data.Single(set => set.Id == selector.DataSetId).Rows[0][content.Data.Single(set => set.Id == selector.DataSetId).KeyField].GetString()!;
                samples.Add(Measure(fixture + ":select-and-render", iterations, () =>
                {
                    Require(session.Dispatch(new(selector.Id, UiEventKind.Select, session.Revision) { Value = JsonValues.From(key) }));
                    return output.ToHtmlString().Length;
                }, 100));
            }
            OutputContent large = LargeTable(250);
            samples.Add(Measure("250-rows:validate-and-render", iterations, () =>
            {
                Require(session.Open(large)); return output.ToHtmlString().Length;
            }, 500));
            long before = session.Revision;
            samples.Add(Measure("251-rows:reject-and-retain", iterations, () =>
            {
                if (session.Open(LargeTable(251)).Success || session.Revision != before)
                    throw new InvalidOperationException("The large-result boundary did not retain the accepted workspace.");
                return output.ToHtmlString().Length;
            }, 500));
            OutputContent staff = catalog.Get("staff"); Require(session.Open(staff));
            Require(session.Dispatch(new("jobs", UiEventKind.Select, session.Revision) { Value = JsonValues.From("job-101") }));
            Require(session.Change("notesInput", "Protected benchmark draft", session.Revision));
            samples.Add(Measure("staff:compatible-followup-and-render", iterations, () =>
            {
                Require(session.ApplyUpdate(staff, session.Revision));
                if (session.Snapshot.Values["notes"].GetString() != "Protected benchmark draft" || !session.HasDraft)
                    throw new InvalidOperationException("A follow-up lost the draft.");
                return output.ToHtmlString().Length;
            }, 250));
        });
        return new(fingerprint, RuntimeInformation.FrameworkDescription, RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount, DateTimeOffset.UtcNow, [.. samples]);
    }

    public static OutputContent LargeTable(int rows) => new()
    {
        Data = [new()
        {
            Id = "records", KeyField = "id",
            Fields = ImmutableDictionary<string, DataField>.Empty.Add("id", new() { Kind = StateKind.Text }).Add("label", new() { Kind = StateKind.Text }),
            Rows = Enumerable.Range(0, rows).Select(i => ImmutableDictionary<string, System.Text.Json.JsonElement>.Empty
                .Add("id", JsonValues.From("record-" + i)).Add("label", JsonValues.From("Synthetic record " + i))).ToImmutableArray()
        }],
        Document = new()
        {
            Id = "large-result",
            RootId = "table",
            DataSets = ["records"],
            Nodes = [new() { Id = "table", Kind = NodeKind.Table, Title = "Bounded synthetic records", DataSetId = "records", Fields = [new("label", "Record")] }]
        }
    };

    private static TimingSample Measure(string name, int iterations, Func<int> work, double budget)
    {
        for (int i = 0; i < 3; i++) work();
        List<double> durations = []; int characters = 0;
        for (int i = 0; i < iterations; i++)
        {
            long start = Stopwatch.GetTimestamp(); characters = work(); durations.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        durations.Sort();
        return new(name, iterations, durations[iterations / 2], durations[(int)Math.Ceiling(iterations * .95) - 1], characters, budget);
    }
    private static void Require(OperationResult result)
    { if (!result.Success) throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Code))); }
}
