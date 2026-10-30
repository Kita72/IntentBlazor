using System.Collections.Immutable;
using System.Text.Json;

namespace IntentBlazor.Tests;

public sealed class DurableWorkspaceTests
{
    [Theory]
    [InlineData("memory")]
    [InlineData("sqlite")]
    public async Task EditFollowupAndReconnect_RestoresExactDraftBaselineAndContext(string storage)
    {
        using WorkspaceHarness h = new(storage);
        using WorkspaceScope first = h.Scope(); AiSession session = first.Session;
        Assert.True((await session.ConnectAsync("staff-chat")).Success);
        Assert.True(session.Open(h.Staff.Create()).Success);
        TestContent.Select(session, "jobs", "job-101");
        session.Change("notesInput", "Call at noon", session.Revision);
        session.Change("assigneeInput", "staff-2", session.Revision);
        session.Dispatch(new("jobs", UiEventKind.Sort, session.Revision) { SortField = "priority", Descending = true });
        session.SetArtifacts([new("asset-2", "image/svg+xml", "sample-1")]);
        Assert.True((await session.SubmitAsync("Keep this draft and describe my next step")).Success);
        Assert.Equal("Call at noon", TestContent.Value(session, "notes"));
        Assert.True((await session.SaveAsync()).Success);
        using WorkspaceScope second = h.Scope();
        Assert.True((await second.Session.RestoreAsync("staff-chat")).Success);
        Assert.Equal("job-101", TestContent.Value(second.Session, "selectedJob"));
        Assert.Equal("Call at noon", TestContent.Value(second.Session, "notes"));
        Assert.Equal("staff-2", TestContent.Value(second.Session, "assignee"));
        Assert.Equal(session.Snapshot.DirtyStates.Order(), second.Session.Snapshot.DirtyStates.Order());
        Assert.True(second.Session.Snapshot.Sorting["jobs"].Descending);
        Assert.Equal(session.Artifacts.ToArray(), second.Session.Artifacts.ToArray());
        Assert.Equal(session.Archive.ToArray(), second.Session.Archive.ToArray());
        Assert.Equal("completed", Assert.Single(second.Session.History).Status);
        Assert.Contains("Keep this draft", second.Session.Snapshot.Conversation.Recent[0].Input);
        Assert.True(second.Session.Dispatch(new("clearDraft", UiEventKind.Activate, second.Session.Revision)).Success);
        Assert.Equal("Confirm delivery window.", TestContent.Value(second.Session, "notes"));
        Assert.Equal("staff-1", TestContent.Value(second.Session, "assignee"));
        Assert.False(second.Session.HasDraft);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("sqlite")]
    public async Task ConcurrentCheckpointConflict_RetainsDraftAndCanFork(string storage)
    {
        using WorkspaceHarness h = new(storage);
        using WorkspaceScope first = h.Scope();
        await first.Session.ConnectAsync("chat"); first.Session.Open(h.Staff.Create()); TestContent.Select(first.Session, "jobs", "job-101");
        await first.Session.SaveAsync();
        using WorkspaceScope second = h.Scope(); await second.Session.RestoreAsync("chat");
        first.Session.Change("notesInput", "First circuit", first.Session.Revision); Assert.True((await first.Session.SaveAsync()).Success);
        second.Session.Change("notesInput", "Second circuit", second.Session.Revision);
        Assert.False((await second.Session.SaveAsync()).Success);
        Assert.Equal("workspace-conflict", second.Session.PersistenceError!.Code);
        Assert.Equal("Second circuit", TestContent.Value(second.Session, "notes"));
        Assert.False((await second.Session.RestoreAsync("chat")).Success);
        Assert.True((await second.Session.ForkAsync("chat-fork")).Success);
        using WorkspaceScope restored = h.Scope(); await restored.Session.RestoreAsync("chat");
        Assert.Equal("First circuit", TestContent.Value(restored.Session, "notes"));
        await restored.Session.RestoreAsync("chat-fork", discardLocal: true);
        Assert.Equal("Second circuit", TestContent.Value(restored.Session, "notes"));
    }

