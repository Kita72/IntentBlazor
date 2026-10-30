using System.Net;
using System.Text.Json;
using IntentBlazor.ExampleHost;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntentBlazor.Tests;

public sealed class Phase5AuthenticationTests
{
    [Fact]
    public async Task SyntheticSignInAndSignOut_RequireAntiforgeryAndIssueOnlyAllowlistedRoles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "IntentBlazor-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = directory });
            builder.WebHost.UseTestServer(); builder.Configuration["Example:DataDirectory"] = directory;
            builder.Services.AddExampleHost(builder.Configuration, builder.Environment, "test-staff", "Staff", "Synthetic", true);
            builder.Services.RemoveAll<ExampleWorkspaceController>(); // This host maps only the HTTP identity boundary, without an interactive workspace.
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            await using var app = builder.Build(); app.UseExampleHost();
            app.MapGet("/test/token", (HttpContext context, IAntiforgery antiforgery) => Results.Text(antiforgery.GetAndStoreTokens(context).RequestToken!));
            app.MapGet("/test/identity", (HttpContext context) => Results.Json(new { name = context.User.Identity!.Name, reader = context.User.IsInRole("Reader"), dispatcher = context.User.IsInRole("Dispatcher") })).RequireAuthorization();
            await app.StartAsync(); using HttpClient client = app.GetTestClient();
            using var missing = await client.PostAsync("/example/sign-in", Form("riley")); Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            using var tokenResponse = await client.GetAsync("/test/token"); string token = await tokenResponse.Content.ReadAsStringAsync();
            string cookie = string.Join("; ", tokenResponse.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));
            client.DefaultRequestHeaders.Add("Cookie", cookie);
            using var invalid = await client.PostAsync("/example/sign-in", Form("invented-admin", token)); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            using var signIn = await client.PostAsync("/example/sign-in", Form("riley", token)); Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
            string authenticated = signIn.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(".IntentBlazor.Example."));
            Assert.Contains("httponly", authenticated.ToLowerInvariant()); Assert.Contains("samesite=strict", authenticated.ToLowerInvariant());
            Assert.Contains("expires=", authenticated.ToLowerInvariant());
            client.DefaultRequestHeaders.Remove("Cookie"); client.DefaultRequestHeaders.Add("Cookie", cookie + "; " + authenticated.Split(';')[0]);
            using var identity = await client.GetAsync("/test/identity"); Assert.Equal(HttpStatusCode.OK, identity.StatusCode);
            using var payload = JsonDocument.Parse(await identity.Content.ReadAsStringAsync()); Assert.True(payload.RootElement.GetProperty("reader").GetBoolean()); Assert.False(payload.RootElement.GetProperty("dispatcher").GetBoolean());
            using var missingSignOut = await client.PostAsync("/example/sign-out", new FormUrlEncodedContent([])); Assert.Equal(HttpStatusCode.BadRequest, missingSignOut.StatusCode);
            // Tokens are bound to the authenticated principal, so obtain the post-login token.
            using var currentToken = await client.GetAsync("/test/token"); string logoutToken = await currentToken.Content.ReadAsStringAsync();
            using var signOut = await client.PostAsync("/example/sign-out", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = logoutToken }));
            Assert.Equal(HttpStatusCode.Redirect, signOut.StatusCode); Assert.Contains(signOut.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".IntentBlazor.Example.") && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
            await app.StopAsync();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
    private static FormUrlEncodedContent Form(string user, string? token = null)
    {
        Dictionary<string, string> values = new() { ["user"] = user, ["role"] = "Dispatcher" };
        if (token is not null) values["__RequestVerificationToken"] = token;
        return new(values);
    }
}
