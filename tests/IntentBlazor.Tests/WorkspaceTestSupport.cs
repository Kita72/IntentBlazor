using System.Collections.Immutable;
using IntentBlazor.Core;
using IntentBlazor.Runtime;
using IntentBlazor.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

internal sealed class MutableWorkspaceIdentity : IWorkspaceIdentity
{
    public string? Owner { get; set; }
    public ValueTask<string?> GetOwnerIdAsync(CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(Owner); }
}
internal sealed class TestWorkspaceAccess(StaffWorkspaces inner) : IWorkspaceAccess
{
    public bool Allowed { get; set; } = true;
    public bool RemoveSelectedRow { get; set; }
    public Func<CancellationToken, Task>? BeforeResolve { get; set; }
    public async ValueTask<WorkspaceAccess> ReauthorizeAsync(string owner, ImmutableArray<string> ids,
        ImmutableArray<ArtifactReference> artifacts, CancellationToken token)
    {
        if (BeforeResolve is not null) await BeforeResolve(token);
        WorkspaceAccess result = await inner.ReauthorizeAsync(owner, ids, artifacts, token);
        if (RemoveSelectedRow) result = result with
        {
            Data = result.Data.Select(set => set.Id == "jobs"
            ? set with { Rows = set.Rows.Where(row => row["id"].GetString() != "job-101").ToImmutableArray() } : set).ToImmutableArray()
        };
        return result with { Allowed = Allowed && result.Allowed };
    }
}
internal sealed class ThrowAfterSaveHandler(SharedJobStore jobs) : IWorkspaceActionHandler
{
    public ValueTask<bool> AuthorizeAsync(ActionContext context, CancellationToken cancellationToken) => ValueTask.FromResult(true);
    public ValueTask<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    { jobs.Save(context, cancellationToken); throw new InvalidOperationException("private backend exception"); }
}
internal sealed class FailingReceiptStore(IWorkspaceStore inner) : IWorkspaceStore
{
    public ValueTask<StoredWorkspace?> LoadAsync(string ownerId, string key, CancellationToken token = default) => inner.LoadAsync(ownerId, key, token);
    public ValueTask<bool> SaveAsync(string ownerId, string key, long expectedVersion, string json, CancellationToken token = default) => inner.SaveAsync(ownerId, key, expectedVersion, json, token);
    public ValueTask<bool> TryClaimActionAsync(string ownerId, string key, string requestId, string fingerprint, CancellationToken token = default) => inner.TryClaimActionAsync(ownerId, key, requestId, fingerprint, token);
    public ValueTask<ActionReceipt?> GetActionAsync(string ownerId, string key, string requestId, CancellationToken token = default) => inner.GetActionAsync(ownerId, key, requestId, token);
    public ValueTask CompleteActionAsync(string ownerId, string key, string requestId, string fingerprint, ActionResult result, CancellationToken token = default) => throw new IOException("private store exception");
}
internal sealed class WorkspaceHarness : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "IntentBlazor-tests-" + Guid.NewGuid().ToString("N"));
    public ServiceProvider Provider { get; }
    public IWorkspaceStore Store { get; }
    public SharedJobStore Jobs { get; }
    public StaffWorkspaces Staff { get; }
    public TestWorkspaceAccess Access { get; }
    public WorkspaceHarness(string storage = "memory", bool throwAfterSave = false, bool failReceipt = false)
    {
        Directory.CreateDirectory(DirectoryPath);
        Store = storage == "sqlite" ? new SqliteWorkspaceStore(Path.Combine(DirectoryPath, "workspaces.sqlite")) : new InMemoryWorkspaceStore();
        if (failReceipt) Store = new FailingReceiptStore(Store);
        Jobs = new(Path.Combine(DirectoryPath, "domain.sqlite"), TestContent.Catalog);
        Staff = new(TestContent.Catalog, Jobs); Access = new(Staff);
        ServiceCollection services = new(); services.AddLogging(); services.AddIntentBlazor();
        services.AddSingleton(Store); services.AddSingleton(Jobs); services.AddSingleton(Staff);
        services.AddSingleton<IWorkspaceAccess>(Access);
        services.AddScoped<MutableWorkspaceIdentity>(); services.AddScoped<IWorkspaceIdentity>(sp => sp.GetRequiredService<MutableWorkspaceIdentity>());
        services.AddScoped<DemoPermissions>(); services.AddScoped<IInputProcessor>(_ => Staff);
        if (throwAfterSave) services.AddIntentAction<ThrowAfterSaveHandler>(StaffFollowupAction.Definition);
        else services.AddIntentAction<StaffFollowupAction>(StaffFollowupAction.Definition);
        Provider = services.BuildServiceProvider(validateScopes: true);
    }
    public WorkspaceScope Scope(string? owner = "demo-alice", bool autoSave = false) => new(Provider.CreateScope(), owner, autoSave);
    public void Dispose() { Provider.Dispose(); Directory.Delete(DirectoryPath, recursive: true); }
}
internal sealed class WorkspaceScope : IDisposable
{
    private readonly IServiceScope _scope;
    public AiSession Session { get; }
    public MutableWorkspaceIdentity Identity { get; }
    public DemoPermissions Permissions { get; }
    public IServiceProvider Services => _scope.ServiceProvider;
    public WorkspaceScope(IServiceScope scope, string? owner, bool autoSave)
    {
        _scope = scope; Identity = Services.GetRequiredService<MutableWorkspaceIdentity>(); Identity.Owner = owner;
        Permissions = Services.GetRequiredService<DemoPermissions>();
        Services.GetRequiredService<WorkspaceServices>().AutoSave = autoSave;
        Session = Services.GetRequiredService<AiSession>();
    }
    public void Dispose() => _scope.Dispose();
}
