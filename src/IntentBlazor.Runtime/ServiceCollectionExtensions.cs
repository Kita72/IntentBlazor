using IntentBlazor.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntentBlazor.Runtime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIntentBlazor(this IServiceCollection services)
    {
        services.TryAddSingleton<DocumentValidator>();
        services.TryAddScoped<IInputProcessor, UnconfiguredInputProcessor>();
        services.TryAddScoped<AiSession>();
        services.TryAddSingleton<IWorkspaceStore, InMemoryWorkspaceStore>();
        services.TryAddScoped<IWorkspaceIdentity, AnonymousWorkspaceIdentity>();
        services.TryAddScoped<IWorkspaceAccess, DeniedWorkspaceAccess>();
        services.TryAddScoped<WorkspaceServices>();
        return services;
    }

    public static IServiceCollection AddIntentAction<T>(this IServiceCollection services, ActionDefinition definition)
        where T : class, IWorkspaceActionHandler
    {
        if (string.IsNullOrWhiteSpace(definition.Name) || definition.Name.Length > 64 ||
            !definition.Name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ||
            string.IsNullOrWhiteSpace(definition.Label) || definition.Label.Length > 80 ||
            string.IsNullOrWhiteSpace(definition.DataSetId) || definition.Parameters is null || definition.Parameters.Count > 16 ||
            definition.Parameters.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 64 ||
                pair.Value is null || pair.Value.Kind is not (StateKind.Text or StateKind.Number or StateKind.Boolean)) ||
            services.Any(descriptor => descriptor.ImplementationInstance is ActionDefinition prior && prior.Name == definition.Name))
            throw new ArgumentException("Register a unique, labelled action with bounded scalar parameters.");
        services.AddSingleton(definition);
        services.AddSingleton(new ActionRegistration(definition.Name, typeof(T)));
        services.TryAddScoped<T>();
        return services;
    }

    private sealed class AnonymousWorkspaceIdentity : IWorkspaceIdentity
    { public ValueTask<string?> GetOwnerIdAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null); }
    private sealed class DeniedWorkspaceAccess : IWorkspaceAccess
    {
        public ValueTask<WorkspaceAccess> ReauthorizeAsync(string ownerId, System.Collections.Immutable.ImmutableArray<string> dataSets,
            System.Collections.Immutable.ImmutableArray<ArtifactReference> artifacts, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new WorkspaceAccess(false, []));
    }

    private sealed class UnconfiguredInputProcessor : IInputProcessor
    {
        public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken cancellationToken) =>
            ValueTask.FromException<OutputContent>(new InputRejectedException(
                "No input processor is configured. Register an IInputProcessor or open an authored document."));
    }
}
