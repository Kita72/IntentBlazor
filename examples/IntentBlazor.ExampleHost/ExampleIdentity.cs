using System.Collections.Immutable;
using System.Security.Claims;
using IntentBlazor.Runtime;
using Microsoft.AspNetCore.Components.Authorization;

namespace IntentBlazor.ExampleHost;

public sealed record ExampleActor(string Id, string Name, ImmutableHashSet<string> Roles)
{ public bool CanAssign => Roles.Contains("Dispatcher"); }
public interface IExampleActor { ValueTask<ExampleActor?> ReadAsync(CancellationToken token = default); }
public sealed class ExampleIdentity(AuthenticationStateProvider authentication) : IExampleActor, IWorkspaceIdentity
{
    public async ValueTask<ExampleActor?> ReadAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); ClaimsPrincipal user = (await authentication.GetAuthenticationStateAsync()).User;
        return user.Identity?.IsAuthenticated == true && user.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } id
            ? new(id, user.Identity.Name ?? id, user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToImmutableHashSet(StringComparer.Ordinal)) : null;
    }
    public async ValueTask<string?> GetOwnerIdAsync(CancellationToken token) => (await ReadAsync(token))?.Id;
}
public sealed record ExampleUser(string Id, string Name, string Role);
public sealed record ExampleHostOptions(string Key, string Title, string Description, string DataDirectory, bool StaffAccounts)
{
    public IReadOnlyList<ExampleUser> Users => StaffAccounts
        ? [new("alex", "Alex · dispatcher", "Dispatcher"), new("sam", "Sam · dispatcher", "Dispatcher"), new("riley", "Riley · read only", "Reader")]
        : [new("private", "Create my private example identity", "Member")];
    public ClaimsPrincipal? CreatePrincipal(string? selected)
    {
        ExampleUser? user = Users.SingleOrDefault(item => item.Id == selected);
        return user is null ? null : new(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, StaffAccounts ? user.Id : Key + "-" + Guid.NewGuid().ToString("N")),
            new(ClaimTypes.Name, user.Name), new(ClaimTypes.Role, user.Role)
        ], "Cookies"));
    }
}
