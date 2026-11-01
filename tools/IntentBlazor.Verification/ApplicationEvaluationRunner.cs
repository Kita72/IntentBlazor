using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using IntentBlazor.AI;
using IntentBlazor.AssetWorkbench;
using IntentBlazor.Blazor;
using IntentBlazor.Content;
using IntentBlazor.Core;
using IntentBlazor.ExampleHost;
using IntentBlazor.GamingNews;
using IntentBlazor.Runtime;
using IntentBlazor.StaffBusiness;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace IntentBlazor.Verification;

public sealed record ApplicationScenarioResult(ModelScenarioResult Model, ImmutableDictionary<string, bool> Checks)
{
    public bool Passed => Model.Passed && ApplicationEvaluationRunner.RequiredChecks(Model.Fixture).All(key => Checks.TryGetValue(key, out bool value) && value) &&
        Checks.All(pair => pair.Value);
}

public static class ApplicationEvaluationRunner
{
    public static ImmutableArray<string> RequiredChecks(string application) => application switch
    {
        "news" => ["source-attribution", "bookmark", "save-comparison", "saved-attributed-records"],
        "staff" => ["peer-save", "conflict-retains-draft", "reader-denied", "reviewed-save"],
        "assets" => ["height-edit", "resize", "export-and-provenance", "original-retained", "private-download-url"],
        _ => ["unknown-application"]
    };
    public static async Task<ApplicationScenarioResult> RunAsync(string application, EvaluationConnection connection, ModelOptions options,
        bool preload, Action<IServiceCollection>? configureTestTransport = null, CancellationToken token = default)
    {
        await using ApplicationEvaluationHost host = await ApplicationEvaluationHost.CreateAsync(application, connection, options, preload, configureTestTransport, token);
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        AiSession session = await ApplicationEvaluationHost.StartAsync(services, application, token);
        ApplicationEvaluationData data = services.GetRequiredService<ApplicationEvaluationData>();
        ModelInputProcessor processor = services.GetRequiredService<ModelInputProcessor>();
        Dictionary<string, bool> checks = [];
        List<string> errors = [];
        data.Workspace = session.Snapshot;
        string read = preload || !connection.NativeTools ? "Use authorized hostData" : "Call application_records for every workspace dataset";
        long start = Stopwatch.GetTimestamp();
        OperationResult initial = await session.SubmitAsync(read + ". Refresh this application workspace. Retain the existing document ID, schema, dataset references, " +
            "state/node IDs, selection bindings, editable controls and registered action buttons. Improve a heading or layout. Return the complete compact document; omit unused optional properties.", token);
        double first = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        ModelRunReport? firstRun = processor.LastRun;
        bool interacted = false, followupAccepted = false, draft = false, selection = false;
        double interactionMs = 0, followupMs = 0;
        ModelRunReport? followupRun = null;
        if (!initial.Success) errors.AddRange(initial.Errors.Select(error => error.Code));
        else
        {
            start = Stopwatch.GetTimestamp();
            interacted = Interact(session, application);
            interactionMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (!interacted) errors.Add("missing-application-controls");
            else
            {
                string draftKey = application switch { "news" => "comparisonArticle", "staff" => "notes", _ => "targetWidth" };
                string selectionKey = application switch { "news" => "selectedArticle", "staff" => "selectedJob", _ => "selectedArtifact" };
                string draftValue = session.Snapshot.Values[draftKey].GetRawText(), selectedValue = session.Snapshot.Values[selectionKey].GetRawText();
                data.Workspace = session.Snapshot;
                start = Stopwatch.GetTimestamp();
                host.Diagnostics.Phase = "followup";
                OperationResult followup = await session.SubmitAsync(read + ". Present a useful follow-up. Keep my selection, exact dirty drafts, stable state/node IDs, " +
                    "visible editors, source attribution and registered action buttons. Return the full document without clearing or hiding edited fields.", token);
                followupMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                followupAccepted = followup.Success; followupRun = processor.LastRun;
                if (!followup.Success) errors.AddRange(followup.Errors.Select(error => error.Code));
                draft = session.Snapshot.Values.TryGetValue(draftKey, out var currentDraft) && currentDraft.GetRawText() == draftValue && session.Snapshot.DirtyStates.Contains(draftKey);
                selection = session.Snapshot.Values.TryGetValue(selectionKey, out var currentSelection) && currentSelection.GetRawText() == selectedValue;
                if (followupAccepted && draft && selection)
                {
                    try { await CheckActionsAsync(application, host, services, session, checks, token); }
                    catch (Exception error) when (error is InvalidOperationException or InputRejectedException or ArgumentException)
                    { errors.Add("application-action-workflow-incomplete"); }
                }
            }
        }
        string html = await RenderAsync(services);
        if (application == "news") checks["source-attribution"] = html.Contains("https://example.com/intentblazor/", StringComparison.Ordinal);
        if (checks.Count == 0) checks["actions-exercised"] = false;
        ModelScenarioResult model = new(application, initial.Success, followupAccepted, data.Calls, interacted, draft, selection, html.Length,
            first, followupMs, interactionMs, firstRun, followupRun, [.. errors.Distinct()])
        { Diagnostics = [.. host.Diagnostics.Observations], Requests = [.. host.Diagnostics.Requests] };
        return new(model, checks.ToImmutableDictionary());
    }

