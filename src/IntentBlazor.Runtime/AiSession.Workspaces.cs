using System.Collections.Immutable;
using System.Text.Json;
using IntentBlazor.Core;

namespace IntentBlazor.Runtime;

public sealed partial class AiSession
{
    private ImmutableArray<ConversationTurn> _archive = [];
    private string? _ownerId, _workspaceKey;
    private long _storedVersion;
    private readonly SemaphoreSlim _persistGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Task _autoSave = Task.CompletedTask;
    private bool _saveRequested, _saveBlocked;
    private bool _ownershipInvalidated;
    public string? WorkspaceKey => _workspaceKey;
    public long StoredVersion => _storedVersion;
    public ImmutableArray<ConversationTurn> Archive => _archive;
    public ImmutableArray<ArtifactReference> Artifacts { get; private set; } = [];
    public DocumentError? PersistenceError { get; private set; }

    public OperationResult ApplyUpdate(OutputContent content, long expectedRevision)
    {
        if (_disposed || _ownershipInvalidated || expectedRevision != Revision) return Fail("stale-revision", "This update belongs to an older workspace revision or owner.");
        OutputContent copy;
        try { copy = OutputJson.ReadContent(OutputJson.SerializeContent(content)); }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        { return Fail("invalid-json", "The proposed output is not valid bounded JSON."); }
        OperationResult validation = validator.Validate(copy);
        if (!validation.Success) return Fail(validation);
        var data = copy.Data.ToDictionary(set => set.Id, StringComparer.Ordinal);
        var values = copy.Document.State.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.InitialValue);
        var baseline = values;
        foreach (var pair in _values)
        {
            bool compatible = Document is not null && copy.Document.State.TryGetValue(pair.Key, out StateDefinition? definition) &&
                Document.State[pair.Key].Kind == definition.Kind && Document.State[pair.Key].DataSetId == definition.DataSetId &&
                DocumentValidator.Matches(definition, pair.Value, data) && DocumentValidator.Matches(definition, _baseline[pair.Key], data);
            if (_dirty.Contains(pair.Key) && !compatible)
                return Fail("dirty-state", "The update would remove or invalidate an edited field. The current draft is retained.");
            if (compatible) { values = values.SetItem(pair.Key, pair.Value); baseline = baseline.SetItem(pair.Key, _baseline[pair.Key]); }
        }
        foreach (string key in _dirty)
            if (!copy.Document.Nodes.Any(node => Visible(node) && ((node.StateKey == key && node.Kind is NodeKind.Input or NodeKind.Select) ||
                (node.Extension is { } extension && validator.TryExtension(extension, out IOutputExtensionContract? contract) && contract.EditableStates(extension.Configuration).Contains(key)))))
                return Fail("dirty-state", "The update would hide an edited field. The current draft is retained.");
        _content = copy; _values = values; _baseline = baseline;
        _sorting = _sorting.Where(pair => copy.Document.Nodes.Any(node => node.Id == pair.Key && node.Kind == NodeKind.Table &&
            node.Fields.Any(field => field.Field == pair.Value.Field))).ToImmutableDictionary();
        Revision++;
        return Succeed();

