using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IntentBlazor.Core;
using IntentBlazor.Runtime;
using Microsoft.Data.Sqlite;

namespace IntentBlazor.Examples;

// Synthetic example domain storage, kept separate from framework checkpoint storage.
public sealed class SharedJobStore
{
    private readonly string _connection;
    private readonly ExampleCatalog _catalog;
    public SharedJobStore(string path, ExampleCatalog catalog)
    {
        _catalog = catalog;
        path = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 3 }.ToString();
        using SqliteConnection connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS demo_jobs(id TEXT PRIMARY KEY, notes TEXT NOT NULL, assignee TEXT NULL, version INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS demo_receipts(owner TEXT NOT NULL, request TEXT NOT NULL, fingerprint TEXT NOT NULL, result TEXT NOT NULL, PRIMARY KEY(owner,request));
            INSERT OR IGNORE INTO demo_jobs VALUES('job-101','Confirm delivery window.','staff-1',1);
            INSERT OR IGNORE INTO demo_jobs VALUES('job-102','Check stock at the supplier.','staff-2',1);
            """;
        command.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var connection = new SqliteConnection(_connection); connection.Open(); return connection; }
    public ImmutableArray<OutputDataSet> Read()
    { using var connection = Open(); return Read(connection, null); }
    private ImmutableArray<OutputDataSet> Read(SqliteConnection connection, SqliteTransaction? transaction)
    {
        OutputContent staff = _catalog.Get("staff");
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT id,notes,assignee,version FROM demo_jobs ORDER BY id";
        using var reader = command.ExecuteReader();
        var rows = staff.Data[0].Rows.ToBuilder();
        while (reader.Read())
        {
            int index = Enumerable.Range(0, rows.Count).Single(i => rows[i]["id"].GetString() == reader.GetString(0));
            rows[index] = rows[index].SetItem("notes", JsonValues.From(reader.GetString(1)))
                .SetItem("assignee", reader.IsDBNull(2) ? JsonValues.Null : JsonValues.From(reader.GetString(2)))
                .SetItem("version", JsonValues.From(reader.GetInt64(3)));
        }
        return [staff.Data[0] with { Fields = staff.Data[0].Fields.Add("version", new() { Kind = StateKind.Number })
            .SetItem("assignee", new() { Kind = StateKind.Text, Nullable = true }), Rows = rows.ToImmutable() }, staff.Data[1]];
    }
    public ActionResult Save(ActionContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!context.Arguments.TryGetValue("notes", out JsonElement notes) || notes.ValueKind != JsonValueKind.String || notes.GetString()!.Length > 4096 ||
            !context.Arguments.TryGetValue("assignee", out JsonElement assignee) || assignee.ValueKind is not (JsonValueKind.Null or JsonValueKind.String) ||
            (assignee.ValueKind == JsonValueKind.String && assignee.GetString() is not ("staff-1" or "staff-2")) ||
            !long.TryParse(context.ExpectedVersion, NumberStyles.None, CultureInfo.InvariantCulture, out long expected) || expected < 1 || expected == long.MaxValue)
            return new(ActionStatus.Rejected, "The application rejected the follow-up values or record version.");
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(WorkspaceJson.Write(new
        { context.WorkspaceKey, context.ResourceId, context.ExpectedVersion, notes, assignee }))));
        using SqliteConnection connection = Open(); using SqliteTransaction transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT fingerprint,result FROM demo_receipts WHERE owner=$owner AND request=$request";
        command.Parameters.AddWithValue("$owner", context.OwnerId); command.Parameters.AddWithValue("$request", context.IdempotencyKey);
        using (var reader = command.ExecuteReader())
        {
            if (reader.Read()) return reader.GetString(0) == fingerprint ? WorkspaceJson.Read<ActionResult>(reader.GetString(1)) :
                new(ActionStatus.Rejected, "The application's idempotency key was reused with a different request.");
        }
        token.ThrowIfCancellationRequested();
        command.Parameters.Clear();
        command.CommandText = "UPDATE demo_jobs SET notes=$notes,assignee=$assignee,version=version+1 WHERE id=$id AND version=$expected";
        command.Parameters.AddWithValue("$notes", notes.GetString()!); command.Parameters.AddWithValue("$assignee", assignee.ValueKind == JsonValueKind.Null ? DBNull.Value : assignee.GetString()!);
        command.Parameters.AddWithValue("$id", context.ResourceId); command.Parameters.AddWithValue("$expected", expected);
        ActionResult result = command.ExecuteNonQuery() == 1
            ? new(ActionStatus.Succeeded, "The synthetic job follow-up was saved by the application.")
            {
                Values = ImmutableDictionary<string, JsonElement>.Empty.Add("notes", notes.Clone()).Add("assignee", assignee.Clone()).Add("recordVersion", JsonValues.From(expected + 1)),
                AcceptedStates = ["notes", "assignee", "recordVersion"],
                Data = Read(connection, transaction)
            }
            : new(ActionStatus.Conflict, "The job changed since you selected it. Your draft is retained; load current records to review the conflict.");
        command.Parameters.Clear(); command.CommandText = "INSERT INTO demo_receipts VALUES($owner,$request,$fingerprint,$result)";
        command.Parameters.AddWithValue("$owner", context.OwnerId); command.Parameters.AddWithValue("$request", context.IdempotencyKey);
        command.Parameters.AddWithValue("$fingerprint", fingerprint); command.Parameters.AddWithValue("$result", WorkspaceJson.Write(result));
        command.ExecuteNonQuery(); transaction.Commit(); return result;
    }
}

public sealed class DemoPermissions { public bool CanSave { get; set; } = true; }
public sealed class StaffFollowupAction(SharedJobStore jobs, DemoPermissions permissions) : IWorkspaceActionHandler
{
    public static ActionDefinition Definition { get; } = new("save_followup", "Save follow-up", "jobs",
        ImmutableDictionary<string, StateDefinition>.Empty.Add("notes", new() { Kind = StateKind.Text })
            .Add("assignee", new() { Kind = StateKind.Text, Nullable = true }));
    public ValueTask<bool> AuthorizeAsync(ActionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(permissions.CanSave && context.OwnerId.StartsWith("demo-", StringComparison.Ordinal));
    public ValueTask<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        if (!permissions.CanSave || !context.OwnerId.StartsWith("demo-", StringComparison.Ordinal))
            return ValueTask.FromResult(new ActionResult(ActionStatus.Rejected, "The application denied this operation."));
        return ValueTask.FromResult(jobs.Save(context, cancellationToken));
    }
}

public sealed class StaffWorkspaces(ExampleCatalog catalog, SharedJobStore jobs) : IWorkspaceAccess, IInputProcessor
{
    public OutputContent Create()
    {
        OutputContent content = catalog.Get("staff");
        OutputDocument document = content.Document with
        {
            SchemaVersion = "0.2",
            State = content.Document.State.Add("recordVersion", new() { Kind = StateKind.Number, InitialValue = JsonValues.From(1), Minimum = 1 }),
            Nodes = content.Document.Nodes.Select(node => node.Id switch
            {
                "draft" => node with { Children = node.Children.Add("saveFollowup") },
                "notice" => node with { Value = ValueBinding.Constant("Synthetic shared jobs. Save runs a registered server action; drafts and checkpoints are separate from business saves.") },
                _ => node
            }).ToImmutableArray().Add(new() { Id = "saveFollowup", Kind = NodeKind.Button, Title = "Model label is replaced by the server", InteractionId = "saveFollowup" }),
            Interactions = content.Document.Interactions.Select(interaction => interaction.Id == "chooseJob"
                ? interaction with { Assignments = interaction.Assignments.Add(new("recordVersion", ValueBinding.EventField("jobs", "version"))) } : interaction)
                .ToImmutableArray().Add(new()
                {
                    Id = "saveFollowup",
                    Event = UiEventKind.Activate,
                    Action = "save_followup",
                    Resource = ValueBinding.FromState("selectedJob"),
                    ExpectedVersion = ValueBinding.FromState("recordVersion"),
                    Arguments = ImmutableDictionary<string, ValueBinding>.Empty.Add("notes", ValueBinding.FromState("notes")).Add("assignee", ValueBinding.FromState("assignee"))
                })
        };
        return new() { Document = document, Data = jobs.Read() };
    }
    public ValueTask<WorkspaceAccess> ReauthorizeAsync(string ownerId, ImmutableArray<string> dataSets,
        ImmutableArray<ArtifactReference> artifacts, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool allowed = ownerId.StartsWith("demo-", StringComparison.Ordinal) && dataSets.All(id => id is "jobs" or "staff") &&
            artifacts.All(item => item == new ArtifactReference("asset-2", "image/svg+xml", "sample-1"));
        var data = jobs.Read();
        return ValueTask.FromResult(new WorkspaceAccess(allowed, data.Where(set => dataSets.Contains(set.Id)).ToImmutableArray()));
    }
    public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OutputContent content = Create();
        return ValueTask.FromResult(content with
        {
            Document = content.Document with
            {
                Nodes = content.Document.Nodes.Select(node => node.Id == "notice" ? node with
                { Value = ValueBinding.Constant("Deterministic follow-up: " + input) } : node).ToImmutableArray()
            }
        });
    }
}