    public static bool Interact(AiSession session, string application)
    {
        string selectedState = application switch { "news" => "selectedArticle", "staff" => "selectedJob", _ => "selectedArtifact" };
        OutputNode? selector = session.Document!.Nodes.FirstOrDefault(node => node.SelectionState == selectedState && node.Kind is NodeKind.List or NodeKind.Table or NodeKind.Gallery);
        OperationResult select;
        if (application == "assets" && session.Document.Nodes.FirstOrDefault(node => node.Extension?.Pack == "assets") is { } versions)
        {
            string id = session.Artifacts[0].Id;
            select = session.DispatchExtension(versions.Id, session.Revision, new AssetVersionEvent(id, id, true));
        }
        else if (selector is not null && !session.Rows(selector).IsEmpty)
            select = session.Dispatch(new(selector.Id, UiEventKind.Select, session.Revision) { Value = JsonValues.From(session.RecordKey(selector, session.Rows(selector)[0])) });
        else return false;
        if (!select.Success) return false;
        string key = application switch { "news" => "comparisonArticle", "staff" => "notes", _ => "targetWidth" };
        OutputNode? editor = session.Document.Nodes.FirstOrDefault(node => node.StateKey == key && node.Kind is NodeKind.Input or NodeKind.Select);
        if (editor is null) return false;
        string value = application switch
        {
            "news" => session.Data.Single(set => set.Id == "articles").Rows.Select(row => row["id"].GetString()!).First(id => id != session.Snapshot.Values[selectedState].GetString()),
            "staff" => "Protected synthetic assignment note",
            _ => "16"
        };
        return session.Change(editor.Id, value, session.Revision).Success;
    }

