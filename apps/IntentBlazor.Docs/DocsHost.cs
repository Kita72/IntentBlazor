using System.Reflection;
using IntentBlazor.Docs.Components;
using IntentBlazor.Docs.Documentation;
using IntentBlazor.Examples;

namespace IntentBlazor.Docs;

public static class DocsHost
{
    public static WebApplication Build(WebApplicationOptions options, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(options);
        configure?.Invoke(builder);
        builder.Services.AddSingleton<DocumentationCatalog>();
        ExampleModelState model = builder.Services.AddExampleModel(builder.Configuration);
        if (model.Enabled && !builder.Environment.IsDevelopment())
            throw new InvalidOperationException("The anonymous docs model demo can only be enabled in Development. Keep deployed docs in fixture mode.");
        builder.AddWorkspaceDemo();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        var app = builder.Build();
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
            app.UseHttpsRedirection();
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.MapWorkspaceDemo();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapGet("/health/ready", () =>
        {
            string informational = typeof(DocsHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
            string[] parts = informational.Split('+', 2);
            return Results.Json(new
            {
                status = "ready",
                version = parts[0],
                sourceRevision = parts.Length == 2 ? parts[1] : "unknown",
                mode = model.Enabled ? "configured-model" : "fixture",
                hosting = "InteractiveServer"
            });
        });
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return app;
    }
}
