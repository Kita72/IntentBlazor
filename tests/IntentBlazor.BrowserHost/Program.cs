using IntentBlazor.AI;
using IntentBlazor.BrowserHost;
using IntentBlazor.BrowserHost.Components;
using IntentBlazor.Runtime;
using IntentBlazor.Storage.Sqlite;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment() || !builder.Configuration.GetValue<bool>("Browser:Enabled"))
    throw new InvalidOperationException("This offline fixture requires Development and Browser:Enabled.");
string directory = Path.GetFullPath(builder.Configuration["Browser:DataDirectory"]
    ?? throw new InvalidOperationException("A private browser-test directory is required."));
builder.Services.AddDataProtection().PersistKeysToFileSystem(new(Path.Combine(directory, "keys")));
builder.Services.AddScoped<BrowserFixtureState>();
builder.Services.AddScoped<IWorkspaceIdentity, FixtureIdentity>();
builder.Services.AddScoped<IWorkspaceAccess, FixtureAccess>();
builder.Services.AddSqliteWorkspaces(Path.Combine(directory, "workspaces.sqlite"));
builder.Services.AddIntentBlazorAI(options =>
{
    options.Timeout = TimeSpan.FromMilliseconds(builder.Configuration.GetValue<int?>("Browser:TimeoutMilliseconds") ?? 30000);
    options.MaximumCorrections = 0; options.MaximumOutputTokens = 512; options.MaximumReportedTokens = 1024;
}).UseChatClient(sp => new OfflineChatClient(sp.GetRequiredService<BrowserFixtureState>()),
    new("Offline browser fixture", "scripted-only", null, NativeTools: false, SupportsAdvancedSampling: false))
    .AddDataSource<FixtureData>();
builder.Services.AddIntentAiSetup<FixtureSetupAccess>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/health/ready", () => new { mode = "offline-browser-fixture" });
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
