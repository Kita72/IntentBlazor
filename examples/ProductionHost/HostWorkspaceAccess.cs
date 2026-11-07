using System.Collections.Immutable;
using IntentBlazor.AI;
using IntentBlazor.Content;
using IntentBlazor.Core;
using IntentBlazor.Runtime;

namespace IntentBlazor.ProductionHost;

public sealed class HostWorkspaceAccess(HostIdentity identity, HostNotes notes, IArtifactStore artifacts) : IWorkspaceAccess
{
    public async ValueTask<WorkspaceAccess> ReauthorizeAsync(string owner, ImmutableArray<string> ids, ImmutableArray<ArtifactReference> references, CancellationToken token)
    {
        if ((await identity.CurrentAsync(token))?.OwnerId != owner || ids.Any(id => id != "notes")) return new(false, []);
        try
        {
            foreach (ArtifactReference reference in references)
                if (await artifacts.DescribeAsync(owner, reference, token) is null) return new(false, []);
        }
        catch (ContentRejectedException) { return new(false, []); }
        catch (ArgumentException) { return new(false, []); }
        return new(true, ids.Contains("notes") ? [await notes.ReadAsync(token)] : []);
    }
}

public sealed class HostReadSource(HostNotes notes) : IOutputDataSource
{
    public async ValueTask<ImmutableArray<OutputDataSet>> LoadAsync(WorkspaceSnapshot workspace, CancellationToken token) => [await notes.ReadAsync(token)];
}

public sealed class HostOfflineProcessor(HostNotes notes) : IInputProcessor
{
    // An authored interactive output, not a simulated model or production identity provider.
    public async ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken token)
    {
        OutputDataSet data = await notes.ReadAsync(token);
        return new()
        {
            Data = [data],
            Document = new()
            {
                Id = "host-notes",
                SchemaVersion = "0.3",
                RootId = "root",
                DataSets = ["notes"],
                State = ImmutableDictionary<string, StateDefinition>.Empty
                    .Add("selected", new() { Kind = StateKind.RecordReference, DataSetId = "notes", InitialValue = JsonValues.From("note-1") })
                    .Add("draft", new() { Kind = StateKind.Text, InitialValue = data.Rows[0]["body"] }),
                Nodes = [
                    new() { Id = "root", Kind = NodeKind.Layout, Children = ["notice", "note", "draft", "save"] },
                    new() { Id = "notice", Kind = NodeKind.Text, Title = "Offline host integration", Value = ValueBinding.Constant("Authored output from an authorized C# service. No model call is made.") },
                    new() { Id = "note", Kind = NodeKind.Text, Title = "Current saved note", Value = ValueBinding.Selected("selected", "notes", "body") },
                    new() { Id = "draft", Kind = NodeKind.Input, Title = "Private note draft", StateKey = "draft" },
                    new() { Id = "save", Kind = NodeKind.Button, Title = "Save note", InteractionId = "saveNote" }
                ],
                Interactions = [
                    new() { Id = "saveNote", Event = UiEventKind.Activate, Action = "save_note", Resource = ValueBinding.FromState("selected"),
                        ExpectedVersion = ValueBinding.Selected("selected", "notes", "version"), Arguments = ImmutableDictionary<string, ValueBinding>.Empty.Add("body", ValueBinding.FromState("draft")) }
                ]
            }
        };
    }
}

public sealed class HostAiSetupAccess(HostIdentity identity) : IAiSetupAccess
{
    public async ValueTask<AiSetupPolicy> GetPolicyAsync(CancellationToken token) => await identity.CurrentAsync(token) is { } account
        ? new(CanView: true, CanEdit: account.CanConfigureAi, CanInspect: account.CanConfigureAi, CanProbe: false,
            MaximumOutputTokens: 1024, MaximumContextCharacters: 8000) : new();
}

public sealed class HostWorkspaceController(HostIdentity identity, HostNotes notes, AiSession session)
{
    public async Task<OperationResult> OpenAsync(string key, CancellationToken token = default)
    {
        await identity.RequireAsync(token);
        if (!(await notes.ListWorkspacesAsync(token)).Any(workspace => workspace.Key == key))
            return OperationResult.Fail("host-workspace", "Choose one of your current host workspaces.");
        return await session.ConnectAsync(key, token);
    }
}
