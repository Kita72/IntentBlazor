using System.Collections.Concurrent;
using System.Security.Claims;
using IntentBlazor.AI;
using IntentBlazor.ProductionHost;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

internal sealed class HostTestAuthentication : AuthenticationStateProvider
{
    public ClaimsPrincipal Principal { get; set; } = new(new ClaimsIdentity());
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(Principal));
    public static ClaimsPrincipal User(string subject = "alex", string tenant = "tenant-a") => new(new ClaimsIdentity([
        new(ClaimTypes.NameIdentifier, subject), new(HostClaims.TenantClaim, tenant)
    ], "TestServerOnly"));
}

internal sealed class HostTestAccounts : IHostAccountDirectory
{
    private readonly ConcurrentDictionary<(string, string), HostAccount> _accounts = new();
    public void Set(HostAccount account) => _accounts[(account.Tenant, account.Subject)] = account;
    public void Revoke(string tenant = "tenant-a", string subject = "alex") => _accounts.TryRemove((tenant, subject), out _);
    public ValueTask<HostAccount?> FindAsync(string tenant, string subject, CancellationToken token)
    { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(_accounts.GetValueOrDefault((tenant, subject))); }
}

internal sealed class HostTestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 7, 23, 59, 59, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class HostHarness : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "IntentBlazor-host-" + Guid.NewGuid().ToString("N"));
    public HostPaths Paths { get; }
    public HostTestAccounts Accounts { get; } = new();
    public HostTestClock Clock { get; } = new();
    public ServiceProvider Provider { get; private set; }
    private readonly bool _attributeTools;
    private readonly Action<IntentModelBuilder>? _configureModel;
    public HostHarness(bool attributeTools = true, Action<IntentModelBuilder>? configureModel = null)
    {
        _attributeTools = attributeTools; _configureModel = configureModel;
        Directory.CreateDirectory(Path.Combine(Root, "app"));
        Paths = HostPaths.Create(Path.Combine(Root, "private"), Path.Combine(Root, "app"));
        foreach (var account in new[] { new HostAccount("tenant-a", "alex", true, true), new("tenant-a", "bob", true, false), new("tenant-b", "alex", true, false) }) Accounts.Set(account);
        Provider = BuildProvider();
    }
    private ServiceProvider BuildProvider()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        ServiceCollection services = new(); services.AddLogging(); services.AddSingleton(config);
        services.AddHostWorkspace(config, Paths, _configureModel, _attributeTools);
        services.AddSingleton<IHostAccountDirectory>(Accounts); services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<HostTestAuthentication>(); services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<HostTestAuthentication>());
        return services.BuildServiceProvider(validateScopes: true);
    }
    public void Restart() { Provider.Dispose(); Provider = BuildProvider(); }
    public HostScope Scope(string subject = "alex", string tenant = "tenant-a") => new(Provider.CreateScope(), HostTestAuthentication.User(subject, tenant));
    public void Dispose()
    {
        Provider.Dispose();
        if (!Path.GetFullPath(Root).StartsWith(Path.Combine(Path.GetTempPath(), "IntentBlazor-host-"), StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected test root.");
        Directory.Delete(Root, recursive: true);
    }
}

internal sealed class HostScope : IDisposable
{
    private readonly IServiceScope _scope;
    public IServiceProvider Services => _scope.ServiceProvider;
    public AiSession Session => Services.GetRequiredService<AiSession>();
    public HostNotes Notes => Services.GetRequiredService<HostNotes>();
    public HostIdentity Identity => Services.GetRequiredService<HostIdentity>();
    public HostTestAuthentication Authentication => Services.GetRequiredService<HostTestAuthentication>();
    public HostScope(IServiceScope scope, ClaimsPrincipal user)
    { _scope = scope; Authentication.Principal = user; Services.GetRequiredService<WorkspaceServices>().AutoSave = false; }
    public async Task StartAsync()
    {
        var opened = await Services.GetRequiredService<HostWorkspaceController>().OpenAsync("notes"); Assert.True(opened.Success, string.Join(",", opened.Errors.Select(error => error.Code)));
        var result = await Session.SubmitAsync("Show my private note"); Assert.True(result.Success, string.Join(",", result.Errors.Select(error => error.Code)));
    }
    public void Edit(string text) => Assert.True(Session.Change("draft", text, Session.Revision).Success);
    public async Task<ActionResult> SaveNoteAsync() => await Session.ExecuteActionAsync(Assert.IsType<ActionRequest>(Session.PrepareAction("save", Session.Revision)));
    public void Dispose() => _scope.Dispose();
}
