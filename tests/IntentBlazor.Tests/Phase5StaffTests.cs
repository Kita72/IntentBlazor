using System.Collections.Immutable;
using System.Text.Json;
using IntentBlazor.StaffBusiness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

public sealed class Phase5StaffTests
{
    [Fact]
    public async Task TwoStaff_SaveConflictsRetainPrivateDraftsAndRequireAnExplicitReviewedRebase()
    {
        using Phase5Harness h = await Phase5Harness.CreateAsync("staff"); using Phase5Scope alex = h.Scope(); using Phase5Scope sam = h.Scope("sam");
        await alex.StartAsync(); await sam.StartAsync(); alex.Select("jobs", "job-101"); sam.Select("jobs", "job-101");
        alex.Change("notesInput", "Alex confirmed the delivery."); alex.Change("assigneeInput", "sam");
        sam.Change("notesInput", "Sam's exact private draft, still being reviewed.");
        Assert.True((await sam.Session.SubmitAsync("Keep my notes while reviewing the job")).Success);
        Assert.Equal(ActionStatus.Succeeded, (await alex.ActivateAsync("save")).Status); Assert.False(alex.Session.HasDraft);
        Assert.Equal(ActionStatus.Conflict, (await sam.ActivateAsync("save")).Status); Assert.True(sam.Session.HasDraft);
        Assert.Equal("Sam's exact private draft, still being reviewed.", TestContent.Value(sam.Session, "notes")); Assert.Equal("1", TestContent.Value(sam.Session, "recordVersion"));
        Assert.True((await sam.Session.SaveAsync()).Success);
        using Phase5Scope restored = h.Scope("sam"); await restored.StartAsync();
        Assert.Equal("Sam's exact private draft, still being reviewed.", TestContent.Value(restored.Session, "notes"));
        Assert.Contains("Alex confirmed the delivery.", (await restored.RenderAsync()).Output);
        Assert.Equal("1", TestContent.Value(restored.Session, "recordVersion")); Assert.True(restored.Session.HasDraft);
        Assert.True((await restored.Session.SubmitAsync("Refresh current saved values and operation history")).Success);
        Assert.True(restored.Session.Dispatch(new("rebase", UiEventKind.Activate, restored.Session.Revision)).Success);
        Assert.Equal("2", TestContent.Value(restored.Session, "recordVersion")); Assert.True(restored.Session.HasDraft);
        Assert.Equal(ActionStatus.Succeeded, (await restored.ActivateAsync("save")).Status); Assert.False(restored.Session.HasDraft);
        await using var db = await h.Provider.GetRequiredService<IDbContextFactory<BusinessDbContext>>().CreateDbContextAsync();
        BusinessJob job = await db.Jobs.AsNoTracking().SingleAsync(item => item.Id == "job-101");
        Assert.Equal(3, job.Version); Assert.Equal("Sam's exact private draft, still being reviewed.", job.Notes);
        Assert.Equal(2, await db.Operations.CountAsync()); Assert.Equal(2, await db.Receipts.CountAsync());
        using Phase5Scope alexReopened = h.Scope(); await alexReopened.StartAsync();
        Assert.Empty(alexReopened.Session.Archive); Assert.DoesNotContain("still being reviewed", string.Join(' ', alexReopened.Session.Archive.Select(turn => turn.Input)));
    }

    [Fact]
    public async Task ReaderCannotSaveFromGeneratedUiOrDirectHandler_AndForgedOwnersCannotWrite()
    {
        using Phase5Harness h = await Phase5Harness.CreateAsync("staff"); using Phase5Scope reader = h.Scope("riley", "Reader"); await reader.StartAsync();
        reader.Select("jobs", "job-101"); reader.Change("notesInput", "Unprivileged draft");
        Assert.Equal(ActionStatus.Rejected, (await reader.ActivateAsync("save")).Status); Assert.True(reader.Session.HasDraft);
        var handler = reader.Services.GetRequiredService<SaveAssignmentAction>();
        foreach (string owner in new[] { "riley", "alex" })
        {
            ActionContext context = Context(owner, "Direct attempt");
            Assert.False(await handler.AuthorizeAsync(context, default));
            Assert.Equal(ActionStatus.Rejected, (await handler.ExecuteAsync(context, default)).Status);
        }
        await using var db = await h.Provider.GetRequiredService<IDbContextFactory<BusinessDbContext>>().CreateDbContextAsync();
        Assert.Equal("Confirm delivery window.", (await db.Jobs.SingleAsync(item => item.Id == "job-101")).Notes);
        Assert.Empty(await db.Operations.ToListAsync()); Assert.Empty(await db.Receipts.ToListAsync());
    }

    [Fact]
    public async Task ApplicationReceipt_PreventsARepeatedWriteAndRejectsChangedPayloadAfterReopeningContexts()
    {
        using Phase5Harness h = await Phase5Harness.CreateAsync("staff"); using Phase5Scope scope = h.Scope();
        var repository = scope.Services.GetRequiredService<IStaffRepository>(); ActionContext request = Context("alex", "Receipt proof");
        Assert.Equal(ActionStatus.Succeeded, (await repository.SaveAsync(request)).Status);
        using Phase5Scope second = h.Scope(); var reopened = second.Services.GetRequiredService<IStaffRepository>();
        Assert.Equal(ActionStatus.Succeeded, (await reopened.SaveAsync(request)).Status);
        Assert.Equal(ActionStatus.Rejected, (await reopened.SaveAsync(request with { Arguments = request.Arguments.SetItem("notes", JsonValues.From("Changed payload")) })).Status);
        await using var db = await h.Provider.GetRequiredService<IDbContextFactory<BusinessDbContext>>().CreateDbContextAsync();
        Assert.Equal(2, (await db.Jobs.SingleAsync(item => item.Id == "job-101")).Version); Assert.Single(await db.Operations.ToListAsync()); Assert.Single(await db.Receipts.ToListAsync());
        Assert.True(File.Exists(Path.Combine(h.DirectoryPath, "business.sqlite"))); Assert.True(File.Exists(Path.Combine(h.DirectoryPath, "workspaces.sqlite")));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("assignee")]
    [InlineData("notes")]
    [InlineData("request")]
    public async Task DirectBusinessSave_RejectsInvalidValuesWithoutChangingRecords(string invalid)
    {
        using Phase5Harness h = await Phase5Harness.CreateAsync("staff"); using Phase5Scope scope = h.Scope(); ActionContext request = Context("alex", "Valid notes");
        request = invalid switch
        {
            "version" => request with { ExpectedVersion = "0" },
            "request" => request with { IdempotencyKey = "arbitrary" },
            "assignee" => request with { Arguments = request.Arguments.SetItem("assignee", JsonValues.From("missing-staff")) },
            _ => request with { Arguments = request.Arguments.SetItem("notes", JsonValues.From(new string('x', 4097))) }
        };
        Assert.Equal(ActionStatus.Rejected, (await scope.Services.GetRequiredService<IStaffRepository>().SaveAsync(request)).Status);
        await using var db = await h.Provider.GetRequiredService<IDbContextFactory<BusinessDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, (await db.Jobs.SingleAsync(item => item.Id == "job-101")).Version); Assert.Equal(0, await db.Operations.CountAsync());
    }
    private static ActionContext Context(string owner, string notes) => new(owner, "main", Guid.NewGuid().ToString("N"), "job-101", "1",
        ImmutableDictionary<string, JsonElement>.Empty.Add("notes", JsonValues.From(notes)).Add("assignee", JsonValues.From("sam")));
}
