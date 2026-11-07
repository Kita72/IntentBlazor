using IntentBlazor.Core;
using IntentBlazor.Runtime;
using Microsoft.Data.Sqlite;

namespace IntentBlazor.ProductionHost;

public sealed record HostRequestLimits(int RequestsPerDay = 50, int ConcurrentPerOwner = 1, int ConcurrentTotal = 8);

// One coordinator per application process; accepted admissions persist across restarts.
public sealed class HostRequestBudget
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _active = new(StringComparer.Ordinal);
    private readonly HostPaths _paths;
    private readonly HostRequestLimits _limits;
    private readonly TimeProvider _clock;
    private int _total;

    public HostRequestBudget(HostPaths paths, HostRequestLimits limits, TimeProvider clock)
    {
        if (limits.RequestsPerDay is < 1 or > 10000 || limits.ConcurrentPerOwner is < 1 or > 16 ||
            limits.ConcurrentTotal < limits.ConcurrentPerOwner || limits.ConcurrentTotal > 256) throw new ArgumentException("Invalid host admission limits.");
        _paths = paths; _limits = limits; _clock = clock;
        using SqliteConnection connection = HostDatabase.Open(paths.Usage);
        HostDatabase.Execute(connection, "CREATE TABLE IF NOT EXISTS admissions(owner TEXT NOT NULL, day TEXT NOT NULL, count INTEGER NOT NULL, PRIMARY KEY(owner,day));");
    }

    public IDisposable Enter(string owner, CancellationToken token = default)
    {
        WorkspaceJson.CheckAddress(owner, "admission"); token.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_active.GetValueOrDefault(owner) >= _limits.ConcurrentPerOwner || _total >= _limits.ConcurrentTotal)
                throw new InputRejectedException("The host request concurrency limit is reached. Try again after the current request finishes.");
            using SqliteConnection connection = HostDatabase.Open(_paths.Usage);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO admissions(owner,day,count) VALUES($owner,$day,1)
                ON CONFLICT(owner,day) DO UPDATE SET count=count+1 WHERE count < $limit;
                """;
            command.Parameters.AddWithValue("$owner", owner);
            command.Parameters.AddWithValue("$day", _clock.GetUtcNow().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$limit", _limits.RequestsPerDay);
            if (command.ExecuteNonQuery() != 1) throw new InputRejectedException("The host's daily request allowance is exhausted. It resets at UTC midnight.");
            _active[owner] = _active.GetValueOrDefault(owner) + 1; _total++;
            return new Lease(this, owner);
        }
    }

    private void Release(string owner)
    {
        lock (_gate)
        {
            int count = _active[owner] - 1;
            if (count == 0) _active.Remove(owner); else _active[owner] = count;
            _total--;
        }
    }

    private sealed class Lease(HostRequestBudget budget, string owner) : IDisposable
    {
        private int _disposed;
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) budget.Release(owner); }
    }
}

public sealed class HostLimitedInputProcessor(HostIdentity identity, HostRequestBudget budget, IInputProcessor inner) : IStreamingInputProcessor
{
    public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken token) => RunAsync(input, workspace, null, token);
    public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, IProgress<InputProgress> progress, CancellationToken token) => RunAsync(input, workspace, progress, token);
    private async ValueTask<OutputContent> RunAsync(string input, WorkspaceSnapshot workspace, IProgress<InputProgress>? progress, CancellationToken token)
    {
        HostAccount account = await identity.RequireAsync(token);
        using IDisposable admission = budget.Enter(account.OwnerId, token);
        OutputContent result = inner is IStreamingInputProcessor streaming && progress is not null
            ? await streaming.ProcessAsync(input, workspace, progress, token) : await inner.ProcessAsync(input, workspace, token);
        token.ThrowIfCancellationRequested();
        if ((await identity.RequireAsync(token)).OwnerId != account.OwnerId)
            throw new InputRejectedException("The authenticated account changed while preparing output. Reopen the workspace.");
        return result;
    }
}
