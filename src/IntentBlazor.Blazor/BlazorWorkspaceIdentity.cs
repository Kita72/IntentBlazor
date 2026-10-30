using System.Security.Claims;
using IntentBlazor.Runtime;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntentBlazor.Blazor;

public sealed class BlazorWorkspaceIdentity(AuthenticationStateProvider authentication) : IWorkspaceIdentity
{
    public async ValueTask<string?> GetOwnerIdAsync(CancellationToken cancellationToken)
    {
        AuthenticationState state = await authentication.GetAuthenticationStateAsync().WaitAsync(cancellationToken);
        return state.User.Identity?.IsAuthenticated == true ? state.User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    }
}
public static class BlazorWorkspaceIdentityRegistration
{
    public static IServiceCollection AddBlazorWorkspaceIdentity(this IServiceCollection services)
    {
        services.AddIntentBlazor(); services.AddCascadingAuthenticationState();
        services.Replace(ServiceDescriptor.Scoped<IWorkspaceIdentity, BlazorWorkspaceIdentity>());
        return services;
    }
}
