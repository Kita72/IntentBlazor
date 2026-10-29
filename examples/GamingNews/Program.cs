using IntentBlazor.Examples;
using IntentBlazor.GamingNews;

var builder = WebApplication.CreateBuilder(args);
ExampleModelState connection = builder.Services.AddExampleModel(builder.Configuration);
if (connection.Enabled && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Enable the sample's model mode only in Development; production access control is application-owned.");
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
