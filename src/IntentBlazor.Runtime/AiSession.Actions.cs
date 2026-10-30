using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IntentBlazor.Core;

namespace IntentBlazor.Runtime;

public sealed partial class AiSession
{
    private sealed record IssuedAction(ActionRequest Request, string Name);
    private readonly Dictionary<string, IssuedAction> _issuedActions = new(StringComparer.Ordinal);
    public bool IsActionRunning { get; private set; }
    public ActionResult? LastAction { get; private set; }
    public string? LastActionRequestId { get; private set; }
    public string ActionLabel(string name) => validator.Actions.TryGetValue(name, out ActionDefinition? definition) ? definition.Label : "Unavailable action";

    public ActionRequest? PrepareAction(string nodeId, long expectedRevision)
    {
        if (_disposed || _ownerId is null || _workspaceKey is null || workspaceServices is null || expectedRevision != Revision) return null;
        OutputNode? node = Document?.Nodes.FirstOrDefault(candidate => candidate.Id == nodeId);
        OutputInteraction? interaction = Document?.Interactions.FirstOrDefault(candidate => candidate.Id == node?.InteractionId);
        if (node?.Kind != NodeKind.Button || !IsVisiblePath(node) || interaction?.Action is not { } name) return null;
        IssuedAction? existing = _issuedActions.Values.FirstOrDefault(item => item.Request.NodeId == nodeId && item.Request.ExpectedRevision == expectedRevision);
        if (existing is not null) return existing.Request;
        if (_issuedActions.Count >= 128) _issuedActions.Clear();
        ActionRequest request = new(nodeId, expectedRevision, Guid.NewGuid().ToString("N"));
        _issuedActions.Add(request.RequestId, new(request, name));
        return request;
    }