    [Fact]
    public async Task OwnersAndReauthorization_PreventPrivateWorkspaceLeakage()
    {
        using WorkspaceHarness h = new(); using WorkspaceScope alice = h.Scope();
        await alice.Session.ConnectAsync("chat"); alice.Session.Open(h.Staff.Create());
        TestContent.Select(alice.Session, "jobs", "job-101"); alice.Session.Change("notesInput", "Alice private draft", alice.Session.Revision);
        await alice.Session.SaveAsync();
        using WorkspaceScope bob = h.Scope("demo-bob");
        Assert.False((await bob.Session.RestoreAsync("chat")).Success); Assert.Null(bob.Session.Document); Assert.Empty(bob.Session.Archive);
        using WorkspaceScope reader = h.Scope(); h.Access.RemoveSelectedRow = true;
        Assert.False((await reader.Session.RestoreAsync("chat")).Success); Assert.Null(reader.Session.Document);
        h.Access.RemoveSelectedRow = false; h.Access.Allowed = false;
        Assert.False((await reader.Session.RestoreAsync("chat")).Success);
        Assert.Contains("Alice private draft", (await h.Store.LoadAsync("demo-alice", "chat"))!.Json);
        alice.Identity.Owner = "demo-bob";
        Assert.False(await alice.Session.ValidateOwnerAsync()); Assert.Null(alice.Session.Document); Assert.Empty(alice.Session.Snapshot.Values);
        Assert.Empty(alice.Session.Archive);
        Assert.False(alice.Session.ApplyUpdate(h.Staff.Create(), alice.Session.Revision).Success);
        Assert.False(alice.Session.SetArtifacts([new("asset-2", "image/svg+xml", "sample-1")]).Success);
        alice.Identity.Owner = "demo-alice";
        Assert.False(await alice.Session.ValidateOwnerAsync()); Assert.Null(alice.Session.Document);
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("owner")]
    public async Task InFlightRestoration_RejectsNewEditsAndChangedOwners(string change)
    {
        using WorkspaceHarness h = new(); using WorkspaceScope original = h.Scope();
        await original.Session.ConnectAsync("saved"); original.Session.Open(h.Staff.Create());
        TestContent.Select(original.Session, "jobs", "job-101"); await original.Session.SaveAsync();
        using WorkspaceScope target = h.Scope(); await target.Session.ConnectAsync("local");
        target.Session.Open(h.Staff.Create()); TestContent.Select(target.Session, "jobs", "job-101");
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Access.BeforeResolve = async token => { entered.SetResult(); await release.Task.WaitAsync(token); };
        Task<OperationResult> restore = target.Session.RestoreAsync("saved");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (change == "edit") target.Session.Change("notesInput", "Edited while restoring", target.Session.Revision);
        else target.Identity.Owner = "demo-bob";
        release.SetResult();
        Assert.False((await restore).Success);
        if (change == "edit")
        {
            Assert.Equal("local", target.Session.WorkspaceKey);
            Assert.Equal("Edited while restoring", TestContent.Value(target.Session, "notes")); Assert.True(target.Session.HasDraft);
        }
        else { Assert.Null(target.Session.Document); Assert.Empty(target.Session.Snapshot.Values); }
    }

    [Theory]
    [InlineData("format")]
    [InlineData("schema")]
    [InlineData("values")]
    [InlineData("dirty")]
    [InlineData("duplicate-json")]
    public async Task MalformedOrIncompatibleCheckpoint_IsRejectedWithoutOverwritingLocalState(string kind)
    {
        using WorkspaceHarness h = new(); using WorkspaceScope original = h.Scope();
        await original.Session.ConnectAsync("chat"); original.Session.Open(h.Staff.Create()); await original.Session.SaveAsync();
        StoredWorkspace stored = (await h.Store.LoadAsync("demo-alice", "chat"))!;
        WorkspaceCheckpoint saved = WorkspaceJson.Read<WorkspaceCheckpoint>(stored.Json);
        string json = kind switch
        {
            "format" => WorkspaceJson.Write(saved with { FormatVersion = "future" }),
            "schema" => WorkspaceJson.Write(saved with { Content = saved.Content with { Document = saved.Content.Document with { SchemaVersion = "99" } } }),
            "values" => WorkspaceJson.Write(saved with { Values = saved.Values.SetItem("notes", JsonValues.From(12)) }),
            "dirty" => WorkspaceJson.Write(saved with { DirtyStates = ImmutableHashSet.Create("notes") }),
            _ => stored.Json.Replace("\"formatVersion\":\"1\"", "\"formatVersion\":\"1\",\"formatVersion\":\"1\"")
        };
        await h.Store.SaveAsync("demo-alice", "chat", stored.Version, json);
        using WorkspaceScope target = h.Scope(); target.Session.Open(TestContent.Catalog.Get("news")); long revision = target.Session.Revision;
        Assert.False((await target.Session.RestoreAsync("chat")).Success);
        Assert.Equal("news-reader", target.Session.Document!.Id); Assert.Equal(revision, target.Session.Revision);
    }

