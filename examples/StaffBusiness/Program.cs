using IntentBlazor.ExampleHost;
using IntentBlazor.StaffBusiness;
using IntentBlazor.StaffBusiness.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var host = builder.Services.AddExampleHost(builder.Configuration, builder.Environment, "staff", "Staff Business",
    "Coordinate synthetic jobs waiting on materials. Private drafts and shared assignments have separate persistence and permissions.", staffAccounts: true);
builder.Services.AddStaffBusiness(builder.Configuration, host.DataDirectory);
var app = builder.Build();
await EfStaffRepository.InitializeAsync(app.Services.GetRequiredService<IDbContextFactory<BusinessDbContext>>());
app.UseExampleHost(); app.MapStaticAssets(); app.MapRazorComponents<App>().AddInteractiveServerRenderMode(); app.Run();
