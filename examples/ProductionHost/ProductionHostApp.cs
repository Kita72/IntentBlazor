using System.Net;
using System.Security.Cryptography.X509Certificates;
using IntentBlazor.Content;
using IntentBlazor.ProductionHost.Components;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace IntentBlazor.ProductionHost;

public static class ProductionHostApp
{
    public static WebApplication Build(WebApplicationOptions options, Action<WebApplicationBuilder>? configure = null,
        Action<IDataProtectionBuilder>? protectKeys = null, Action<IntentBlazor.AI.IntentModelBuilder>? configureModel = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(options);
        string storage = builder.Configuration["Host:DataDirectory"] ?? (builder.Environment.IsDevelopment()
            ? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "artifacts", "production-host"))
            : throw new InvalidOperationException("Configure Host:DataDirectory outside the application directory before production startup."));
        HostPaths paths = HostPaths.Create(storage, builder.Environment.ContentRootPath);
        builder.Services.AddHostWorkspace(builder.Configuration, paths, configureModel);
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(cookie =>
        {
            cookie.Cookie.Name = "__Host-IntentBlazorRecipe"; cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Strict; cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookie.ExpireTimeSpan = TimeSpan.FromHours(1); cookie.SlidingExpiration = false;
            cookie.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
            cookie.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        });
        IDataProtectionBuilder protection = builder.Services.AddDataProtection().SetApplicationName("IntentBlazor.ProductionRecipe").PersistKeysToFileSystem(new(paths.Keys));
        if (protectKeys is not null) protectKeys(protection);
        else if (builder.Configuration["Host:KeyEncryptionCertificateThumbprint"] is { Length: > 0 } thumbprint)
        {
            using X509Store store = new(StoreName.My, StoreLocation.CurrentUser); store.Open(OpenFlags.ReadOnly);
            X509Certificate2? certificate = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: true).OfType<X509Certificate2>().FirstOrDefault(item => item.HasPrivateKey);
            if (certificate is null) throw new InvalidOperationException("The host key-encryption certificate with its private key is unavailable.");
            protection.ProtectKeysWithCertificate(certificate);
        }
        else if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
        else if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Configure host data-protection key encryption before production startup.");
        string? proxy = builder.Configuration["Host:KnownProxy"];
        if (proxy is not null) builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwarded.ForwardLimit = 1; forwarded.KnownProxies.Clear(); forwarded.KnownIPNetworks.Clear(); forwarded.KnownProxies.Add(IPAddress.Parse(proxy));
        });
        configure?.Invoke(builder);
        WebApplication app = builder.Build();
        if (proxy is not null) app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler(error => error.Run(async context => { context.Response.StatusCode = 500; await context.Response.WriteAsync("The host request failed."); }));
            app.UseHsts(); app.UseHttpsRedirection();
        }
        app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
        app.MapGet("/health/ready", () => Results.Ok(new { mode = configureModel is null ? "authored-offline" : "host-configured", hosting = "InteractiveServer" })).AllowAnonymous();
        app.MapIntentContent(context => HostClaims.OwnerOf(context.User)).RequireAuthorization(HostClaims.SessionPolicy);
        app.MapStaticAssets(); app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return app;
    }
}
