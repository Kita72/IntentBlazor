using IntentBlazor.Docs;
using IntentBlazor.Docs.Components;
using IntentBlazor.Examples;
using IntentBlazor.Runtime;

var builder = WebApplication.CreateBuilder(args);
ExampleModelState model = builder.Services.AddExampleModel(builder.Configuration);
if (model.Enabled && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("The anonymous docs model demo can only be enabled in Development. Keep deployed docs in fixture mode.");
builder.AddWorkspaceDemo();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.MapWorkspaceDemo();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
