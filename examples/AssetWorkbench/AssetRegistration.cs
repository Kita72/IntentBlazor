using IntentBlazor.AssetWorkbench.Components;
using IntentBlazor.Blazor;
using IntentBlazor.Content;
using IntentBlazor.ExampleHost;
using IntentBlazor.Packs.ImageEditor;

namespace IntentBlazor.AssetWorkbench;

public static class AssetRegistration
{
    public static IServiceCollection AddAssetWorkbench(this IServiceCollection services, IConfiguration configuration, string directory)
    {
        services.AddIntentContent(Path.Combine(directory, "private-artifacts")); services.AddIntentImageEditor();
        services.AddIntentRenderer<AssetVersionsContract, AssetVersions>();
        services.AddExampleScenario<AssetScenario>(configuration["Ai:Provider"] is { Length: > 0 } provider && !provider.Equals("Fixture", StringComparison.OrdinalIgnoreCase));
        return services;
    }
}
