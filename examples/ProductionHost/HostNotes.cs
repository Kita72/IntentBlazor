using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IntentBlazor.AI;
using IntentBlazor.Core;
using IntentBlazor.Runtime;
using Microsoft.Data.Sqlite;

namespace IntentBlazor.ProductionHost;

public sealed record HostWorkspace(string Key, string Name);

// The application's data and receipt transaction are separate from framework checkpoints.
public sealed class HostNotes
{
    private readonly HostPaths _paths;
    private readonly HostIdentity _identity;
    public HostNotes(HostPaths paths, HostIdentity identity)
    {
        _paths = paths; _identity = identity;
        using SqliteConnection connection = HostDatabase.Open(paths.Business);
        HostDatabase.Execute(connection, """
            CREATE TABLE IF NOT EXISTS host_workspaces(owner TEXT NOT NULL, key TEXT NOT NULL, name TEXT NOT NULL, PRIMARY KEY(owner,key));
            CREATE TABLE IF NOT EXISTS host_notes(owner TEXT PRIMARY KEY, body TEXT NOT NULL, version INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS host_receipts(owner TEXT NOT NULL, request TEXT NOT NULL, fingerprint TEXT NOT NULL, result TEXT NOT NULL, PRIMARY KEY(owner,request));
            """);
    }

