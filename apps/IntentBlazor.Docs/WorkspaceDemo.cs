using System.Security.Claims;
using IntentBlazor.Blazor;
using IntentBlazor.Core;
using IntentBlazor.Examples;
using IntentBlazor.Runtime;
using IntentBlazor.Storage.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace IntentBlazor.Docs;

public sealed class RecoveryDemo(StaffWorkspaces staff, DocumentValidator validator, WorkspaceServices services,
    DemoPermissions permissions, SharedJobStore jobs) : IDisposable
{
    public AiSession Workspace { get; } = new(validator, staff, services);
    public DemoPermissions Permissions { get; } = permissions;
    public OutputContent CurrentRecords() => staff.Create();
    public RecoveryDemo NewSession() => new(staff, validator, services, Permissions, jobs);
    public ActionResult SaveAsOtherStaff(string id)
    {
        var row = jobs.Read()[0].Rows.Single(value => value["id"].GetString() == id);
        var context = new ActionContext("demo-other-staff", "other-private-chat", Guid.NewGuid().ToString("N"), id,
            JsonValues.Display(row["version"]), System.Collections.Immutable.ImmutableDictionary<string, System.Text.Json.JsonElement>.Empty
                .Add("notes", JsonValues.From("Updated by another synthetic staff member.")).Add("assignee", row["assignee"]));
        return jobs.Save(context, CancellationToken.None);
    }
    public void Dispose() => Workspace.Dispose();
}

public static class WorkspaceDemo
{
    public const string Scheme = "IntentBlazor.WorkspaceDemo";
    public static void AddWorkspaceDemo(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment()) return;
        string root = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../artifacts/phase3-demo"));
        builder.Services.AddAuthentication(Scheme).AddCookie(Scheme, options =>
        {
            options.Cookie.Name = Scheme;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.LoginPath = "/workspaces/demo-signin";
        });
        builder.Services.AddAuthorization();
        builder.Services.AddBlazorWorkspaceIdentity();
        builder.Services.AddSqliteWorkspaces(Path.Combine(root, "private-workspaces.sqlite"));
        builder.Services.AddSingleton(sp => new SharedJobStore(Path.Combine(root, "shared-jobs.sqlite"), sp.GetRequiredService<ExampleCatalog>()));
        builder.Services.AddScoped<DemoPermissions>();
        builder.Services.AddScoped<StaffWorkspaces>();
        builder.Services.AddScoped<IWorkspaceAccess>(sp => sp.GetRequiredService<StaffWorkspaces>());
        builder.Services.AddIntentAction<StaffFollowupAction>(StaffFollowupAction.Definition);
        builder.Services.AddScoped<RecoveryDemo>();
    }

    public static void MapWorkspaceDemo(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/workspaces/demo-signin", async (HttpContext context) =>
        {
            // Each browser gets a protected, synthetic demo identity. This is not a production account system.
            if (context.User.Identity?.IsAuthenticated != true)
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "demo-" + Guid.NewGuid().ToString("N")), new Claim(ClaimTypes.Name, "Synthetic demo staff")], Scheme));
                await context.SignInAsync(Scheme, principal);
            }
            context.Response.Redirect("/workspaces");
        });
    }
}
