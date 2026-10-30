using IntentBlazor.AI;
using IntentBlazor.Runtime;
using IntentBlazor.Storage.Sqlite;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IntentBlazor.ExampleHost;

public sealed record ExampleModelMode(bool Enabled, string Provider, string Model);
public static class ExampleRegistration
{
    public static ExampleHostOptions AddExampleHost(this IServiceCollection services, IConfiguration configuration,
        IWebHostEnvironment environment, string key, string title, string description, bool staffAccounts = false)
    {
        if (!environment.IsDevelopment()) throw new InvalidOperationException("Synthetic-account examples run only in Development. Integrate production identity before deploying.");
        string directory = Path.GetFullPath(configuration["Example:DataDirectory"] ?? Path.Combine(environment.ContentRootPath, "..", "..", "artifacts", "phase5-" + key));
        Directory.CreateDirectory(directory);
        ExampleHostOptions options = new(key, title, description, directory, staffAccounts); services.AddSingleton(options);
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddCascadingAuthenticationState(); services.AddAuthorization();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(cookie =>
        {
            cookie.Cookie.Name = ".IntentBlazor.Example." + key; cookie.Cookie.HttpOnly = true; cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.LoginPath = "/sign-in"; cookie.ExpireTimeSpan = TimeSpan.FromHours(4);
        });
        services.AddDataProtection().SetApplicationName("IntentBlazor.Example." + key);
        services.AddScoped<ExampleIdentity>(); services.AddScoped<IExampleActor>(sp => sp.GetRequiredService<ExampleIdentity>());
        services.AddScoped<IWorkspaceIdentity>(sp => sp.GetRequiredService<ExampleIdentity>());
        services.AddSqliteWorkspaces(Path.Combine(directory, "workspaces.sqlite")); services.AddScoped<ExampleWorkspaceController>();
        ConfigureModel(services, configuration); services.AddIntentAiSetup<ExampleAiAccess>(); return options;
    }
    public static IServiceCollection AddExampleScenario<T>(this IServiceCollection services, bool modelEnabled) where T : class, IExampleScenario
    {
        services.AddScoped<T>(); services.AddScoped<IExampleScenario>(sp => sp.GetRequiredService<T>());
        services.AddScoped<IWorkspaceAccess>(sp => sp.GetRequiredService<T>()); services.AddScoped<IOutputDataSource>(sp => sp.GetRequiredService<T>());
        if (!modelEnabled) services.AddScoped<IInputProcessor>(sp => sp.GetRequiredService<T>());
        return services;
    }
    public static void UseExampleHost(this WebApplication app)
    {
        app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
        app.MapPost("/example/sign-in", async (HttpContext context, IAntiforgery antiforgery, ExampleHostOptions options) =>
        {
            try { await antiforgery.ValidateRequestAsync(context); } catch (AntiforgeryValidationException) { return Results.BadRequest(); }
            var form = await context.Request.ReadFormAsync(context.RequestAborted); var principal = options.CreatePrincipal(form["user"].ToString());
            if (principal is null) return Results.BadRequest();
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = true }); return Results.LocalRedirect("/");
        });
        app.MapPost("/example/sign-out", async (HttpContext context, IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(context); } catch (AntiforgeryValidationException) { return Results.BadRequest(); }
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return Results.LocalRedirect("/sign-in");
        }).RequireAuthorization();
    }
    private static void ConfigureModel(IServiceCollection services, IConfiguration configuration)
    {
        string provider = configuration["Ai:Provider"] ?? "Fixture";
        if (provider.Equals("Fixture", StringComparison.OrdinalIgnoreCase))
        { services.AddSingleton(new ExampleModelMode(false, "Fixture", "No model requests")); return; }
        string model = configuration["Ai:Model"] ?? throw new InvalidOperationException("Set Ai:Model to an installed or enabled model identifier.");
        var ai = services.AddIntentBlazorAI(options => options.MaximumOutputTokens = 4096);
        Uri? endpoint = configuration["Ai:Endpoint"] is { Length: > 0 } address ? new(address, UriKind.Absolute) : null;
        switch (provider.ToLowerInvariant())
        {
            case "ollama": ai.UseOllama(model, endpoint); break;
            case "lmstudio": ai.UseLMStudio(model, endpoint, _ => configuration["Ai:ApiKey"] ?? "lm-studio"); break;
            case "openai": ai.UseOpenAI(model, _ => configuration["Ai:ApiKey"] ?? throw new InvalidOperationException("Configure the host credential outside source.")); break;
            case "codex": ai.UseCodex(new() { Model = model, ExecutablePath = configuration["Ai:CodexExecutable"] ?? "codex" }); break;
            default: throw new InvalidOperationException("Ai:Provider must be Fixture, Ollama, LMStudio, OpenAI, or Codex.");
        }
        services.AddSingleton(new ExampleModelMode(true, provider, model));
    }
}
public sealed class ExampleAiAccess(IExampleActor actor) : IAiSetupAccess
{
    public async ValueTask<AiSetupPolicy> GetPolicyAsync(CancellationToken token) => await actor.ReadAsync(token) is not null
        ? new(true, true, true, true, true, MaximumOutputTokens: 4096) : new();
}