    public async ValueTask<IReadOnlyList<HostWorkspace>> ListWorkspacesAsync(CancellationToken token = default)
    {
        HostAccount account = await _identity.RequireAsync(token);
        using SqliteConnection connection = HostDatabase.Open(_paths.Business);
        using SqliteCommand create = connection.CreateCommand();
        create.CommandText = "INSERT OR IGNORE INTO host_workspaces(owner,key,name) VALUES($owner,'notes','My notes');";
        create.Parameters.AddWithValue("$owner", account.OwnerId); create.ExecuteNonQuery();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT key,name FROM host_workspaces WHERE owner=$owner ORDER BY name,key LIMIT 100;";
        command.Parameters.AddWithValue("$owner", account.OwnerId);
        using SqliteDataReader reader = command.ExecuteReader();
        List<HostWorkspace> result = [];
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1)));
        return result;
    }

    public async ValueTask<HostWorkspace> CreateWorkspaceAsync(string name, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl)) throw new ArgumentException("Use a workspace name of 1 to 80 characters.");
        HostAccount account = await _identity.RequireAsync(token);
        await ListWorkspacesAsync(token);
        if ((await _identity.RequireAsync(token)).OwnerId != account.OwnerId) throw new InputRejectedException("The current account changed while creating a workspace.");
        using SqliteConnection connection = HostDatabase.Open(_paths.Business);
        using SqliteCommand command = connection.CreateCommand();
        HostWorkspace workspace = new(Guid.NewGuid().ToString("N"), name.Trim());
        command.CommandText = """
            INSERT INTO host_workspaces(owner,key,name) SELECT $owner,$key,$name
            WHERE (SELECT COUNT(*) FROM host_workspaces WHERE owner=$owner) < 100;
            """;
        command.Parameters.AddWithValue("$owner", account.OwnerId); command.Parameters.AddWithValue("$key", workspace.Key); command.Parameters.AddWithValue("$name", workspace.Name);
        if (command.ExecuteNonQuery() != 1) throw new InputRejectedException("The host workspace limit is reached.");
        return workspace;
    }

    [AiDataTool("read_private_notes", "Read the current authenticated account's private note. No identity argument is accepted.")]
    public async ValueTask<OutputDataSet> ReadAsync(CancellationToken token = default)
    {
        HostAccount account = await _identity.RequireAsync(token);
        using SqliteConnection connection = HostDatabase.Open(_paths.Business);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT body,version FROM host_notes WHERE owner=$owner;"; command.Parameters.AddWithValue("$owner", account.OwnerId);
        using SqliteDataReader reader = command.ExecuteReader();
        bool exists = reader.Read();
        return Data(exists ? reader.GetString(0) : "Write a private note, then choose Save note and confirm.", exists ? reader.GetInt64(1) : 0);
    }

    public async ValueTask<ActionResult> SaveAsync(ActionContext context, CancellationToken token = default)
    {
        HostAccount? account = await _identity.CurrentAsync(token);
        if (account is not { CanEdit: true } || account.OwnerId != context.OwnerId || context.ResourceId != "note-1")
            return new(ActionStatus.Rejected, "Current host permission to edit this note is required.");
        try { WorkspaceJson.CheckAddress(account.OwnerId, context.WorkspaceKey); }
        catch (ArgumentException) { return new(ActionStatus.Rejected, "A valid private workspace is required."); }
        if (string.IsNullOrWhiteSpace(context.IdempotencyKey) || context.IdempotencyKey.Length > 128 || context.IdempotencyKey.Any(char.IsControl))
            return new(ActionStatus.Rejected, "A bounded business request identifier is required.");
        if (!context.Arguments.TryGetValue("body", out var body) || body.ValueKind != System.Text.Json.JsonValueKind.String ||
            body.GetString() is not { Length: <= 512 } text || text.Contains('\0') ||
            !long.TryParse(context.ExpectedVersion, NumberStyles.None, CultureInfo.InvariantCulture, out long expected) || expected < 0 || expected == long.MaxValue)
            return new(ActionStatus.Rejected, "Use a bounded note and a current record version.");
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context.WorkspaceKey + "\0" + context.ResourceId + "\0" + context.ExpectedVersion + "\0" + text)));
        using SqliteConnection connection = HostDatabase.Open(_paths.Business);
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand prior = connection.CreateCommand(); prior.Transaction = transaction;
        prior.CommandText = "SELECT fingerprint,result FROM host_receipts WHERE owner=$owner AND request=$request;";
        prior.Parameters.AddWithValue("$owner", account.OwnerId); prior.Parameters.AddWithValue("$request", context.IdempotencyKey);
        using (SqliteDataReader reader = prior.ExecuteReader())
        {
            if (reader.Read()) return reader.GetString(0) == fingerprint ? WorkspaceJson.Read<ActionResult>(reader.GetString(1))
                : new(ActionStatus.Rejected, "This business request was already used with different arguments.");
        }
        using SqliteCommand write = connection.CreateCommand(); write.Transaction = transaction;
        write.CommandText = expected == 0
            ? "INSERT OR IGNORE INTO host_notes(owner,body,version) VALUES($owner,$body,1);"
            : "UPDATE host_notes SET body=$body,version=version+1 WHERE owner=$owner AND version=$expected;";
        write.Parameters.AddWithValue("$owner", account.OwnerId); write.Parameters.AddWithValue("$body", text); write.Parameters.AddWithValue("$expected", expected);
        bool saved = write.ExecuteNonQuery() == 1;
        ActionResult result = saved ? new(ActionStatus.Succeeded, "Note saved.")
        {
            AcceptedStates = ["draft"],
            Values = ImmutableDictionary<string, System.Text.Json.JsonElement>.Empty.Add("draft", JsonValues.From(text)),
            Data = [Data(text, expected + 1)]
        } : new(ActionStatus.Conflict, "Another session saved the note. Your draft is retained; review the latest record before retrying.");
        using SqliteCommand receipt = connection.CreateCommand(); receipt.Transaction = transaction;
        receipt.CommandText = "INSERT INTO host_receipts(owner,request,fingerprint,result) VALUES($owner,$request,$fingerprint,$result);";
        receipt.Parameters.AddWithValue("$owner", account.OwnerId); receipt.Parameters.AddWithValue("$request", context.IdempotencyKey);
        receipt.Parameters.AddWithValue("$fingerprint", fingerprint); receipt.Parameters.AddWithValue("$result", WorkspaceJson.Write(result)); receipt.ExecuteNonQuery();
        transaction.Commit(); return result;
    }

    private static OutputDataSet Data(string body, long version) => new()
    {
        Id = "notes",
        KeyField = "id",
        Fields = new Dictionary<string, DataField> { ["id"] = new() { Kind = StateKind.Text }, ["body"] = new() { Kind = StateKind.Text }, ["version"] = new() { Kind = StateKind.Text } }.ToImmutableDictionary(),
        Rows = [new Dictionary<string, System.Text.Json.JsonElement> { ["id"] = JsonValues.From("note-1"), ["body"] = JsonValues.From(body), ["version"] = JsonValues.From(version.ToString(CultureInfo.InvariantCulture)) }.ToImmutableDictionary()]
    };
}

public sealed class HostSaveNote(HostIdentity identity, HostNotes notes) : IWorkspaceActionHandler
{
    public static ActionDefinition Definition { get; } = new("save_note", "Save note", "notes", ImmutableDictionary<string, StateDefinition>.Empty.Add("body", new() { Kind = StateKind.Text }));
    public async ValueTask<bool> AuthorizeAsync(ActionContext context, CancellationToken token) =>
        await identity.CurrentAsync(token) is { CanEdit: true } account && account.OwnerId == context.OwnerId && context.ResourceId == "note-1";
    public ValueTask<ActionResult> ExecuteAsync(ActionContext context, CancellationToken token) => notes.SaveAsync(context, token);
}
