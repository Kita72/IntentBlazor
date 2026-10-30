namespace IntentBlazor.Runtime;

public sealed class InMemoryWorkspaceStore : IWorkspaceStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Owner, string Key), StoredWorkspace> _workspaces = [];
    private readonly Dictionary<(string Owner, string Key, string Request), ActionReceipt> _receipts = [];

    public ValueTask<StoredWorkspace?> LoadAsync(string ownerId, string key, CancellationToken cancellationToken = default)
    {
        Check(ownerId, key, cancellationToken);
        lock (_gate) return ValueTask.FromResult(_workspaces.GetValueOrDefault((ownerId, key)));
    }
    public ValueTask<bool> SaveAsync(string ownerId, string key, long expectedVersion, string json, CancellationToken cancellationToken = default)
    {
        Check(ownerId, key, cancellationToken); WorkspaceJson.CheckSize(json);
        if (expectedVersion < 0 || expectedVersion == long.MaxValue) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        lock (_gate)
        {
            long version = _workspaces.GetValueOrDefault((ownerId, key))?.Version ?? 0;
            if (version != expectedVersion) return ValueTask.FromResult(false);
            _workspaces[(ownerId, key)] = new(checked(version + 1), json);
            return ValueTask.FromResult(true);
        }
    }
    public ValueTask<bool> TryClaimActionAsync(string ownerId, string key, string requestId, string fingerprint, CancellationToken cancellationToken = default)
    {
        Check(ownerId, key, cancellationToken); CheckReceipt(requestId, fingerprint);
        lock (_gate) return ValueTask.FromResult(_receipts.TryAdd((ownerId, key, requestId), new(fingerprint, null)));
    }
    public ValueTask<ActionReceipt?> GetActionAsync(string ownerId, string key, string requestId, CancellationToken cancellationToken = default)
    {
        Check(ownerId, key, cancellationToken); CheckReceipt(requestId);
        lock (_gate) return ValueTask.FromResult(_receipts.GetValueOrDefault((ownerId, key, requestId)));
    }
    public ValueTask CompleteActionAsync(string ownerId, string key, string requestId, string fingerprint, ActionResult result, CancellationToken cancellationToken = default)
    {
        Check(ownerId, key, cancellationToken); CheckReceipt(requestId, fingerprint);
        lock (_gate)
        {
            if (!_receipts.TryGetValue((ownerId, key, requestId), out ActionReceipt? receipt) || receipt.Fingerprint != fingerprint || receipt.Result is not null)
                throw new InvalidOperationException("Only a matching pending action may be completed.");
            _receipts[(ownerId, key, requestId)] = new(fingerprint, WorkspaceJson.Read<ActionResult>(WorkspaceJson.Write(result)));
        }
        return ValueTask.CompletedTask;
    }
    public static void CheckReceipt(string requestId, string? fingerprint = null)
    {
        if (!Guid.TryParseExact(requestId, "N", out _) || (fingerprint is not null && (fingerprint.Length != 64 || !fingerprint.All(char.IsAsciiHexDigit))))
            throw new ArgumentException("An action requires a generated request ID and SHA-256 fingerprint.");
    }
    private static void Check(string owner, string key, CancellationToken token)
    { token.ThrowIfCancellationRequested(); WorkspaceJson.CheckAddress(owner, key); }
}
