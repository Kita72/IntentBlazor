using IntentBlazor.ExampleHost;
using IntentBlazor.GamingNews;

var builder = WebApplication.CreateBuilder(args);
var host = builder.Services.AddExampleHost(builder.Configuration, builder.Environment, "news", "Gaming News and Tips",
    "Explore dated updates, prioritize games, compare version-aware tips, and keep a private library of bookmarks and comparisons.");
builder.Services.AddGamingNews(builder.Configuration, host.DataDirectory);
var app = builder.Build();
app.UseExampleHost(); app.MapStaticAssets(); app.MapRazorComponents<App>().AddInteractiveServerRenderMode(); app.Run();
