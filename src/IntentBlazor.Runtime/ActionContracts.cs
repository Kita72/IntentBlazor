using System.Collections.Immutable;
using System.Text.Json;
using IntentBlazor.Core;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Runtime;

public enum ActionStatus { Succeeded, Conflict, Rejected, Failed, Indeterminate }
public sealed record ActionResult(ActionStatus Status, string Message)
{
    public ImmutableDictionary<string, JsonElement> Values { get; init; } = ImmutableDictionary<string, JsonElement>.Empty;
    public ImmutableArray<string> AcceptedStates { get; init; } = [];
    public ImmutableArray<OutputDataSet> Data { get; init; } = [];
    public ImmutableArray<ArtifactReference> Artifacts { get; init; } = [];
}
public sealed record ActionContext(string OwnerId, string WorkspaceKey, string IdempotencyKey,
    string ResourceId, string ExpectedVersion, ImmutableDictionary<string, JsonElement> Arguments)
{
    public ImmutableArray<OutputDataSet> Data { get; init; } = [];
    public ImmutableArray<ArtifactReference> Artifacts { get; init; } = [];
}
public sealed record ActionRequest(string NodeId, long ExpectedRevision, string RequestId);
public interface IWorkspaceActionHandler
{
    ValueTask<bool> AuthorizeAsync(ActionContext context, CancellationToken cancellationToken);
    ValueTask<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken);
}
public sealed record ActionRegistration(string Name, Type HandlerType);
public sealed class WorkspaceServices(IWorkspaceIdentity identity, IWorkspaceAccess access, IWorkspaceStore store,
    IEnumerable<ActionRegistration> actions, IServiceProvider services)
{
    public IWorkspaceIdentity Identity { get; } = identity;
    public IWorkspaceAccess Access { get; } = access;
    public IWorkspaceStore Store { get; } = store;
    public bool AutoSave { get; set; } = true;
    public IWorkspaceActionHandler? Handler(string name) => actions.FirstOrDefault(action => action.Name == name) is { } registration
        ? (IWorkspaceActionHandler)services.GetRequiredService(registration.HandlerType) : null;
}
