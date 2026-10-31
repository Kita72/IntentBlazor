using IntentBlazor.AI;
using IntentBlazor.Blazor;
using IntentBlazor.Quickstart.Components;
using IntentBlazor.Runtime;

namespace IntentBlazor.Quickstart;

public static class QuickstartRegistration
{
    public static IServiceCollection AddQuickstart(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIntentBlazor();
        services.AddScoped<CatalogService>();
        services.AddScoped<IInputProcessor, CatalogInputProcessor>();
        services.AddIntentRenderer<ScoreContract, ScoreView>();

        // Leave Ai:Provider unset for authored, credential-free output.
        string? provider = configuration["Ai:Provider"];
        if (string.IsNullOrWhiteSpace(provider)) return services;
        string model = configuration["Ai:Model"] ?? throw new InvalidOperationException("Configure Ai:Model.");
        IntentModelBuilder ai = services.AddIntentBlazorAI(options =>
        {
            options.Timeout = TimeSpan.FromSeconds(45);
            options.MaximumModelCalls = 4;
            options.MaximumToolCalls = 4;
            options.MaximumCorrections = 1;
            options.MaximumOutputTokens = 4096;
        });
        switch (provider)
        {
            case "Ollama": ai.UseOllama(model); break;
            case "LMStudio": ai.UseLMStudio(model); break;
            case "OpenAI":
                ai.UseOpenAI(model, sp => sp.GetRequiredService<IConfiguration>()["Ai:ApiKey"]
                ?? throw new InvalidOperationException("Configure the server-side Ai:ApiKey secret.")); break;
            default: throw new InvalidOperationException("Use Ollama, LMStudio, or OpenAI for Ai:Provider.");
        }

        if (configuration.GetValue<bool>("Ai:ExplicitTools"))
            ai.AddTool<CatalogService>(nameof(CatalogService.Read), "catalog_items", "Read the synthetic item catalog.");
        else
            ai.AddTools<CatalogService>();
        ai.AddDataSource<CatalogService>();
        return services;
    }
}
