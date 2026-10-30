using IntentBlazor.Core;
using IntentBlazor.Runtime;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Blazor;

public sealed record OutputRendererRegistration(string Key, Type ComponentType);
public static class OutputRendererRegistrationExtensions
{
    public static IServiceCollection AddIntentRenderer<TContract, TRenderer>(this IServiceCollection services)
        where TContract : class, IOutputExtensionContract, new() where TRenderer : IComponent
    {
        services.AddIntentBlazor();
        TContract contract = new(); ExtensionDescriptor descriptor = contract.Descriptor;
        string key = DocumentValidator.ExtensionKey(descriptor.Pack, descriptor.Version, descriptor.Renderer);
        if (services.Any(item => item.ImplementationInstance is OutputRendererRegistration prior && prior.Key == key))
            throw new ArgumentException("Renderer identifiers and versions must be unique.");
        if (descriptor.Pack.Length is < 1 or > 64 || descriptor.Renderer.Length is < 1 or > 64 || descriptor.Description.Length is < 1 or > 500 ||
            descriptor.Version.Length is < 1 or > 32 || descriptor.Example.GetRawText().Length > 8192)
            throw new ArgumentException("Use a bounded renderer descriptor and configuration example.");
        services.AddSingleton<IOutputExtensionContract>(contract);
        services.AddSingleton(new OutputRendererRegistration(key, typeof(TRenderer)));
        return services;
    }
}

public abstract class IntentExtensionComponent<TConfiguration> : ComponentBase where TConfiguration : class
{
    [Parameter, EditorRequired] public AiSession Session { get; set; } = default!;
    [Parameter, EditorRequired] public OutputNode Node { get; set; } = default!;
    protected TConfiguration Configuration { get; private set; } = default!;
    protected override void OnParametersSet() => Configuration =
        OutputExtensionContract<TConfiguration, object>.Read<TConfiguration>(Node.Extension!.Configuration);
}