        bool Visible(OutputNode node)
        {
            if (node.WhenState is { } condition && values[condition].ValueKind is JsonValueKind.Null or JsonValueKind.False) return false;
            OutputNode? parent = copy.Document.Nodes.FirstOrDefault(candidate => candidate.Children.Contains(node.Id));
            return parent is null || Visible(parent);
        }
    }

    public OperationResult SetArtifacts(ImmutableArray<ArtifactReference> references)
    {
        if (_disposed || _ownershipInvalidated) return Fail("disposed", "The session has been disposed or its owner changed.");
        if (!ValidArtifacts(references)) return Fail("artifact-reference", "Use up to 64 bounded artifact identifiers and versions.");
        Artifacts = references; Revision++; return Succeed();
    }

    public async Task<OperationResult> ConnectAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_workspaceKey == key && _ownerId is not null)
            return await OwnerAsync(cancellationToken) is null ? Fail("workspace-owner", "The current identity does not own this workspace.") : OperationResult.Ok;
        return await LoadWorkspaceAsync(key, createIfMissing: true, discardLocal: false, cancellationToken);
    }
    public Task<OperationResult> RestoreAsync(string key, bool discardLocal = false, CancellationToken cancellationToken = default) =>
        LoadWorkspaceAsync(key, createIfMissing: false, discardLocal, cancellationToken);

    private async Task<OperationResult> LoadWorkspaceAsync(string key, bool createIfMissing, bool discardLocal, CancellationToken cancellationToken)
    {
        if (_disposed || workspaceServices is null) return Fail("persistence-unavailable", "Persistence requires host identity, access, and storage services.");
        if (IsBusy || (HasDraft && !discardLocal)) return Fail("dirty-state", "Finish the current request or explicitly discard the local draft before restoring.");
        long expected = Revision;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try { await _persistGate.WaitAsync(linked.Token); }
        catch (OperationCanceledException) { return Fail("cancelled", "Workspace restoration was cancelled."); }
        try
        {
            if (Revision != expected) return Fail("stale-revision", "The local workspace changed while restoration was waiting.");
            string? owner = await OwnerAsync(linked.Token);
            if (owner is null) return Fail("workspace-owner", "A current authenticated workspace owner is required.");
            WorkspaceJson.CheckAddress(owner, key);
            StoredWorkspace? stored = await workspaceServices.Store.LoadAsync(owner, key, linked.Token);
            if (await OwnerAsync(linked.Token) != owner) return Fail("workspace-owner", "The workspace identity changed during restoration.");
            if (Revision != expected) return Fail("stale-revision", "The local workspace changed while restoration was running.");
            if (stored is null)
            {
                if (!createIfMissing) return Fail("workspace-unavailable", "The workspace is unavailable for the current owner.");
                _workspaceKey = key; _ownerId = owner; _storedVersion = 0; _saveBlocked = false;
                PersistenceError = null; QueueAutoSave(); return OperationResult.Ok;
            }
            if (stored.Version <= 0) return Fail("invalid-checkpoint", "The checkpoint version is invalid.");
            WorkspaceCheckpoint saved = WorkspaceJson.Read<WorkspaceCheckpoint>(stored.Json);
            if (saved.FormatVersion != "1" || saved.Content.Document.SchemaVersion is not ("0.1" or "0.2" or "0.3"))
                return Fail("incompatible-workspace", "This saved workspace requires an unsupported format or document schema. It has not been changed.");
            if (!ValidArtifacts(saved.Artifacts)) return Fail("invalid-checkpoint", "Saved artifact references are invalid.");
            WorkspaceAccess access = await workspaceServices.Access.ReauthorizeAsync(owner, saved.Content.Document.DataSets, saved.Artifacts, linked.Token);
            if (!access.Allowed || await OwnerAsync(linked.Token) != owner)
                return Fail("workspace-access", "Current access to this workspace's data or artifacts was denied.");
            if (Revision != expected) return Fail("stale-revision", "The local workspace changed while restoration was running.");
            OutputContent content = saved.Content with { Data = access.Data };
            OperationResult validation = validator.Validate(content);
            if (!validation.Success) return Fail("restore-references", "Saved references are no longer valid or available. The local workspace is retained.");
            if (!ValidState(saved, content)) return Fail("invalid-checkpoint", "The saved workspace state is inconsistent and was not restored.");
            // Normalize all JsonElements before installing immutable state.
            saved = WorkspaceJson.Read<WorkspaceCheckpoint>(WorkspaceJson.Write(saved with { Content = content }));
            _content = saved.Content; _values = saved.Values; _baseline = saved.Baseline; _dirty = saved.DirtyStates;
            _sorting = saved.Sorting; Artifacts = saved.Artifacts; _archive = saved.Archive;
            History = _archive.TakeLast(20).Select(turn => new InputTurn(turn.Input, turn.Outcome)).ToImmutableArray();
            _workspaceKey = key; _ownerId = owner; _storedVersion = stored.Version;
            _saveBlocked = false; PersistenceError = null; LastAction = null;
            _issuedActions.Clear(); Revision++;
            Changed?.Invoke(); return OperationResult.Ok;
        }
        catch (OperationCanceledException) { return Fail("cancelled", "Workspace restoration was cancelled."); }
        catch (JsonException) { return Fail("invalid-checkpoint", "The saved checkpoint is malformed, oversized, or incompatible."); }
        catch (Exception error) when (error is not OutOfMemoryException)
        { return Fail("persistence-failed", "The workspace store or access service failed. The current workspace is retained."); }
        finally { _persistGate.Release(); }
    }

    public async Task<OperationResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || workspaceServices is null || _workspaceKey is null || _content is null)
            return PersistenceFailure("persistence-unavailable", "Connect an owner-scoped workspace before saving a checkpoint.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try { await _persistGate.WaitAsync(linked.Token); }
        catch (OperationCanceledException) { return PersistenceFailure("cancelled", "Checkpoint saving was cancelled."); }
        try
        {
            string? owner = await OwnerAsync(linked.Token);
            if (owner is null) return PersistenceFailure("workspace-owner", "The current identity does not own this workspace.");
            WorkspaceCheckpoint checkpoint = new()
            {
                Content = _content,
                Values = _values,
                Baseline = _baseline,
                Sorting = _sorting,
                DirtyStates = _dirty,
                Artifacts = Artifacts,
                Archive = _archive
            };
            WorkspaceAccess access = await workspaceServices.Access.ReauthorizeAsync(owner, checkpoint.Content.Document.DataSets, checkpoint.Artifacts, linked.Token);
            if (!access.Allowed || await OwnerAsync(linked.Token) != owner)
                return PersistenceFailure("workspace-access", "Current access to the workspace references was denied.");
            if (!validator.Validate(checkpoint.Content with { Data = access.Data }).Success || !ValidState(checkpoint, checkpoint.Content with { Data = access.Data }))
                return PersistenceFailure("restore-references", "Workspace references changed. The local draft is retained.");
            string json = WorkspaceJson.Write(checkpoint);
            if (!await workspaceServices.Store.SaveAsync(owner, _workspaceKey, _storedVersion, json, linked.Token))
            {
                _saveBlocked = true;
                return PersistenceFailure("workspace-conflict", "Another circuit saved this workspace. The local draft is retained; restore explicitly or fork it to a new key.");
            }
            _storedVersion++; PersistenceError = null; Changed?.Invoke(); return OperationResult.Ok;
        }
        catch (OperationCanceledException) { return PersistenceFailure("cancelled", "Checkpoint saving was cancelled; its outcome may require reloading."); }
        catch (Exception error) when (error is not OutOfMemoryException)
        { return PersistenceFailure("persistence-failed", "The checkpoint could not be saved. The local workspace is retained."); }
        finally { _persistGate.Release(); }
    }

    public async Task<OperationResult> ForkAsync(string newKey, CancellationToken cancellationToken = default)
    {
        if (IsBusy) return Fail("busy", "Finish the current request before forking the workspace.");
        try
        {
            if (workspaceServices is null || await OwnerAsync(cancellationToken) is not { } owner)
                return Fail("workspace-owner", "An authenticated workspace owner is required.");
            WorkspaceJson.CheckAddress(owner, newKey);
            await FlushAsync();
            if (await workspaceServices.Store.LoadAsync(owner, newKey, cancellationToken) is not null)
                return Fail("workspace-conflict", "The new workspace key already exists.");
            if (IsBusy || await OwnerAsync(cancellationToken) != owner)
                return Fail("workspace-owner", "The workspace operation or identity changed while preparing the fork.");
            _workspaceKey = newKey; _ownerId = owner; _storedVersion = 0; _saveBlocked = false; _issuedActions.Clear();
            return await SaveAsync(cancellationToken);
        }
        catch (OperationCanceledException) { return Fail("cancelled", "Workspace forking was cancelled; the local draft is retained."); }
        catch (Exception error) when (error is not OutOfMemoryException)
        { return Fail("persistence-failed", "The workspace could not be forked. The local draft is retained."); }
    }
    public Task FlushAsync() => _autoSave;
    private void QueueAutoSave()
    {
        if (_disposed || _saveBlocked || _workspaceKey is null || _content is null || workspaceServices?.AutoSave != true) return;
        _saveRequested = true;
        if (_autoSave.IsCompleted) _autoSave = SaveLoopAsync();
    }
    private async Task SaveLoopAsync()
    {
        // Yield so local events finish before serializing, and coalesce adjacent notifications.
        await Task.Yield();
        while (_saveRequested && !_disposed && !_saveBlocked)
        {
            _saveRequested = false;
            if (!(await SaveAsync()).Success) { _saveBlocked = true; break; }
        }
    }
    private OperationResult PersistenceFailure(string code, string message)
    { PersistenceError = new(code, "", message); Changed?.Invoke(); return OperationResult.Fail(code, message); }
    private async ValueTask<string?> OwnerAsync(CancellationToken token)
    {
        if (_disposed || _ownershipInvalidated || workspaceServices is null) return null;
        string? owner = await workspaceServices.Identity.GetOwnerIdAsync(token);
        if (string.IsNullOrWhiteSpace(owner) || (_ownerId is not null && owner != _ownerId))
        {
            if (_ownerId is not null && !_ownershipInvalidated)
            {
                _ownershipInvalidated = true; _saveBlocked = true; _lifetime.Cancel(); Cancel();
                _content = null; _values = _baseline = ImmutableDictionary<string, JsonElement>.Empty;
                _sorting = ImmutableDictionary<string, SortState>.Empty; _dirty = []; _archive = []; History = []; Artifacts = [];
                LastAction = null; _issuedActions.Clear(); Revision++; Changed?.Invoke();
            }
            return null;
        }
        WorkspaceJson.CheckAddress(owner, "owner-check"); return owner;
    }
    public async ValueTask<bool> ValidateOwnerAsync(CancellationToken cancellationToken = default) =>
        _ownerId is null || await OwnerAsync(cancellationToken) is not null;
    private static bool ValidArtifacts(ImmutableArray<ArtifactReference> references) => !references.IsDefault && references.Length <= 64 &&
        references.All(item => item is not null && item.Id is { Length: > 0 and <= 128 } && item.Id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') &&
            item.Version is { Length: > 0 and <= 128 } && !item.Version.Any(char.IsControl) && item.MediaType is { Length: > 0 and <= 128 } && !item.MediaType.Any(char.IsControl)) &&
        references.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == references.Length;

    private static bool ValidState(WorkspaceCheckpoint saved, OutputContent content)
    {
        if (saved.Values is null || saved.Baseline is null || saved.Sorting is null || saved.DirtyStates is null ||
            saved.Values.Count != content.Document.State.Count || saved.Baseline.Count != content.Document.State.Count ||
            saved.Archive.IsDefault || saved.Archive.Length > 512) return false;
        var data = content.Data.ToDictionary(set => set.Id, StringComparer.Ordinal);
        if (!content.Document.State.All(pair => saved.Values.TryGetValue(pair.Key, out JsonElement value) &&
            saved.Baseline.TryGetValue(pair.Key, out JsonElement baseline) && DocumentValidator.Matches(pair.Value, value, data) && DocumentValidator.Matches(pair.Value, baseline, data))) return false;
        var dirty = saved.Values.Where(pair => pair.Value.GetRawText() != saved.Baseline[pair.Key].GetRawText()).Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
        return dirty.SetEquals(saved.DirtyStates) && saved.Sorting.All(pair => pair.Value is not null &&
            content.Document.Nodes.Any(node => node.Id == pair.Key && node.Kind == NodeKind.Table && node.Fields.Any(field => field.Field == pair.Value.Field))) &&
            saved.Archive.Select((turn, index) => turn is not null && turn.Sequence == index + 1 && turn.Input is { Length: <= 2048 } &&
                turn.Outcome is "completed" or "rejected" or "cancelled" or "failed" && (turn.OutputJson is null || turn.OutputJson.Length <= OutputJson.MaximumBytes)).All(valid => valid);
    }
}