    private static async Task CheckActionsAsync(string application, ApplicationEvaluationHost host, IServiceProvider services,
        AiSession session, Dictionary<string, bool> checks, CancellationToken token)
    {
        EvaluationActor actor = services.GetRequiredService<EvaluationActor>();
        string owner = actor.Current!.Id;
        if (application == "news")
        {
            checks["bookmark"] = (await ActivateAsync(session, "bookmark_article", token)).Status == ActionStatus.Succeeded;
            checks["save-comparison"] = (await ActivateAsync(session, "save_comparison", token)).Status == ActionStatus.Succeeded;
            var library = services.GetRequiredService<NewsLibrary>();
            checks["saved-attributed-records"] = library.Bookmarks(owner).Length == 1 && library.Comparisons(owner).Length == 1;
        }
        else if (application == "staff")
        {
            string notes = session.Snapshot.Values["notes"].GetString()!;
            await using (AsyncServiceScope other = host.Services.CreateAsyncScope())
            {
                other.ServiceProvider.GetRequiredService<EvaluationActor>().Current = new("evaluation-sam", "Synthetic peer", ImmutableHashSet.Create("Dispatcher"));
                ActionContext competing = new("evaluation-sam", "evaluation", Guid.NewGuid().ToString("N"), session.Snapshot.Values["selectedJob"].GetString()!,
                    JsonValues.Display(session.Snapshot.Values["recordVersion"]), ImmutableDictionary<string, JsonElement>.Empty.Add("notes", JsonValues.From("Other staff saved first"))
                    .Add("assignee", JsonValues.From("sam")));
                checks["peer-save"] = (await other.ServiceProvider.GetRequiredService<IStaffRepository>().SaveAsync(competing, token)).Status == ActionStatus.Succeeded;
            }
            checks["conflict-retains-draft"] = (await ActivateAsync(session, "save_assignment", token)).Status == ActionStatus.Conflict &&
                session.Snapshot.Values["notes"].GetString() == notes && session.Snapshot.DirtyStates.Contains("notes");
            actor.Current = actor.Current with { Roles = ImmutableHashSet.Create("Reader") };
            checks["reader-denied"] = (await ActivateAsync(session, "save_assignment", token)).Status == ActionStatus.Rejected && session.Snapshot.Values["notes"].GetString() == notes;
            actor.Current = actor.Current with { Roles = ImmutableHashSet.Create("Dispatcher") };
            var current = await services.GetRequiredService<IExampleScenario>().LoadAsync(session.Snapshot, token);
            bool refreshed = session.ApplyUpdate(new() { Document = session.Document!, Data = current }, session.Revision).Success;
            OutputInteraction? rebase = session.Document!.Interactions.FirstOrDefault(item => item.Event == UiEventKind.Activate && item.Action is null &&
                item.Assignments.Any(assignment => assignment.StateKey == "recordVersion" && assignment.Value.Source == BindingSource.SelectedField));
            OutputNode? reviewButton = session.Document.Nodes.FirstOrDefault(node => node.Kind == NodeKind.Button && node.InteractionId == rebase?.Id);
            checks["reviewed-save"] = refreshed && reviewButton is not null && session.Dispatch(new(reviewButton.Id, UiEventKind.Activate, session.Revision)).Success &&
                (await ActivateAsync(session, "save_assignment", token)).Status == ActionStatus.Succeeded && !session.Snapshot.DirtyStates.Contains("notes");
        }
        else
        {
            IWorkspaceContent content = services.GetRequiredService<IWorkspaceContent>();
            IArtifactStore store = services.GetRequiredService<IArtifactStore>();
            ArtifactReference original = session.Artifacts.Single(item => item.Id == session.Snapshot.Values["selectedArtifact"].GetString());
            byte[] before = await store.ReadAsync(owner, original, token) ?? throw new InvalidOperationException("The original is unavailable.");
            OutputNode height = session.Document!.Nodes.Single(node => node.StateKey == "targetHeight" && node.Kind == NodeKind.Input);
            checks["height-edit"] = session.Change(height.Id, "24", session.Revision).Success;
            checks["resize"] = (await ActivateAsync(session, "image_resize", token)).Status == ActionStatus.Succeeded;
            string derivedId = session.Snapshot.Values["previewArtifact"].GetString()!;
            ArtifactReference? derived = session.Artifacts.FirstOrDefault(item => item.Id == derivedId && item != original);
            if (derived is null) { checks["export-and-provenance"] = false; return; }
            byte[]? bytes = await store.ReadAsync(owner, derived, token);
            using SKBitmap? bitmap = bytes is null ? null : SKBitmap.Decode(bytes);
            checks["export-and-provenance"] = bitmap?.Width == 16 && bitmap.Height == 24 && (await content.DescribeAsync(derived, token))?.Parent == original;
            byte[] after = await store.ReadAsync(owner, original, token) ?? [];
            checks["original-retained"] = before.SequenceEqual(after);
            checks["private-download-url"] = content.Url(derived, download: true).StartsWith("/intent-artifacts/", StringComparison.Ordinal);
        }
    }

    private static async Task<ActionResult> ActivateAsync(AiSession session, string action, CancellationToken token)
    {
        var interactions = session.Document!.Interactions.Where(item => item.Action == action).Select(item => item.Id).ToHashSet();
        OutputNode? button = session.Document.Nodes.FirstOrDefault(node => node.Kind == NodeKind.Button && node.InteractionId is not null && interactions.Contains(node.InteractionId));
        ActionRequest? request = button is null ? null : session.PrepareAction(button.Id, session.Revision);
        return request is null ? new(ActionStatus.Rejected, "The required reviewed action button is unavailable.") : await session.ExecuteActionAsync(request, token);
    }

    private static async Task<string> RenderAsync(IServiceProvider services)
    {
        await using HtmlRenderer renderer = new(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<AiOutput>()).ToHtmlString());
    }
}
