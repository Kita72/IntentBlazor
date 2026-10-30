using System.Collections.Immutable;
using IntentBlazor.AssetWorkbench;
using IntentBlazor.Blazor;
using IntentBlazor.ExampleHost;
using IntentBlazor.GamingNews;
using IntentBlazor.StaffBusiness;
using IntentBlazor.Storage.Sqlite;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace IntentBlazor.Tests;

internal sealed class Phase5Actor : IExampleActor, IWorkspaceIdentity
{
    public ExampleActor? Current { get; set; }
    public ValueTask<ExampleActor?> ReadAsync(CancellationToken token = default) { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(Current); }
    public ValueTask<string?> GetOwnerIdAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(Current?.Id); }
}
internal sealed class MutableNewsSource : INewsSource
{
    public Exception? Failure { get; set; }
    public ImmutableArray<GameArticle> Articles { get; set; } = [];
    public ValueTask<ImmutableArray<GameArticle>> ReadAsync(CancellationToken token = default) { token.ThrowIfCancellationRequested(); if (Failure is { } error) throw error; return ValueTask.FromResult(Articles); }
}
internal sealed class Phase5Harness : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "IntentBlazor-phase5-" + Guid.NewGuid().ToString("N"));
    public ServiceProvider Provider { get; }
    public MutableNewsSource Source { get; } = new();
    private Phase5Harness(string kind, ImmutableArray<GameArticle> articles)
    {
        Directory.CreateDirectory(DirectoryPath); Source.Articles = articles;
        ServiceCollection services = new(); services.AddLogging(); services.AddIntentBlazor();
        services.AddSqliteWorkspaces(Path.Combine(DirectoryPath, "workspaces.sqlite"));
        services.AddScoped<Phase5Actor>(); services.AddScoped<IExampleActor>(sp => sp.GetRequiredService<Phase5Actor>());
        services.AddScoped<IWorkspaceIdentity>(sp => sp.GetRequiredService<Phase5Actor>());
        services.AddScoped<ExampleWorkspaceController>(); services.AddSingleton<IJSRuntime, ContentIntegrationTests.NoBrowserJs>();
        IConfiguration config = new ConfigurationBuilder().Build();
        switch (kind)
        {
            case "news": services.AddGamingNews(config, DirectoryPath); services.AddSingleton<INewsSource>(Source); break;
            case "staff": services.AddStaffBusiness(config, DirectoryPath); break;
            case "assets": services.AddAssetWorkbench(config, DirectoryPath); break;
            default: throw new ArgumentException("Unknown scenario.");
        }
        Provider = services.BuildServiceProvider(validateScopes: true);
    }
    public static async Task<Phase5Harness> CreateAsync(string kind)
    {
        Phase5Harness harness = new(kind, await new FixtureNewsSource().ReadAsync());
        if (kind == "staff") await EfStaffRepository.InitializeAsync(harness.Provider.GetRequiredService<IDbContextFactory<BusinessDbContext>>());
        return harness;
    }
    public Phase5Scope Scope(string owner = "alex", string role = "Dispatcher") => new(Provider.CreateScope(), owner, role);
    public void Dispose() { Provider.Dispose(); Directory.Delete(DirectoryPath, recursive: true); }
}
internal sealed class Phase5Scope : IDisposable
{
    private readonly IServiceScope _scope;
    public IServiceProvider Services => _scope.ServiceProvider;
    public Phase5Actor Actor => Services.GetRequiredService<Phase5Actor>();
    public AiSession Session => Services.GetRequiredService<AiSession>();
    public Phase5Scope(IServiceScope scope, string owner, string role)
    {
        _scope = scope; Actor.Current = new(owner, owner, ImmutableHashSet.Create(role));
        Services.GetRequiredService<WorkspaceServices>().AutoSave = false;
    }
    public async Task StartAsync() { await Services.GetRequiredService<ExampleWorkspaceController>().StartAsync(); Assert.NotNull(Session.Document); }
    public void Select(string node, string key) => Assert.True(Session.Dispatch(new(node, UiEventKind.Select, Session.Revision) { Value = JsonValues.From(key) }).Success);
    public void Change(string node, string value) => Assert.True(Session.Change(node, value, Session.Revision).Success);
    public async Task<ActionResult> ActivateAsync(string node) => await Session.ExecuteActionAsync(Assert.IsType<ActionRequest>(Session.PrepareAction(node, Session.Revision)));
    public void Dispose() => _scope.Dispose();
    public async Task<(string Input, string Output)> RenderAsync()
    {
        await using HtmlRenderer renderer = new(Services, Services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            ((await renderer.RenderComponentAsync<AiInput>()).ToHtmlString(), (await renderer.RenderComponentAsync<AiOutput>()).ToHtmlString()));
    }
}
