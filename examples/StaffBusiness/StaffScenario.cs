using System.Collections.Immutable;
using IntentBlazor.Core;
using IntentBlazor.ExampleHost;
using IntentBlazor.Runtime;

namespace IntentBlazor.StaffBusiness;

public sealed class StaffScenario(IStaffRepository repository, IExampleActor actor) : IExampleScenario
{
    public string InitialRequest => "Inspect jobs waiting on materials. Select a job to edit its assignment and follow-up notes.";
    public ValueTask<ImmutableArray<OutputDataSet>> LoadAsync(WorkspaceSnapshot workspace, CancellationToken token) => repository.ReadAsync(token);
    public async ValueTask<WorkspaceAccess> ReauthorizeAsync(string owner, ImmutableArray<string> ids, ImmutableArray<ArtifactReference> artifacts, CancellationToken token)
    {
        if ((await actor.ReadAsync(token))?.Id != owner || !artifacts.IsEmpty || ids.Any(id => id is not ("jobs" or "staff" or "operations"))) return new(false, []);
        return new(true, (await repository.ReadAsync(token)).Where(set => ids.Contains(set.Id)).ToImmutableArray());
    }
    public async ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken token) =>
        StaffDocuments.Create(await repository.ReadAsync(token), input, (await actor.ReadAsync(token))?.CanAssign == true);
}
public sealed class SaveAssignmentAction(IStaffRepository repository, IExampleActor actor) : IWorkspaceActionHandler
{
    public static ActionDefinition Definition { get; } = new("save_assignment", "Save assignment", "jobs", ImmutableDictionary<string, StateDefinition>.Empty
        .Add("notes", new() { Kind = StateKind.Text }).Add("assignee", new() { Kind = StateKind.Text, Nullable = true }));
    public async ValueTask<bool> AuthorizeAsync(ActionContext context, CancellationToken token) => await actor.ReadAsync(token) is { CanAssign: true } current && current.Id == context.OwnerId;
    public ValueTask<ActionResult> ExecuteAsync(ActionContext context, CancellationToken token) => repository.SaveAsync(context, token);
}
