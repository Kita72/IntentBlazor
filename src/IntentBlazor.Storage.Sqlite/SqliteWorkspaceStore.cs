using IntentBlazor.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntentBlazor.Storage.Sqlite;

public sealed class SqliteWorkspaceStore : IWorkspaceStore
{
    private readonly string _connectionString;
    public SqliteWorkspaceStore(string databasePath)
    {
        string full = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = full, Pooling = false, DefaultTimeout = 3 }.ToString();
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS intent_metadata (name TEXT PRIMARY KEY, value TEXT NOT NULL);
            INSERT OR IGNORE INTO intent_metadata VALUES ('schema','1');
            """;
        command.ExecuteNonQuery();
        command.CommandText = "SELECT value FROM intent_metadata WHERE name='schema'";
        if (command.ExecuteScalar()?.ToString() != "1") throw new InvalidOperationException("Incompatible IntentBlazor SQLite schema.");
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS intent_workspaces (
                owner TEXT NOT NULL, key TEXT NOT NULL, version INTEGER NOT NULL, payload TEXT NOT NULL, PRIMARY KEY(owner,key));
            CREATE TABLE IF NOT EXISTS intent_actions (
                owner TEXT NOT NULL, key TEXT NOT NULL, request TEXT NOT NULL, fingerprint TEXT NOT NULL, result TEXT NULL,
                PRIMARY KEY(owner,key,request));
            """;
        command.ExecuteNonQuery();
    }

    public ValueTask<StoredWorkspace?> LoadAsync(string ownerId, string key, CancellationToken cancellationToken = default)
    {
        Check(ownerId, key, cancellationToken);
        using SqliteConnection connection = Open();
        using SqliteCommand command = Command(connection, "SELECT version,payload FROM intent_workspaces WHERE owner=$owner AND key=$key", ownerId, key);
        using SqliteDataReader reader = command.ExecuteReader();
        return ValueTask.FromResult<StoredWorkspace?>(reader.Read() ? new(reader.GetInt64(0), reader.GetString(1)) : null);
    }
    public ValueTask<bool> SaveAsync(string ownerId, string key, long expectedVersion, string json, CancellationToken cancellationToken = default)
    {
        Check(ownerId, key, cancellationToken); WorkspaceJson.CheckSize(json);
        if (expectedVersion < 0 || expectedVersion == long.MaxValue) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        using SqliteConnection connection = Open();
        using SqliteCommand command = Command(connection, expectedVersion == 0
            ? "INSERT OR IGNORE INTO intent_workspaces(owner,key,version,payload) VALUES($owner,$key,1,$json)"
            : "UPDATE intent_workspaces SET version=version+1,payload=$json WHERE owner=$owner AND key=$key AND version=$expected", ownerId, key);
        command.Parameters.AddWithValue("$expected", expectedVersion);
        command.Parameters.AddWithValue("$json", json);
        return ValueTask.FromResult(command.ExecuteNonQuery() == 1);
    }
    public ValueTask<bool> TryClaimActionAsync(string ownerId, string key, string requestId, string fingerprint, CancellationToken cancellationToken = default)
    {
        Check(ownerId, key, cancellationToken); InMemoryWorkspaceStore.CheckReceipt(requestId, fingerprint);
        using SqliteConnection connection = Open();
        using SqliteCommand command = Command(connection,
            "INSERT OR IGNORE INTO intent_actions(owner,key,request,fingerprint) VALUES($owner,$key,$request,$fingerprint)", ownerId, key);
        command.Parameters.AddWithValue("$request", requestId);
        command.Parameters.AddWithValue("$fingerprint", fingerprint);
        return ValueTask.FromResult(command.ExecuteNonQuery() == 1);
    }
    public ValueTask<ActionReceipt?> GetActionAsync(string ownerId, string key, string requestId, CancellationToken cancellationToken = default)
    {
        Check(ownerId, key, cancellationToken); InMemoryWorkspaceStore.CheckReceipt(requestId);
        using SqliteConnection connection = Open();
        using SqliteCommand command = Command(connection,
            "SELECT fingerprint,result FROM intent_actions WHERE owner=$owner AND key=$key AND request=$request", ownerId, key);
        command.Parameters.AddWithValue("$request", requestId);
        using SqliteDataReader reader = command.ExecuteReader();
        return ValueTask.FromResult<ActionReceipt?>(reader.Read() ? new(reader.GetString(0),
            reader.IsDBNull(1) ? null : WorkspaceJson.Read<ActionResult>(reader.GetString(1))) : null);
    }
    public ValueTask CompleteActionAsync(string ownerId, string key, string requestId, string fingerprint, ActionResult result, CancellationToken cancellationToken = default)
    {
        Check(ownerId, key, cancellationToken); InMemoryWorkspaceStore.CheckReceipt(requestId, fingerprint);
        using SqliteConnection connection = Open();
        using SqliteCommand command = Command(connection,
            "UPDATE intent_actions SET result=$result WHERE owner=$owner AND key=$key AND request=$request AND fingerprint=$fingerprint AND result IS NULL", ownerId, key);
        command.Parameters.AddWithValue("$request", requestId);
        command.Parameters.AddWithValue("$fingerprint", fingerprint);
        command.Parameters.AddWithValue("$result", WorkspaceJson.Write(result));
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Only a matching pending action may be completed.");
        return ValueTask.CompletedTask;
    }
    private SqliteConnection Open() { SqliteConnection connection = new(_connectionString); connection.Open(); return connection; }
    private static SqliteCommand Command(SqliteConnection connection, string sql, string owner, string key)
    {
        SqliteCommand command = connection.CreateCommand(); command.CommandText = sql;
        command.Parameters.AddWithValue("$owner", owner); command.Parameters.AddWithValue("$key", key); return command;
    }
    private static void Check(string owner, string key, CancellationToken token)
    { token.ThrowIfCancellationRequested(); WorkspaceJson.CheckAddress(owner, key); }
}

public static class SqliteWorkspaceRegistration
{
    public static IServiceCollection AddSqliteWorkspaces(this IServiceCollection services, string databasePath)
    {
        services.AddIntentBlazor();
        services.Replace(ServiceDescriptor.Singleton<IWorkspaceStore>(_ => new SqliteWorkspaceStore(databasePath)));
        return services;
    }
}
