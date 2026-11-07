using System.Collections.Immutable;
using System.Security.Claims;
using System.Text;
using IntentBlazor.AI;
using IntentBlazor.Content;
using IntentBlazor.ProductionHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IntentBlazor.Tests;

public sealed class FoundationHostTests
{
    [Theory]
    [InlineData("anonymous")]
    [InlineData("missing-subject")]
    [InlineData("duplicate-tenant")]
    [InlineData("multiple-identities")]
    [InlineData("blank-subject")]
    [InlineData("mixed-identities")]
    public void Claims_RejectMissingAmbiguousOrUnauthenticatedIdentity(string kind)
    {
        ClaimsPrincipal user = HostTestAuthentication.User();
        ClaimsIdentity identity = Assert.IsType<ClaimsIdentity>(user.Identity);
        switch (kind)
        {
            case "anonymous": user = new(new ClaimsIdentity(identity.Claims)); break;
            case "missing-subject": identity.RemoveClaim(identity.FindFirst(ClaimTypes.NameIdentifier)!); break;
            case "duplicate-tenant": identity.AddClaim(new(HostClaims.TenantClaim, "another-tenant")); break;
            case "multiple-identities": user.AddIdentity(new ClaimsIdentity(identity.Claims, "Other")); break;
            case "blank-subject": identity.RemoveClaim(identity.FindFirst(ClaimTypes.NameIdentifier)!); identity.AddClaim(new(ClaimTypes.NameIdentifier, " ")); break;
            case "mixed-identities": user = new([new ClaimsIdentity([new Claim(HostClaims.TenantClaim, "tenant-a")]), new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "alex")], "Test")]); break;
        }
        Assert.Null(HostClaims.Read(user)); Assert.Null(HostClaims.OwnerOf(user));
    }

    [Fact]
    public async Task TrustedIdentity_SeparatesTenantsAndRechecksRevocation()
    {
        using HostHarness host = new(); using HostScope alex = host.Scope(); using HostScope otherTenant = host.Scope(tenant: "tenant-b");
        string owner = (await alex.Identity.RequireAsync()).OwnerId;
        Assert.NotEqual(owner, (await otherTenant.Identity.RequireAsync()).OwnerId);
        Assert.Equal(owner, HostClaims.OwnerOf(HostTestAuthentication.User()));
        host.Accounts.Revoke();
        Assert.Null(await alex.Identity.GetOwnerIdAsync(default));
        await Assert.ThrowsAsync<InputRejectedException>(async () => await alex.Notes.ReadAsync());
    }

    [Fact]
    public async Task ConfiguredGrants_RejectDuplicatesAndObservePermissionReloads()
    {
        IConfigurationRoot config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Host:Accounts:0:Tenant"] = "t",
            ["Host:Accounts:0:Subject"] = "s",
            ["Host:Accounts:0:CanEdit"] = "true"
        }).Build();
        ServiceCollection services = new(); services.Configure<HostGrantOptions>(config.GetSection("Host"));
        using ServiceProvider provider = services.BuildServiceProvider();
        ConfiguredHostAccounts accounts = new(provider.GetRequiredService<IOptionsMonitor<HostGrantOptions>>());
        Assert.True((await accounts.FindAsync("t", "s", default))!.CanEdit);
        config["Host:Accounts:0:CanEdit"] = "false"; config.Reload();
        Assert.False((await accounts.FindAsync("t", "s", default))!.CanEdit);
        config["Host:Accounts:1:Tenant"] = "t"; config["Host:Accounts:1:Subject"] = "s"; config.Reload();
        Assert.Null(await accounts.FindAsync("t", "s", default)); Assert.Null(await accounts.FindAsync("t", "unknown", default));
    }

    [Fact]
    public async Task OwnersAndCircuits_IsolatePrivateDraftsAndCheckpointConflicts()
    {
        using HostHarness host = new(); using HostScope first = host.Scope(); await first.StartAsync(); first.Edit("Alex private draft");
        Assert.True((await first.Session.SaveAsync()).Success);
        using HostScope bob = host.Scope("bob"); Assert.False((await bob.Session.RestoreAsync("notes")).Success); Assert.Null(bob.Session.Document);
        using HostScope otherTenant = host.Scope(tenant: "tenant-b"); Assert.False((await otherTenant.Session.RestoreAsync("notes")).Success);
        using HostScope second = host.Scope(); Assert.True((await second.Session.RestoreAsync("notes")).Success);
        second.Edit("Second circuit draft"); first.Edit("First circuit new edit"); Assert.True((await first.Session.SaveAsync()).Success);
        Assert.False((await second.Session.SaveAsync()).Success); Assert.Equal("workspace-conflict", second.Session.PersistenceError!.Code);
        Assert.Equal("Second circuit draft", second.Session.Snapshot.Values["draft"].GetString());
        Assert.Equal("First circuit new edit", first.Session.Snapshot.Values["draft"].GetString());
        first.Authentication.Principal = HostTestAuthentication.User("bob");
        Assert.False(await first.Session.ValidateOwnerAsync()); Assert.Null(first.Session.Document); Assert.Empty(first.Session.Archive);
    }

    [Fact]
    public async Task WorkspaceCatalog_UsesStableOwnerScopedKeysAndRejectsUnknownNavigation()
    {
        using HostHarness host = new(); using HostScope alex = host.Scope(); using HostScope bob = host.Scope("bob");
        Assert.Equal("notes", Assert.Single(await alex.Notes.ListWorkspacesAsync()).Key);
        HostWorkspace named = await alex.Notes.CreateWorkspaceAsync("Research notes");
        Assert.Contains(named, await alex.Notes.ListWorkspacesAsync()); Assert.DoesNotContain(named, await bob.Notes.ListWorkspacesAsync());
        Assert.True((await alex.Services.GetRequiredService<HostWorkspaceController>().OpenAsync(named.Key)).Success);
        Assert.False((await bob.Services.GetRequiredService<HostWorkspaceController>().OpenAsync(named.Key)).Success);
        Assert.Null(bob.Session.WorkspaceKey);
        await Assert.ThrowsAsync<ArgumentException>(async () => await alex.Notes.CreateWorkspaceAsync(new string('x', 81)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AttributeAndExplicitTools_ExposeTheSameAuthorizedReadAndBoundedSetup(bool attributes)
    {
        using HostHarness host = new(attributes); using HostScope scope = host.Scope();
        DataToolRegistration tool = Assert.Single(host.Provider.GetServices<DataToolRegistration>());
        Assert.Equal("read_private_notes", tool.Name); Assert.Equal(nameof(HostNotes.ReadAsync), tool.Method.Name);
        OutputDataSet data = Assert.IsType<OutputDataSet>(await ((ValueTask<OutputDataSet>)tool.Method.Invoke(scope.Notes, [CancellationToken.None])!));
        Assert.Equal("notes", data.Id); Assert.Single(data.Rows);
        AiSetupPolicy policy = await scope.Services.GetRequiredService<IAiSetupAccess>().GetPolicyAsync(default);
        Assert.True(policy.CanEdit); Assert.False(policy.CanProbe); Assert.Equal(1024, policy.MaximumOutputTokens);
        host.Accounts.Set(new("tenant-a", "alex", false, false));
        Assert.False((await scope.Services.GetRequiredService<IAiSetupAccess>().GetPolicyAsync(default)).CanEdit);
        host.Accounts.Revoke();
        await Assert.ThrowsAsync<InputRejectedException>(async () => await scope.Notes.ReadAsync());
        Assert.False((await scope.Services.GetRequiredService<IAiSetupAccess>().GetPolicyAsync(default)).CanView);
    }

    [Fact]
    public async Task BusinessActions_RecheckPermissionAndVersionAndRetainConflictingDrafts()
    {
        using HostHarness host = new(); using HostScope one = host.Scope(); using HostScope two = host.Scope();
        await one.StartAsync(); await two.StartAsync(); one.Edit("Saved note"); two.Edit("Conflicting private edit");
        Assert.Equal(ActionStatus.Succeeded, (await one.SaveNoteAsync()).Status); Assert.False(one.Session.HasDraft);
        Assert.Equal(ActionStatus.Conflict, (await two.SaveNoteAsync()).Status); Assert.True(two.Session.HasDraft);
        Assert.Equal("Conflicting private edit", two.Session.Snapshot.Values["draft"].GetString());
        one.Edit("Permission revoked draft"); host.Accounts.Set(new("tenant-a", "alex", false, false));
        Assert.Equal(ActionStatus.Rejected, (await one.SaveNoteAsync()).Status); Assert.True(one.Session.HasDraft);
        Assert.Equal("Saved note", (await one.Notes.ReadAsync()).Rows[0]["body"].GetString());
    }

    [Fact]
    public async Task BusinessReceipts_AreTransactionalAndRejectChangedReplayArguments()
    {
        using HostHarness host = new(); using HostScope scope = host.Scope();
        ActionContext request = new((await scope.Identity.RequireAsync()).OwnerId, "notes", "business-request-1", "note-1", "0",
            ImmutableDictionary<string, System.Text.Json.JsonElement>.Empty.Add("body", JsonValues.From("Exactly once")));
        Assert.Equal(ActionStatus.Succeeded, (await scope.Notes.SaveAsync(request)).Status);
        Assert.Equal(ActionStatus.Succeeded, (await scope.Notes.SaveAsync(request)).Status);
        Assert.Equal("1", (await scope.Notes.ReadAsync()).Rows[0]["version"].GetString());
        Assert.Equal(ActionStatus.Rejected, (await scope.Notes.SaveAsync(request with { Arguments = request.Arguments.SetItem("body", JsonValues.From("Changed replay")) })).Status);
        Assert.Equal(ActionStatus.Rejected, (await scope.Notes.SaveAsync(request with { IdempotencyKey = "wrong-owner", OwnerId = HostClaims.OwnerId("tenant-a", "bob") })).Status);
        Assert.Equal(ActionStatus.Rejected, (await scope.Notes.SaveAsync(request with { IdempotencyKey = "oversize", Arguments = request.Arguments.SetItem("body", JsonValues.From(new string('n', 513))) })).Status);
        Assert.Equal("Exactly once", (await scope.Notes.ReadAsync()).Rows[0]["body"].GetString());
    }

    [Fact]
    public async Task ReopenAndRestart_RestoreArchiveBoundedContextDraftAndPrivateArtifact()
    {
        using HostHarness host = new(); ArtifactReference reference;
        using (HostScope original = host.Scope())
        {
            await original.StartAsync();
            for (int index = 0; index < 8; index++) Assert.True((await original.Session.SubmitAsync("Follow-up " + index)).Success);
            original.Edit("Draft after many turns");
            await using MemoryStream bytes = new(Encoding.UTF8.GetBytes("Private backup attachment"));
            reference = (await original.Services.GetRequiredService<IWorkspaceContent>().ImportAsync(new("private.txt", "text/plain", bytes.Length), bytes)).Reference;
            Assert.True(original.Session.SetArtifacts([reference]).Success); Assert.True((await original.Session.SaveAsync()).Success);
        }
        host.Restart();
        using HostScope reopened = host.Scope();
        Assert.True((await reopened.Services.GetRequiredService<HostWorkspaceController>().OpenAsync("notes")).Success);
        Assert.Equal("Draft after many turns", reopened.Session.Snapshot.Values["draft"].GetString()); Assert.True(reopened.Session.HasDraft);
        Assert.Equal(9, reopened.Session.Archive.Length); Assert.Equal(9, reopened.Session.Snapshot.Conversation.OriginalCount);
        Assert.Equal(6, reopened.Session.Snapshot.Conversation.Recent.Length); Assert.Contains("Extractive history index", reopened.Session.Snapshot.Conversation.Summary);
        Assert.Equal(reference, Assert.Single(reopened.Session.Artifacts));
        Assert.NotNull(await reopened.Services.GetRequiredService<IWorkspaceContent>().DescribeAsync(reference));
        using HostScope bob = host.Scope("bob"); Assert.Null(await bob.Services.GetRequiredService<IWorkspaceContent>().DescribeAsync(reference));
    }

    [Fact]
    public async Task Restore_ReauthorizesMissingArtifactsAndRejectsUnregisteredDatasets()
    {
        using HostHarness host = new(); using HostScope scope = host.Scope(); await scope.StartAsync();
        string owner = (await scope.Identity.RequireAsync()).OwnerId;
        IWorkspaceAccess access = scope.Services.GetRequiredService<IWorkspaceAccess>();
        Assert.False((await access.ReauthorizeAsync(owner, ["foreign-records"], [], default)).Allowed);
        Assert.False((await access.ReauthorizeAsync(HostClaims.OwnerId("tenant-a", "bob"), ["notes"], [], default)).Allowed);
        ArtifactReference missing = new(new string('a', 32), "text/plain", new string('A', 64));
        Assert.False((await access.ReauthorizeAsync(owner, ["notes"], [missing], default)).Allowed);
        Assert.False((await access.ReauthorizeAsync(owner, ["notes"], [missing with { Id = "malformed" }], default)).Allowed);
        Assert.True((await scope.Session.SaveAsync()).Success);
        IWorkspaceStore store = host.Provider.GetRequiredService<IWorkspaceStore>();
        StoredWorkspace saved = (await store.LoadAsync(owner, "notes"))!;
        WorkspaceCheckpoint checkpoint = WorkspaceJson.Read<WorkspaceCheckpoint>(saved.Json) with { Artifacts = [missing] };
        Assert.True(await store.SaveAsync(owner, "blocked", 0, WorkspaceJson.Write(checkpoint)));
        scope.Edit("Keep existing private draft");
        Assert.False((await scope.Session.RestoreAsync("blocked", discardLocal: true)).Success);
        Assert.Equal("workspace-access", scope.Session.Errors[0].Code); Assert.Equal("Keep existing private draft", scope.Session.Snapshot.Values["draft"].GetString());
    }

    [Theory]
    [InlineData("0.1")]
    [InlineData("0.2")]
    [InlineData("0.3")]
    public async Task SavedCheckpointFixtures_RestoreSupportedSchemaDraftAndHistory(string schema)
    {
        using HostHarness host = new(); using HostScope scope = host.Scope();
        string owner = (await scope.Identity.RequireAsync()).OwnerId;
        string path = Path.Combine(RepositoryRoot(), "specs", "workspace-checkpoints", "schema-" + schema + ".json");
        string json = await File.ReadAllTextAsync(path);
        Assert.True(await host.Provider.GetRequiredService<IWorkspaceStore>().SaveAsync(owner, "saved", 0, json));
        Assert.True((await scope.Session.RestoreAsync("saved")).Success);
        Assert.Equal(schema, scope.Session.Document!.SchemaVersion); Assert.True(scope.Session.HasDraft);
        Assert.Equal("Saved private draft", scope.Session.Snapshot.Values["draft"].GetString());
        Assert.Equal("Show my notes", Assert.Single(scope.Session.Archive).Input);
    }

    [Theory]
    [InlineData("future-format")]
    [InlineData("future-schema")]
    [InlineData("malformed")]
    public async Task UnsupportedCheckpoints_LeaveCurrentDraftAndSavedBytesUntouched(string kind)
    {
        using HostHarness host = new(); using HostScope scope = host.Scope(); await scope.StartAsync(); Assert.True((await scope.Session.SaveAsync()).Success);
        string owner = (await scope.Identity.RequireAsync()).OwnerId;
        IWorkspaceStore store = host.Provider.GetRequiredService<IWorkspaceStore>();
        WorkspaceCheckpoint checkpoint = WorkspaceJson.Read<WorkspaceCheckpoint>((await store.LoadAsync(owner, "notes"))!.Json);
        string json = kind switch
        {
            "future-format" => WorkspaceJson.Write(checkpoint with { FormatVersion = "future" }),
            "future-schema" => WorkspaceJson.Write(checkpoint with { Content = checkpoint.Content with { Document = checkpoint.Content.Document with { SchemaVersion = "9.0" } } }),
            _ => "{\"malformed\":"
        };
        Assert.True(await store.SaveAsync(owner, "unsupported", 0, json)); scope.Edit("Do not discard my work");
        long revision = scope.Session.Revision;
        OperationResult result = await scope.Session.RestoreAsync("unsupported", discardLocal: true);
        Assert.False(result.Success); Assert.Equal(kind == "malformed" ? "invalid-checkpoint" : "incompatible-workspace", result.Errors[0].Code);
        Assert.Equal(revision, scope.Session.Revision); Assert.Equal("Do not discard my work", scope.Session.Snapshot.Values["draft"].GetString());
        Assert.True(scope.Session.HasDraft); Assert.Equal(json, (await store.LoadAsync(owner, "unsupported"))!.Json);
    }

    [Fact]
    public void AdmissionBudget_EnforcesExactDailyLimitAcrossRestartAndUtcRollover()
    {
        using HostHarness host = new(); HostRequestLimits limits = new(2, 1, 1);
        HostRequestBudget budget = new(host.Paths, limits, host.Clock);
        using (budget.Enter("owner")) { }
        using (budget.Enter("owner")) { }
        Assert.Throws<InputRejectedException>(() => budget.Enter("owner"));
        HostRequestBudget restarted = new(host.Paths, limits, host.Clock);
        Assert.Throws<InputRejectedException>(() => restarted.Enter("owner"));
        host.Clock.Now = host.Clock.Now.AddSeconds(1);
        using (restarted.Enter("owner")) { }
        host.Clock.Now = host.Clock.Now.AddSeconds(1);
        using (restarted.Enter("owner")) { }
        Assert.Throws<InputRejectedException>(() => restarted.Enter("owner"));
        using (restarted.Enter("other-owner")) { }
    }

    [Fact]
    public void AdmissionBudget_RejectsConcurrentWorkWithoutChargingDeniedOwnersAndReleasesOnce()
    {
        using HostHarness host = new(); HostRequestBudget budget = new(host.Paths, new(1, 1, 1), host.Clock);
        IDisposable lease = budget.Enter("one");
        Assert.Throws<InputRejectedException>(() => budget.Enter("one")); Assert.Throws<InputRejectedException>(() => budget.Enter("two"));
        lease.Dispose(); lease.Dispose();
        using (budget.Enter("two")) { }
        Assert.Throws<InputRejectedException>(() => budget.Enter("two"));
        using CancellationTokenSource cancelled = new(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => budget.Enter("not-charged", cancelled.Token));
        using (budget.Enter("not-charged")) { }
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("cancellation")]
    [InlineData("identity-change")]
    public async Task LimitedProcessor_HoldsSharedAdmissionAndReleasesOnEveryOutcome(string outcome)
    {
        using HostHarness host = new(); using HostScope scope = host.Scope(); using HostScope sameOwner = host.Scope();
        OutputContent content = await scope.Services.GetRequiredService<HostOfflineProcessor>().ProcessAsync("notes", scope.Session.Snapshot, default);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously), release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestHostInput inner = new(async token => { entered.TrySetResult(); await release.Task.WaitAsync(token); if (outcome == "failure") throw new IOException("test failure"); return content; });
        HostRequestBudget budget = host.Provider.GetRequiredService<HostRequestBudget>();
        HostLimitedInputProcessor limited = new(scope.Identity, budget, inner), other = new(sameOwner.Identity, budget, inner);
        using CancellationTokenSource cancellation = new();
        Task<OutputContent> running = limited.ProcessAsync("notes", scope.Session.Snapshot, cancellation.Token).AsTask(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InputRejectedException>(async () => await other.ProcessAsync("notes", sameOwner.Session.Snapshot, default));
        if (outcome == "cancellation") cancellation.Cancel();
        if (outcome == "identity-change") scope.Authentication.Principal = HostTestAuthentication.User("bob");
        release.TrySetResult();
        if (outcome == "failure") await Assert.ThrowsAsync<IOException>(() => running);
        else if (outcome == "cancellation") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        else if (outcome == "identity-change") await Assert.ThrowsAsync<InputRejectedException>(() => running);
        else Assert.Same(content, await running);
        using (budget.Enter(HostClaims.OwnerId("tenant-a", "alex"))) { }
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task LimitedProcessor_PreservesStreamingProgressWithoutAddingModelConnections()
    {
        using HostHarness host = new(); using HostScope scope = host.Scope();
        OutputContent content = await scope.Services.GetRequiredService<HostOfflineProcessor>().ProcessAsync("notes", scope.Session.Snapshot, default);
        TestHostInput input = new(_ => Task.FromResult(content));
        HostLimitedInputProcessor limited = new(scope.Identity, host.Provider.GetRequiredService<HostRequestBudget>(), input);
        CapturedProgress progress = new();
        Assert.Same(content, await limited.ProcessAsync("notes", scope.Session.Snapshot, progress, default));
        Assert.Equal("test-stream", Assert.Single(progress.Values).Stage);
        Assert.Empty(host.Provider.GetServices<Microsoft.Extensions.AI.IChatClient>());
    }

    [Fact]
    public void PrivatePaths_RejectRelativePublicAndAncestorDirectories()
    {
        using HostHarness host = new(); string app = Path.Combine(host.Root, "app");
        Assert.Throws<ArgumentException>(() => HostPaths.Create("relative", app));
        Assert.Throws<ArgumentException>(() => HostPaths.Create(Path.Combine(app, "wwwroot", "private"), app));
        Assert.Throws<ArgumentException>(() => HostPaths.Create(app, app));
        Assert.Throws<ArgumentException>(() => HostPaths.Create(host.Root, app));
        Assert.Equal(Path.Combine(host.Root, "private"), host.Paths.Root);
    }

    [Fact]
    public async Task ConfiguredModel_UsesAuthorizedPreloadInsideTheSameAdmissionBoundary()
    {
        OutputDocument document = new() { Id = "offline-model", RootId = "text", DataSets = ["notes"], Nodes = [new() { Id = "text", Kind = NodeKind.Text, Value = ValueBinding.Constant("Offline composed notes") }] };
        ScriptChatClient client = new(ModelTestSupport.Text(System.Text.Json.JsonSerializer.Serialize(new { documentJson = OutputJson.SerializeDocument(document) })));
        using HostHarness host = new(configureModel: ai => ai.UseChatClient(_ => client, new("Offline test", "test-only", null, NativeTools: false)));
        using HostScope scope = host.Scope();
        Assert.True((await scope.Services.GetRequiredService<HostWorkspaceController>().OpenAsync("notes")).Success);
        Assert.IsType<HostLimitedInputProcessor>(scope.Services.GetRequiredService<IInputProcessor>());
        var result = await scope.Session.SubmitAsync("Show my note"); Assert.True(result.Success, string.Join(",", result.Errors.Select(error => error.Code)));
        Assert.Single(client.Calls); Assert.Equal("notes", Assert.Single(scope.Session.Data).Id);
        Assert.Contains("Write a private note", string.Join(" ", client.Calls[0].Messages.Select(message => message.Text)));
    }

    [Fact]
    public async Task StoppedHostBackup_RestoresWorkspaceDraftAndArtifactBytesTogether()
    {
        using HostHarness host = new(); string owner; ArtifactReference reference; string requestId;
        using (HostScope original = host.Scope())
        {
            await original.StartAsync(); owner = (await original.Identity.RequireAsync()).OwnerId;
            original.Edit("Persisted business note");
            ActionRequest save = Assert.IsType<ActionRequest>(original.Session.PrepareAction("save", original.Session.Revision)); requestId = save.RequestId;
            Assert.Equal(ActionStatus.Succeeded, (await original.Session.ExecuteActionAsync(save)).Status);
            original.Edit("Backed up private draft");
            await using MemoryStream stream = new(Encoding.UTF8.GetBytes("Backup file bytes"));
            reference = (await original.Services.GetRequiredService<IWorkspaceContent>().ImportAsync(new("backup.txt", "text/plain", stream.Length), stream)).Reference;
            Assert.True(original.Session.SetArtifacts([reference]).Success); Assert.True((await original.Session.SaveAsync()).Success);
        }
        host.Provider.Dispose(); // All DB connections use Pooling=false; the backup is made while the host is stopped.
        string backup = Path.Combine(host.Root, "backup"); Directory.CreateDirectory(backup);
        foreach (string file in Directory.EnumerateFiles(host.Paths.Root, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(backup, Path.GetRelativePath(host.Paths.Root, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
        }
        IntentBlazor.Storage.Sqlite.SqliteWorkspaceStore restored = new(Path.Combine(backup, "workspaces.sqlite"));
        StoredWorkspace saved = (await restored.LoadAsync(owner, "notes"))!;
        WorkspaceCheckpoint checkpoint = WorkspaceJson.Read<WorkspaceCheckpoint>(saved.Json);
        Assert.Equal("Backed up private draft", checkpoint.Values["draft"].GetString()); Assert.Single(checkpoint.Archive);
        Assert.Equal(ActionStatus.Succeeded, (await restored.GetActionAsync(owner, "notes", requestId))!.Result!.Status);
        FileArtifactStore files = new(new(Path.Combine(backup, "artifacts")));
        Assert.Equal("Backup file bytes", Encoding.UTF8.GetString((await files.ReadAsync(owner, reference))!));
        Assert.Null(await restored.LoadAsync(HostClaims.OwnerId("tenant-a", "bob"), "notes"));
        using Microsoft.Data.Sqlite.SqliteConnection business = new(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path.Combine(backup, "business.sqlite"), Pooling = false }.ToString());
        business.Open(); using var command = business.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM host_receipts WHERE request=$request;";
        command.Parameters.AddWithValue("$request", requestId); Assert.Equal(1L, command.ExecuteScalar());
        HostRequestBudget restoredBudget = new(new(backup), new(1, 1, 1), host.Clock);
        Assert.Throws<InputRejectedException>(() => restoredBudget.Enter(owner));
    }

    [Fact]
    public async Task SqliteUpgrade_RejectsFutureSchemaWithoutRewritingSavedCheckpoint()
    {
        using HostHarness host = new(); using HostScope scope = host.Scope(); await scope.StartAsync(); scope.Edit("Upgrade recovery draft");
        Assert.True((await scope.Session.SaveAsync()).Success); string owner = (await scope.Identity.RequireAsync()).OwnerId;
        IWorkspaceStore store = host.Provider.GetRequiredService<IWorkspaceStore>(); StoredWorkspace saved = (await store.LoadAsync(owner, "notes"))!;
        using Microsoft.Data.Sqlite.SqliteConnection database = new(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = host.Paths.Workspaces, Pooling = false }.ToString());
        database.Open(); using var update = database.CreateCommand(); update.CommandText = "UPDATE intent_metadata SET value='future' WHERE name='schema';"; Assert.Equal(1, update.ExecuteNonQuery());
        Assert.Throws<InvalidOperationException>(() => new IntentBlazor.Storage.Sqlite.SqliteWorkspaceStore(host.Paths.Workspaces));
        Assert.Equal(saved, await store.LoadAsync(owner, "notes"));
        update.CommandText = "SELECT value FROM intent_metadata WHERE name='schema';"; Assert.Equal("future", update.ExecuteScalar());
    }

    [Fact]
    public async Task WorkspaceNames_EnforceCountAndValidationBoundsWithoutDroppingExistingNames()
    {
        using HostHarness host = new(); using HostScope scope = host.Scope();
        for (int index = 0; index < 99; index++) await scope.Notes.CreateWorkspaceAsync("Workspace " + index);
        Assert.Equal(100, (await scope.Notes.ListWorkspacesAsync()).Count);
        await Assert.ThrowsAsync<InputRejectedException>(async () => await scope.Notes.CreateWorkspaceAsync("One too many"));
        await Assert.ThrowsAsync<ArgumentException>(async () => await scope.Notes.CreateWorkspaceAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(async () => await scope.Notes.CreateWorkspaceAsync("Invalid\nname"));
        Assert.Equal(100, (await scope.Notes.ListWorkspacesAsync()).Count);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(10001, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 17, 17)]
    [InlineData(1, 2, 1)]
    [InlineData(1, 1, 257)]
    public void AdmissionConfiguration_RejectsUnsupportedOrInconsistentBounds(int daily, int owner, int total)
    {
        using HostHarness host = new(); Assert.Throws<ArgumentException>(() => new HostRequestBudget(host.Paths, new(daily, owner, total), host.Clock));
    }

    internal static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IntentBlazor.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }

    private sealed class TestHostInput(Func<CancellationToken, Task<OutputContent>> run) : IStreamingInputProcessor
    {
        public int Calls { get; private set; }
        public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken token) { Calls++; return new(run(token)); }
        public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, IProgress<InputProgress> progress, CancellationToken token)
        { progress.Report(new("test-stream", 0, 0, 0)); return ProcessAsync(input, workspace, token); }
    }
    private sealed class CapturedProgress : IProgress<InputProgress>
    {
        public List<InputProgress> Values { get; } = [];
        public void Report(InputProgress value) => Values.Add(value);
    }
}
