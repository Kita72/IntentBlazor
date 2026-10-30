using IntentBlazor.AssetWorkbench;
using IntentBlazor.AssetWorkbench.Components;
using IntentBlazor.Content;
using IntentBlazor.ExampleHost;

var builder = WebApplication.CreateBuilder(args);
var host = builder.Services.AddExampleHost(builder.Configuration, builder.Environment, "assets", "Asset Workbench",
    "Import private images, inspect inconsistent dimensions, resize immutable versions, and export the displayed working artifact.");
builder.Services.AddAssetWorkbench(builder.Configuration, host.DataDirectory);
var app = builder.Build();
app.UseExampleHost(); app.MapStaticAssets(); app.MapIntentContent(); app.MapRazorComponents<App>().AddInteractiveServerRenderMode(); app.Run();