    [Fact]
    public async Task AutoSave_CoalescesChangesAndRestoresInANewScope()
    {
        using WorkspaceHarness h = new("sqlite"); using WorkspaceScope first = h.Scope(autoSave: true);
        await first.Session.ConnectAsync("auto"); first.Session.Open(h.Staff.Create()); TestContent.Select(first.Session, "jobs", "job-101");
        first.Session.Change("notesInput", "Automatically retained", first.Session.Revision);
        await first.Session.FlushAsync(); Assert.Null(first.Session.PersistenceError);
        using WorkspaceScope restored = h.Scope(); Assert.True((await restored.Session.RestoreAsync("auto")).Success);
        Assert.Equal("Automatically retained", TestContent.Value(restored.Session, "notes")); Assert.True(restored.Session.HasDraft);
    }

    [Fact]
    public async Task PreCancelledFollowupAndFork_PreserveTheConnectedDraft()
    {
        using WorkspaceHarness h = new(); using WorkspaceScope scope = h.Scope();
        await scope.Session.ConnectAsync("chat"); scope.Session.Open(h.Staff.Create());
        TestContent.Select(scope.Session, "jobs", "job-101"); scope.Session.Change("notesInput", "Keep my notes", scope.Session.Revision);
        using CancellationTokenSource source = new(); source.Cancel();
        Assert.Equal("cancelled", Assert.Single((await scope.Session.SubmitAsync("Follow up", source.Token)).Errors).Code);
        Assert.Equal("cancelled", Assert.Single((await scope.Session.ForkAsync("cancelled-fork", source.Token)).Errors).Code);
        Assert.Equal("Keep my notes", TestContent.Value(scope.Session, "notes")); Assert.True(scope.Session.HasDraft);
        Assert.Equal("chat", scope.Session.WorkspaceKey); Assert.Empty(scope.Session.Archive);
        Assert.Null(await h.Store.LoadAsync("demo-alice", "cancelled-fork"));
    }

    [Fact]
    public async Task History_ContextIsBoundedWhileOriginalTurnsSurviveStorage()
    {
        using WorkspaceHarness h = new(); using WorkspaceScope scope = h.Scope(); await scope.Session.ConnectAsync("history"); scope.Session.Open(h.Staff.Create());
        for (int i = 0; i < 24; i++) Assert.True((await scope.Session.SubmitAsync($"Request {i}")).Success);
        Assert.Equal(24, scope.Session.Archive.Length); Assert.Equal(20, scope.Session.History.Length);
        ConversationContext context = scope.Session.Snapshot.Conversation;
        Assert.Equal(6, context.Recent.Length); Assert.Contains("Extractive history index", context.Summary);
        Assert.Equal("Request 0", scope.Session.Archive[0].Input); Assert.NotNull(scope.Session.Archive[0].OutputJson);
        await scope.Session.SaveAsync(); using WorkspaceScope restored = h.Scope(); await restored.Session.RestoreAsync("history");
        Assert.Equal(scope.Session.Archive.ToArray(), restored.Session.Archive.ToArray()); Assert.Equal(24, restored.Session.Snapshot.Conversation.OriginalCount);
    }

    [Fact]
    public async Task Updates_RejectStaleRemovedAndHiddenDirtyFields()
    {
        using WorkspaceHarness h = new(); using WorkspaceScope scope = h.Scope(); AiSession session = scope.Session;
        session.Open(h.Staff.Create()); TestContent.Select(session, "jobs", "job-101"); session.Change("notesInput", "Retain", session.Revision);
        OutputContent next = h.Staff.Create();
        Assert.False(session.ApplyUpdate(next, session.Revision - 1).Success);
        next = TestContent.ChangeNode(next, "notesInput", node => node with { Kind = NodeKind.Text, StateKey = null, Value = ValueBinding.Constant("Removed") });
        Assert.Equal("dirty-state", Assert.Single(session.ApplyUpdate(next, session.Revision).Errors).Code);
        Assert.Equal("Retain", TestContent.Value(session, "notes"));
        next = h.Staff.Create(); next = next with { Document = next.Document with { State = next.Document.State.Add("hidden", new() { Kind = StateKind.Boolean, InitialValue = JsonValues.From(false) }) } };
        next = TestContent.ChangeNode(next, "draft", node => node with { WhenState = "hidden" });
        Assert.False(session.ApplyUpdate(next, session.Revision).Success); Assert.Equal("Retain", TestContent.Value(session, "notes"));
    }
}
