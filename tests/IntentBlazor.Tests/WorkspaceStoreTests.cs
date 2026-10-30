using IntentBlazor.Storage.Sqlite;

namespace IntentBlazor.Tests;

public sealed class WorkspaceStoreTests
{
    [Theory]
    [InlineData("memory")]
    [InlineData("sqlite")]
    public async Task Stores_IsolateOwnersAndCompareCheckpointVersions(string kind)
    {
        using WorkspaceHarness h = new(kind);
        Assert.True(await h.Store.SaveAsync("owner-a", "chat", 0, "{\"value\":1}"));
        Assert.Null(await h.Store.LoadAsync("owner-b", "chat"));
        Assert.False(await h.Store.SaveAsync("owner-b", "chat", 1, "other"));
        Assert.True(await h.Store.SaveAsync("owner-b", "chat", 0, "{\"value\":2}"));
        Assert.False(await h.Store.SaveAsync("owner-a", "chat", 0, "overwrite"));
        Assert.Equal(1, (await h.Store.LoadAsync("owner-a", "chat"))!.Version);
        Assert.Equal("{\"value\":1}", (await h.Store.LoadAsync("owner-a", "chat"))!.Json);
        Assert.Equal("{\"value\":2}", (await h.Store.LoadAsync("owner-b", "chat"))!.Json);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("sqlite")]
    public async Task Stores_AllowOnlyOneConcurrentCheckpointWriter(string kind)
    {
        using WorkspaceHarness h = new(kind);
        await h.Store.SaveAsync("owner", "chat", 0, "initial");
        bool[] results = await Task.WhenAll(Enumerable.Range(0, 2).Select(index => Task.Run(async () =>
            await h.Store.SaveAsync("owner", "chat", 1, $"writer-{index}"))));
        Assert.Single(results, result => result);
        Assert.Equal(2, (await h.Store.LoadAsync("owner", "chat"))!.Version);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("sqlite")]
    public async Task Stores_ClaimAndCompleteReceiptsOnceWithinOwnerAndWorkspace(string kind)
    {
        using WorkspaceHarness h = new(kind);
        string id = Guid.NewGuid().ToString("N"), fingerprint = new('A', 64);
        Assert.True(await h.Store.TryClaimActionAsync("owner", "chat", id, fingerprint));
        Assert.False(await h.Store.TryClaimActionAsync("owner", "chat", id, fingerprint));
        Assert.Null((await h.Store.GetActionAsync("owner", "chat", id))!.Result);
        Assert.Null(await h.Store.GetActionAsync("other", "chat", id));
        Assert.Null(await h.Store.GetActionAsync("owner", "other", id));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await h.Store.CompleteActionAsync("owner", "chat", id, new('B', 64), new(ActionStatus.Succeeded, "Saved")));
        await h.Store.CompleteActionAsync("owner", "chat", id, fingerprint, new(ActionStatus.Succeeded, "Saved"));
        Assert.Equal(ActionStatus.Succeeded, (await h.Store.GetActionAsync("owner", "chat", id))!.Result!.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await h.Store.CompleteActionAsync("owner", "chat", id, fingerprint, new(ActionStatus.Succeeded, "Again")));
    }

    [Fact]
    public async Task Sqlite_RetainsCheckpointsAndPendingReceiptsAcrossStoreInstances()
    {
        using WorkspaceHarness h = new("sqlite");
        string id = Guid.NewGuid().ToString("N"), fingerprint = new('A', 64);
        await h.Store.SaveAsync("owner", "chat", 0, "retained");
        await h.Store.TryClaimActionAsync("owner", "chat", id, fingerprint);
        SqliteWorkspaceStore reopened = new(Path.Combine(h.DirectoryPath, "workspaces.sqlite"));
        Assert.Equal("retained", (await reopened.LoadAsync("owner", "chat"))!.Json);
        Assert.False(await reopened.TryClaimActionAsync("owner", "chat", id, fingerprint));
        Assert.Null((await reopened.GetActionAsync("owner", "chat", id))!.Result);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("sqlite")]
    public async Task Stores_RejectInvalidAddressesVersionsQuotasAndCancellation(string kind)
    {
        using WorkspaceHarness h = new(kind);
        await Assert.ThrowsAsync<ArgumentException>(async () => await h.Store.LoadAsync("", "chat"));
        await Assert.ThrowsAsync<ArgumentException>(async () => await h.Store.LoadAsync("owner", "../chat"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await h.Store.SaveAsync("owner", "chat", -1, "value"));
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(async () => await h.Store.SaveAsync("owner", "chat", 0, new string('x', WorkspaceJson.MaximumBytes + 1)));
        using CancellationTokenSource cancel = new(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await h.Store.SaveAsync("owner", "chat", 0, "value", cancel.Token));
        Assert.Null(await h.Store.LoadAsync("owner", "chat"));
    }
}
