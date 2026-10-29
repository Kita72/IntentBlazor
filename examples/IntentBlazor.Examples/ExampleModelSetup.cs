using IntentBlazor.AI;
using IntentBlazor.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Examples;

public sealed record ExampleModelState(bool Enabled, string Provider, string Model);

public static class ExampleModelSetup
{
    public static ExampleModelState AddExampleModel(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ExampleCatalog>();
        string? provider = configuration["Ai:Provider"];
        if (string.IsNullOrWhiteSpace(provider) || provider.Equals("Fixture", StringComparison.OrdinalIgnoreCase))
        {
            services.AddIntentBlazor();
            services.AddScoped<IInputProcessor, ExampleInputProcessor>();
            ExampleModelState disabled = new(false, "Fixture", "No model configured");
            services.AddSingleton(disabled);
            return disabled;
        }
        string model = configuration["Ai:Model"] ?? throw new InvalidOperationException("Set Ai:Model to an installed or enabled model ID.");
        IntentModelBuilder builder = services.AddIntentBlazorAI().AddTools<NewsDataService>();
        Uri? endpoint = configuration["Ai:Endpoint"] is { Length: > 0 } address ? new Uri(address, UriKind.Absolute) : null;
        switch (provider.ToLowerInvariant())
        {
            case "ollama": builder.UseOllama(model, endpoint); break;
            case "lmstudio":
                builder.UseLMStudio(model, endpoint, sp =>
                configuration["Ai:ApiKey"] ?? "lm-studio"); break;
            case "openai":
                builder.UseOpenAI(model, sp =>
                configuration["Ai:ApiKey"] ?? throw new InvalidOperationException("The host's OpenAI credential is not configured.")); break;
            case "codex":
                builder.UseCodex(new()
                { Model = model, ExecutablePath = configuration["Ai:CodexExecutable"] ?? "codex" }); break;
            default: throw new InvalidOperationException("Ai:Provider must be Fixture, Ollama, LMStudio, OpenAI, or Codex.");
        }
        ExampleModelState enabled = new(true, provider, model);
        services.AddSingleton(enabled);
        return enabled;
    }
}
