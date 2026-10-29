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
        return services;
    }

    private sealed class UnconfiguredInputProcessor : IInputProcessor
    {
        public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken cancellationToken) =>
            ValueTask.FromException<OutputContent>(new InputRejectedException(
                "No input processor is configured. Register an IInputProcessor or open an authored document."));
    }
}
