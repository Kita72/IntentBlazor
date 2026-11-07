using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using IntentBlazor.Content;
using IntentBlazor.ProductionHost;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntentBlazor.Tests;

public sealed class FoundationHostHttpTests
{
    [Fact]
    public async Task ProductionDefaults_DenyAnonymousAndSpoofedIdentityWithoutLoginShortcutsOrModelClients()
    {
        using HostHarness host = new(); using X509Certificate2 certificate = Certificate();
        await using WebApplication app = Build(host, certificate); await app.StartAsync();
        Assert.Empty(app.Services.GetServices<Microsoft.Extensions.AI.IChatClient>());
        using HttpClient client = app.GetTestClient(); client.BaseAddress = new("https://localhost");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Contains("authored-offline", await client.GetStringAsync("/health/ready"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/workspaces")).StatusCode);
        client.DefaultRequestHeaders.Add("X-User", "alex"); client.DefaultRequestHeaders.Add("X-Tenant", "tenant-a");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/workspace/notes")).StatusCode);
        foreach (string route in new[] { "/example/sign-in", "/workspaces/demo-signin", "/_test/login/alex", "/workspaces.sqlite", "/keys" })
        {
            using HttpResponseMessage response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.False(response.Headers.Contains("Set-Cookie"));
        }
        Assert.DoesNotContain("alex", await client.GetStringAsync("/"));
        await app.StopAsync();
    }

    [Fact]
    public async Task AuthenticatedHttp_ReauthorizesArtifactOwnersAndRevokedAccountsUsingProtectedCookies()
    {
        using HostHarness host = new(); using X509Certificate2 certificate = Certificate();
        ContentArtifact artifact;
        using (HostScope scope = host.Scope())
        {
            byte[] bytes = Encoding.UTF8.GetBytes("Only Alex can download this attachment.");
            await using MemoryStream stream = new(bytes);
            artifact = await scope.Services.GetRequiredService<IWorkspaceContent>().ImportAsync(new("private.txt", "text/plain", bytes.Length), stream);
        }
        await using WebApplication app = Build(host, certificate);
        // This route exists only in this TestServer test assembly, never in the host recipe.
        app.MapPost("/_test/login/{subject}", async (string subject, HttpContext context) =>
        {
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, HostTestAuthentication.User(subject));
            return Results.NoContent();
        }).AllowAnonymous();
        await app.StartAsync();
        using HttpClient alex = await SignedIn(app, "alex"), bob = await SignedIn(app, "bob");
        string url = $"/intent-artifacts/{artifact.Reference.Id}/{artifact.Reference.Version}?type=text%2Fplain&download=true";
        using HttpResponseMessage own = await alex.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode); Assert.Equal("Only Alex can download this attachment.", await own.Content.ReadAsStringAsync());
        Assert.Contains("no-store", own.Headers.CacheControl!.ToString()); Assert.Equal("nosniff", Assert.Single(own.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(url)).StatusCode);
        Assert.Contains("My notes", await alex.GetStringAsync("/workspaces"));
        string workspace = await alex.GetStringAsync("/workspace/notes");
        Assert.Contains("Request", workspace); Assert.DoesNotContain("@onclick", workspace);
        host.Accounts.Revoke();
        Assert.Equal(HttpStatusCode.Forbidden, (await alex.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alex.GetAsync("/workspaces")).StatusCode);
        await app.StopAsync();
    }

    [Fact]
    public void ProductionStartup_RequiresPrivateStorageAndDoesNotReadAiProviderConfiguration()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ProductionHostApp.Build(new()
        {
            ApplicationName = typeof(ProductionHostApp).Assembly.GetName().Name,
            EnvironmentName = "Production",
            ContentRootPath = Path.Combine(FoundationHostTests.RepositoryRoot(), "examples", "ProductionHost"),
            Args = ["--Ai:Provider", "OpenAI", "--Ai:Model", "not-used"]
        }));
        Assert.Contains("Host:DataDirectory", error.Message);
    }

    private static WebApplication Build(HostHarness host, X509Certificate2 certificate) => ProductionHostApp.Build(new()
    {
        ApplicationName = typeof(ProductionHostApp).Assembly.GetName().Name,
        EnvironmentName = "Production",
        ContentRootPath = Path.Combine(FoundationHostTests.RepositoryRoot(), "examples", "ProductionHost"),
        Args = ["--Host:DataDirectory", host.Paths.Root]
    }, builder => { builder.WebHost.UseTestServer(); builder.Logging.ClearProviders(); builder.Services.AddSingleton<IHostAccountDirectory>(host.Accounts); },
        protectKeys: protection => protection.ProtectKeysWithCertificate(certificate));

    private static X509Certificate2 Certificate()
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=IntentBlazor-Offline-Test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static async Task<HttpClient> SignedIn(WebApplication app, string subject)
    {
        HttpClient client = app.GetTestClient(); client.BaseAddress = new("https://localhost");
        using HttpResponseMessage response = await client.PostAsync("/_test/login/" + subject, null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        string[] cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, cookie => cookie.Contains("secure", StringComparison.OrdinalIgnoreCase) && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies.Select(cookie => cookie.Split(';')[0])));
        return client;
    }
}
