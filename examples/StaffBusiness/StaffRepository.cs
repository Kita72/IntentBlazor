using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IntentBlazor.Core;
using IntentBlazor.ExampleHost;
using IntentBlazor.Runtime;
using Microsoft.EntityFrameworkCore;

namespace IntentBlazor.StaffBusiness;

public interface IStaffRepository
{
    ValueTask<ImmutableArray<OutputDataSet>> ReadAsync(CancellationToken token = default);
    ValueTask<ActionResult> SaveAsync(ActionContext context, CancellationToken token = default);
}
public sealed class EfStaffRepository(IDbContextFactory<BusinessDbContext> factory, IExampleActor actor, TimeProvider clock) : IStaffRepository
{
    public static async Task InitializeAsync(IDbContextFactory<BusinessDbContext> factory, CancellationToken token = default)
    {
        await using var db = await factory.CreateDbContextAsync(token); await db.Database.EnsureCreatedAsync(token);
        if (await db.Jobs.AnyAsync(token)) return;
        db.Staff.AddRange(new StaffMember { Id = "alex", Name = "Alex" }, new StaffMember { Id = "sam", Name = "Sam" });
        db.Jobs.AddRange(new BusinessJob { Id = "job-101", Title = "Workshop roof repair", Material = "Galvanized brackets", WaitingDays = 5, Notes = "Confirm delivery window.", AssigneeId = "alex" },
            new BusinessJob { Id = "job-102", Title = "Pump station maintenance", Material = "Seal kit", WaitingDays = 2, Notes = "Check supplier stock.", AssigneeId = "sam" });
        await db.SaveChangesAsync(token);
    }
    public async ValueTask<ImmutableArray<OutputDataSet>> ReadAsync(CancellationToken token = default)
    {
        if (await actor.ReadAsync(token) is null) throw new InputRejectedException("Sign in to read the synthetic business records.");
        await using var db = await factory.CreateDbContextAsync(token); return await Data(db, token);
    }
    private static async Task<ImmutableArray<OutputDataSet>> Data(BusinessDbContext db, CancellationToken token)
    {
        var jobs = await db.Jobs.AsNoTracking().OrderBy(job => job.Id).Take(100).ToListAsync(token);
        var staff = await db.Staff.AsNoTracking().OrderBy(item => item.Id).Take(100).ToListAsync(token);
        var operations = await db.Operations.AsNoTracking().OrderByDescending(item => item.UtcTicks).ThenBy(item => item.Id).Take(50).ToListAsync(token);
        var jobData = ExampleData.TextSet("jobs", jobs.Select(job => ExampleData.Row(("id", job.Id), ("title", job.Title), ("material", job.Material),
            ("waitingDays", job.WaitingDays), ("notes", job.Notes), ("assignee", job.AssigneeId), ("version", job.Version))), "id", "title", "material", "waitingDays", "notes", "assignee", "version");
        jobData = jobData with
        {
            Fields = jobData.Fields.SetItem("waitingDays", new() { Kind = StateKind.Number }).SetItem("version", new() { Kind = StateKind.Number })
            .SetItem("assignee", new() { Kind = StateKind.Text, Nullable = true })
        };
        return [jobData, ExampleData.TextSet("staff", staff.Select(item => ExampleData.Row(("id", item.Id), ("name", item.Name))), "id", "name"),
            ExampleData.TextSet("operations", operations.Select(item => ExampleData.Row(("id", item.Id), ("actor", item.OwnerId), ("job", item.JobId),
                ("description", item.Description), ("version", item.Version.ToString(CultureInfo.InvariantCulture)), ("date", new DateTimeOffset(item.UtcTicks, TimeSpan.Zero).ToString("u")))),
                "id", "actor", "job", "description", "version", "date")];
    }
    public async ValueTask<ActionResult> SaveAsync(ActionContext context, CancellationToken token = default)
    {
        ExampleActor? current = await actor.ReadAsync(token);
        if (current is null || current.Id != context.OwnerId || !current.CanAssign) return new(ActionStatus.Rejected, "This identity cannot save assignments.");
        if (!Guid.TryParseExact(context.IdempotencyKey, "N", out _) || !context.Arguments.TryGetValue("notes", out var notes) || notes.ValueKind != JsonValueKind.String || notes.GetString()!.Length > 4096 ||
            !context.Arguments.TryGetValue("assignee", out var assignee) || assignee.ValueKind is not (JsonValueKind.Null or JsonValueKind.String) ||
            !long.TryParse(context.ExpectedVersion, NumberStyles.None, CultureInfo.InvariantCulture, out long expected) || expected is < 1 or long.MaxValue)
            return new(ActionStatus.Rejected, "The assignment values or record version are invalid.");
        string? assigneeId = assignee.ValueKind == JsonValueKind.Null ? null : assignee.GetString();
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(WorkspaceJson.Write(new { context.WorkspaceKey, context.ResourceId, context.ExpectedVersion, notes, assignee }))));
        await using var db = await factory.CreateDbContextAsync(token); await using var transaction = await db.Database.BeginTransactionAsync(token);
        BusinessReceipt? prior = await db.Receipts.FindAsync([current.Id, context.IdempotencyKey], token);
        if (prior is not null) return prior.Fingerprint == fingerprint ? WorkspaceJson.Read<ActionResult>(prior.ResultJson) : new(ActionStatus.Rejected, "The application's request key was reused with different values.");
        BusinessJob? job = await db.Jobs.SingleOrDefaultAsync(item => item.Id == context.ResourceId, token);
        if (job is null || (assigneeId is not null && !await db.Staff.AnyAsync(item => item.Id == assigneeId, token))) return new(ActionStatus.Rejected, "The job or staff member is unavailable.");
        if (job.Version != expected) return new(ActionStatus.Conflict, "Another staff member saved this job. Your draft is retained. Ask to refresh, review current values/history, then choose Use current record version.");
        if (await actor.ReadAsync(token) is not { CanAssign: true } authorized || authorized.Id != current.Id) return new(ActionStatus.Rejected, "Assignment permission changed.");
        job.Notes = notes.GetString()!; job.AssigneeId = assigneeId; job.Version++;
        db.Operations.Add(new()
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = current.Id,
            RequestId = context.IdempotencyKey,
            JobId = job.Id,
            Description = $"Assignment saved to {assigneeId ?? "unassigned"}.",
            Version = job.Version,
            UtcTicks = clock.GetUtcNow().UtcTicks
        });
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { return new(ActionStatus.Conflict, "The job changed during the save. Your draft is retained."); }
        ActionResult result = new(ActionStatus.Succeeded, "The assignment and operation history were saved to the business database.")
        {
            Values = ImmutableDictionary<string, JsonElement>.Empty.Add("notes", notes.Clone()).Add("assignee", assignee.Clone()).Add("recordVersion", JsonValues.From(job.Version)),
            AcceptedStates = ["notes", "assignee", "recordVersion"],
            Data = await Data(db, token)
        };
        db.Receipts.Add(new() { OwnerId = current.Id, RequestId = context.IdempotencyKey, Fingerprint = fingerprint, ResultJson = WorkspaceJson.Write(result) });
        await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return result;
    }
}
