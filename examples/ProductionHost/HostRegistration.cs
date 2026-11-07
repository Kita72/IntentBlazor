using IntentBlazor.AI;
using IntentBlazor.Content;
using IntentBlazor.Runtime;
using IntentBlazor.Storage.Sqlite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntentBlazor.ProductionHost;

public static class HostRegistration
{
    public static IServiceCollection AddHostWorkspace(this IServiceCollection services, IConfiguration configuration, HostPaths paths,
        Action<IntentModelBuilder>? configureModel = null, bool attributeTools = true)
    {
        services.AddSingleton(paths);
        services.AddRazorComponents().AddInteractiveServerComponents(options =>
        {
            options.DetailedErrors = false; options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(2);
            options.DisconnectedCircuitMaxRetained = 50;
        });
        services.AddCascadingAuthenticationState();
        services.Configure<HostGrantOptions>(configuration.GetSection("Host"));
        services.TryAddSingleton<IHostAccountDirectory, ConfiguredHostAccounts>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<HostIdentity>(); services.AddScoped<IWorkspaceIdentity>(sp => sp.GetRequiredService<HostIdentity>());
        services.AddScoped<HostNotes>(); services.AddScoped<IWorkspaceAccess, HostWorkspaceAccess>();
        services.AddScoped<HostWorkspaceController>(); services.AddScoped<HostOfflineProcessor>();
        services.AddSqliteWorkspaces(paths.Workspaces);
        services.AddIntentContent(paths.Artifacts, storage: new(paths.Artifacts, MaximumOwnerBytes: 16 * 1024 * 1024, MaximumArtifacts: 100));
        services.AddIntentAction<HostSaveNote>(HostSaveNote.Definition);
        services.AddSingleton(configuration.GetSection("Host:Requests").Get<HostRequestLimits>() ?? new());
        services.AddSingleton<HostRequestBudget>();
        services.AddOptions<ModelOptions>().Configure(options =>
        {
            options.Timeout = TimeSpan.FromSeconds(30); options.MaximumOutputTokens = 1024; options.MaximumContextCharacters = 8000;
            options.MaximumModelCalls = 4; options.MaximumToolCalls = 4; options.MaximumCorrections = 0;
        });
        IntentModelBuilder ai = configureModel is null ? new(services) : services.AddIntentBlazorAI();
        ai.AddDataSource<HostReadSource>();
        if (attributeTools) ai.AddTools<HostNotes>();
        else ai.AddTool<HostNotes>(nameof(HostNotes.ReadAsync), "read_private_notes", "Read the authenticated account's private note.");
        configureModel?.Invoke(ai);
        // Install last: every request, including a configured model's tools/corrections, holds one admission.
        services.RemoveAll<IInputProcessor>();
        services.AddScoped<IInputProcessor>(sp => new HostLimitedInputProcessor(sp.GetRequiredService<HostIdentity>(),
            sp.GetRequiredService<HostRequestBudget>(), configureModel is null ? sp.GetRequiredService<HostOfflineProcessor>() : sp.GetRequiredService<ModelInputProcessor>()));
        services.AddIntentAiSetup<HostAiSetupAccess>();
        services.AddAuthorization(options => options.AddPolicy(HostClaims.SessionPolicy, policy => policy.RequireAuthenticatedUser().AddRequirements(new HostAccountRequirement())));
        services.AddScoped<IAuthorizationHandler, HostAccountAuthorizationHandler>();
        return services;
    }
}

public sealed class HostAccountRequirement : IAuthorizationRequirement;
public sealed class HostAccountAuthorizationHandler(IHostAccountDirectory directory) : AuthorizationHandler<HostAccountRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, HostAccountRequirement requirement)
    {
        if (HostClaims.Read(context.User) is { } id && await directory.FindAsync(id.Tenant, id.Subject, CancellationToken.None) is { } account &&
            account.Tenant == id.Tenant && account.Subject == id.Subject) context.Succeed(requirement);
    }
}