    public async Task<ActionResult> ExecuteActionAsync(ActionRequest request, CancellationToken cancellationToken = default)
    {
        if (workspaceServices is null || _workspaceKey is null || _content is null || _disposed ||
            !_issuedActions.TryGetValue(request.RequestId, out IssuedAction? issued) || issued.Request != request)
            return ActionOutcome(new(ActionStatus.Rejected, "The action request was not issued by this workspace."));
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return ActionOutcome(new(ActionStatus.Rejected, "A workspace operation is already running."));
        using CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        lock (_cancellationGate) _cancellation = source;
        IsActionRunning = true; LastAction = null; LastActionRequestId = request.RequestId; Changed?.Invoke();
        bool claimed = false;
        try
        {
            string? owner = await OwnerAsync(source.Token);
            if (owner is null) return ActionOutcome(new(ActionStatus.Rejected, "The current identity does not own this workspace."));
            IWorkspaceActionHandler? handler = workspaceServices.Handler(issued.Name);
            if (handler is null) return ActionOutcome(new(ActionStatus.Rejected, "The application action is not registered."));
            // A completed in-circuit replay returns its receipt, but never invokes the handler again.
            ActionReceipt? receipt = await workspaceServices.Store.GetActionAsync(owner, _workspaceKey, request.RequestId, source.Token);
            if (await OwnerAsync(source.Token) != owner) return ActionOutcome(new(ActionStatus.Rejected, "The workspace identity changed."));
            if (request.ExpectedRevision != Revision)
                return ActionOutcome(new(ActionStatus.Rejected, receipt is null
                    ? "The action belongs to an older workspace revision." : "This action was already submitted. Inspect its stored receipt; it was not executed again."));
            OutputNode? node = Document!.Nodes.FirstOrDefault(candidate => candidate.Id == request.NodeId);
            if (node is null || !IsVisiblePath(node)) return ActionOutcome(new(ActionStatus.Rejected, "The action source is unavailable."));
            OutputInteraction interaction = Document.Interactions.Single(item => item.Id == node.InteractionId);
            if (interaction.Action != issued.Name) return ActionOutcome(new(ActionStatus.Rejected, "The action definition changed."));
            string resource = JsonValues.Display(Read(interaction.Resource!));
            string version = JsonValues.Display(Read(interaction.ExpectedVersion!));
            if (string.IsNullOrWhiteSpace(resource) || string.IsNullOrWhiteSpace(version) || version.Length > 128)
                return ActionOutcome(new(ActionStatus.Rejected, "Select a record with an expected version before saving."));
            var arguments = interaction.Arguments.ToImmutableDictionary(pair => pair.Key, pair => Read(pair.Value).Clone());
            ActionDefinition definition = validator.Actions[issued.Name];
            var data = _content.Data.ToDictionary(set => set.Id, StringComparer.Ordinal);
            if (!arguments.All(pair => DocumentValidator.Matches(definition.Parameters[pair.Key], pair.Value, data)))
                return ActionOutcome(new(ActionStatus.Rejected, "An action argument violates its registered type or range."));
            ActionContext context = new(owner, _workspaceKey, request.RequestId, resource, version, arguments);
            WorkspaceAccess access = await workspaceServices.Access.ReauthorizeAsync(owner, Document.DataSets, Artifacts, source.Token);
            OutputDataSet? authorized = access.Data.IsDefault ? null : access.Data.FirstOrDefault(set => set.Id == definition.DataSetId);
            if (!access.Allowed || authorized is null || !authorized.Rows.Any(row => row[authorized.KeyField].GetString() == resource) ||
                !await handler.AuthorizeAsync(context, source.Token) || await OwnerAsync(source.Token) != owner)
                return ActionOutcome(new(ActionStatus.Rejected, "The application denied this operation."));
            if (Revision != request.ExpectedRevision) return ActionOutcome(new(ActionStatus.Rejected, "The workspace changed while the action was authorized."));
            string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(WorkspaceJson.Write(new
            {
                name = issued.Name,
                owner,
                key = _workspaceKey,
                resource,
                version,
                arguments = arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value)
            }))));
            if (!await workspaceServices.Store.TryClaimActionAsync(owner, _workspaceKey, request.RequestId, fingerprint, source.Token))
            {
                receipt = await workspaceServices.Store.GetActionAsync(owner, _workspaceKey, request.RequestId, source.Token);
                if (await OwnerAsync(source.Token) != owner) return ActionOutcome(new(ActionStatus.Rejected, "The workspace identity changed."));
                return ActionOutcome(receipt?.Fingerprint == fingerprint && receipt.Result is { } completed ? completed :
                    new(ActionStatus.Indeterminate, "This request is pending or has a different payload. It will not be automatically executed again."));
            }
            claimed = true;
            source.Token.ThrowIfCancellationRequested();
            if (await OwnerAsync(source.Token) != owner || Revision != request.ExpectedRevision)
                return ActionOutcome(new(ActionStatus.Indeterminate, "The workspace changed after submission. Check the receipt before retrying."));
            ActionResult result = await handler.ExecuteAsync(context, source.Token);
            // After entering the application handler, cancellation/identity changes may have an uncertain side-effect outcome.
            if (source.IsCancellationRequested || await OwnerAsync(CancellationToken.None) != owner)
                return ActionOutcome(new(ActionStatus.Indeterminate, "The operation may have completed. Check its application receipt before retrying."));
            result = WorkspaceJson.Read<ActionResult>(WorkspaceJson.Write(result));
            if (!Enum.IsDefined(result.Status) || string.IsNullOrWhiteSpace(result.Message) || result.Message.Length > 500)
                result = new(ActionStatus.Indeterminate, "The application returned an invalid action result. Check the operation before retrying.");
            OutputContent? acceptedContent = null;
            ImmutableDictionary<string, JsonElement>? acceptedValues = null;
            if (result.Status == ActionStatus.Succeeded && Revision == request.ExpectedRevision)
            {
                OutputContent updated = _content with { Data = result.Data.IsEmpty ? _content.Data : result.Data };
                var updatedData = updated.Data.ToDictionary(set => set.Id, StringComparer.Ordinal);
                if (!validator.Validate(updated).Success || result.AcceptedStates.IsDefault || result.AcceptedStates.Distinct().Count() != result.AcceptedStates.Length ||
                    !result.Values.All(pair => Document!.State.TryGetValue(pair.Key, out StateDefinition? state) && DocumentValidator.Matches(state, pair.Value, updatedData)) ||
                    !result.AcceptedStates.All(_values.ContainsKey))
                    result = new(ActionStatus.Indeterminate, "The operation returned an incompatible UI update. The local draft is retained.");
                else
                {
                    var values = _values.SetItems(result.Values);
                    if (Document!.State.Any(pair => !DocumentValidator.Matches(pair.Value, values[pair.Key], updatedData)))
                        result = new(ActionStatus.Indeterminate, "The operation invalidated current references. The local draft is retained.");
                    else
                    {
                        acceptedContent = updated; acceptedValues = values;
                    }
                }
            }
            await workspaceServices.Store.CompleteActionAsync(owner, _workspaceKey, request.RequestId, fingerprint, result, CancellationToken.None);
            if (await OwnerAsync(CancellationToken.None) != owner)
                return ActionOutcome(new(ActionStatus.Indeterminate, "The operation completed for the previous owner. Open a new session for the current user."));
            if (acceptedContent is not null && acceptedValues is not null && Revision == request.ExpectedRevision)
            {
                _content = acceptedContent; _values = acceptedValues;
                foreach (string key in result.AcceptedStates)
                { _dirty = _dirty.Remove(key); _baseline = _baseline.SetItem(key, _values[key]); }
                foreach (string key in result.Values.Keys.Where(key => !_dirty.Contains(key))) _baseline = _baseline.SetItem(key, _values[key]);
                Revision++;
            }
            QueueAutoSave();
            return ActionOutcome(result);
        }
        catch (OperationCanceledException)
        {
            return ActionOutcome(new(claimed ? ActionStatus.Indeterminate : ActionStatus.Rejected, claimed
            ? "The operation was interrupted after submission. Check its receipt; it will not be replayed automatically." : "The operation was cancelled before submission."));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return ActionOutcome(new(claimed ? ActionStatus.Indeterminate : ActionStatus.Failed,
            claimed ? "The submitted operation has an uncertain outcome. Check its receipt before retrying." : "The application action could not be prepared."));
        }
        finally
        {
            lock (_cancellationGate) _cancellation = null;
            IsActionRunning = false; Interlocked.Exchange(ref _busy, 0); Changed?.Invoke();
        }
    }

    public async ValueTask<ActionReceipt?> GetActionReceiptAsync(string requestId, CancellationToken cancellationToken = default)
    {
        if (workspaceServices is null || _workspaceKey is null || await OwnerAsync(cancellationToken) is not { } owner) return null;
        WorkspaceAccess access = await workspaceServices.Access.ReauthorizeAsync(owner, Document?.DataSets ?? [], Artifacts, cancellationToken);
        if (!access.Allowed || await OwnerAsync(cancellationToken) != owner) return null;
        ActionReceipt? receipt = await workspaceServices.Store.GetActionAsync(owner, _workspaceKey, requestId, cancellationToken);
        return await OwnerAsync(cancellationToken) == owner ? receipt : null;
    }
    private ActionResult ActionOutcome(ActionResult result) { LastAction = result; Changed?.Invoke(); return result; }
}
