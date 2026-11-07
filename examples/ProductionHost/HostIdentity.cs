using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IntentBlazor.Runtime;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace IntentBlazor.ProductionHost;

public sealed record HostAccount(string Tenant, string Subject, bool CanEdit = false, bool CanConfigureAi = false)
{
    public string OwnerId => HostClaims.OwnerId(Tenant, Subject);
}

public sealed class HostGrantOptions
{
    public List<HostAccount> Accounts { get; set; } = [];
}

// Replace with the application's current account/permission database when integrating real login.
public interface IHostAccountDirectory
{
    ValueTask<HostAccount?> FindAsync(string tenant, string subject, CancellationToken token);
}

public sealed class ConfiguredHostAccounts(IOptionsMonitor<HostGrantOptions> grants) : IHostAccountDirectory
{
    public ValueTask<HostAccount?> FindAsync(string tenant, string subject, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        HostAccount[] matches = grants.CurrentValue.Accounts.Where(account => account.Tenant == tenant && account.Subject == subject).Take(2).ToArray();
        return ValueTask.FromResult(matches.Length == 1 ? matches[0] : null);
    }
}

public static class HostClaims
{
    public const string TenantClaim = "tenant_id";
    public const string SessionPolicy = "HostWorkspace";

    public static (string Tenant, string Subject)? Read(ClaimsPrincipal principal)
    {
        // Bind both identifiers to one authenticated identity. Ambiguous/missing claims fail closed.
        ClaimsIdentity[] identities = principal.Identities.Where(identity => identity.IsAuthenticated).Take(2).ToArray();
        if (identities.Length != 1) return null;
        string[] tenants = identities[0].FindAll(TenantClaim).Select(claim => claim.Value).ToArray();
        string[] subjects = identities[0].FindAll(ClaimTypes.NameIdentifier).Select(claim => claim.Value).ToArray();
        return tenants.Length == 1 && subjects.Length == 1 && Valid(tenants[0]) && Valid(subjects[0]) ? (tenants[0], subjects[0]) : null;
    }

    public static string? OwnerOf(ClaimsPrincipal principal) => Read(principal) is { } id ? OwnerId(id.Tenant, id.Subject) : null;
    public static string OwnerId(string tenant, string subject) => "host-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tenant + "\0" + subject)));
    private static bool Valid(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && !value.Any(char.IsControl);
}

public sealed class HostIdentity(AuthenticationStateProvider authentication, IHostAccountDirectory directory) : IWorkspaceIdentity
{
    public async ValueTask<HostAccount?> CurrentAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ClaimsPrincipal principal = (await authentication.GetAuthenticationStateAsync()).User;
        if (HostClaims.Read(principal) is not { } id) return null;
        HostAccount? account = await directory.FindAsync(id.Tenant, id.Subject, token);
        // A directory must not substitute another account for the authenticated subject.
        return account?.Tenant == id.Tenant && account.Subject == id.Subject ? account : null;
    }

    public async ValueTask<HostAccount> RequireAsync(CancellationToken token = default) =>
        await CurrentAsync(token) ?? throw new InputRejectedException("A current authenticated host account is required.");
    public async ValueTask<string?> GetOwnerIdAsync(CancellationToken token) => (await CurrentAsync(token))?.OwnerId;
}
