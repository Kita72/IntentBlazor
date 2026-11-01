using System.Collections.Immutable;
using IntentBlazor.AI;
using IntentBlazor.AssetWorkbench;
using IntentBlazor.Content;
using IntentBlazor.Core;
using IntentBlazor.ExampleHost;
using IntentBlazor.GamingNews;
using IntentBlazor.Runtime;
using IntentBlazor.StaffBusiness;
using IntentBlazor.Storage.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;

namespace IntentBlazor.Verification;

public sealed class EvaluationActor : IExampleActor, IWorkspaceIdentity
{
    public ExampleActor? Current { get; set; } = new("evaluation-alex", "Synthetic evaluator", ImmutableHashSet.Create("Dispatcher"));
    public ValueTask<ExampleActor?> ReadAsync(CancellationToken token = default)
    { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(Current); }
    public ValueTask<string?> GetOwnerIdAsync(CancellationToken token)
    { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(Current?.Id); }
}

public sealed class ApplicationEvaluationData(IExampleScenario scenario) : IOutputDataSource
{
    public int Calls { get; private set; }
    public WorkspaceSnapshot Workspace { get; set; } = ExampleData.EmptyWorkspace;
    [AiDataTool("application_records", "Read an authorized synthetic example-app dataset by its exact ID from the workspace. Read only, no application writes.")]
    public async Task<OutputDataSet> ReadAsync(string dataSetId, CancellationToken token)
    {
        var sets = await scenario.LoadAsync(Workspace, token);
        OutputDataSet data = sets.SingleOrDefault(set => set.Id == dataSetId) ?? throw new InputRejectedException("The dataset is unavailable in this example.");
        Calls++; return data;
    }
    public async ValueTask<ImmutableArray<OutputDataSet>> LoadAsync(WorkspaceSnapshot workspace, CancellationToken token)
    {
        var sets = await scenario.LoadAsync(workspace, token); Calls++; return sets;
    }
}

public sealed class ApplicationEvaluationHost : IAsyncDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "IntentBlazor-Evaluation-" + Guid.NewGuid().ToString("N"));
    public ServiceProvider Services { get; private set; } = null!;
    public EvaluationDiagnostics Diagnostics { get; } = new();

    public static async Task<ApplicationEvaluationHost> CreateAsync(string application, EvaluationConnection connection,
        ModelOptions options, bool preload, Action<IServiceCollection>? configureTestTransport = null, CancellationToken token = default)
    {
        ApplicationEvaluationHost host = new();
        try
        {
            Directory.CreateDirectory(host.DirectoryPath);
            ServiceCollection services = new(); services.AddLogging();
            IntentModelBuilder builder = connection.Register(services, target => ModelEvaluationRunner.Copy(options, target));
            services.AddSingleton<IHttpMessageHandlerBuilderFilter>(host.Diagnostics);
            services.AddSqliteWorkspaces(Path.Combine(host.DirectoryPath, "workspaces.sqlite"));
            services.AddScoped<EvaluationActor>();
            services.AddScoped<IExampleActor>(sp => sp.GetRequiredService<EvaluationActor>());
            services.AddScoped<IWorkspaceIdentity>(sp => sp.GetRequiredService<EvaluationActor>());
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:Provider"] = connection.Provider }).Build();
            switch (application)
            {
                case "news": services.AddGamingNews(config, host.DirectoryPath); break;
                case "staff": services.AddStaffBusiness(config, host.DirectoryPath); break;
                case "assets": services.AddAssetWorkbench(config, host.DirectoryPath); break;
                default: throw new ArgumentException("Unknown evaluation application.");
            }
            services.RemoveAll<IOutputDataSource>();
            services.AddScoped<ApplicationEvaluationData>();
            if (preload || !connection.NativeTools) services.AddScoped<IOutputDataSource>(sp => sp.GetRequiredService<ApplicationEvaluationData>());
            else builder.AddTools<ApplicationEvaluationData>();
            configureTestTransport?.Invoke(services);
            host.Services = services.BuildServiceProvider(validateScopes: true);
            if (application == "staff") await EfStaffRepository.InitializeAsync(host.Services.GetRequiredService<IDbContextFactory<BusinessDbContext>>(), token);
            return host;
        }
        catch { await host.DisposeAsync(); throw; }
    }

    public static async Task<AiSession> StartAsync(IServiceProvider services, string application, CancellationToken token = default)
    {
        services.GetRequiredService<WorkspaceServices>().AutoSave = false;
        AiSession session = services.GetRequiredService<AiSession>();
        if (!(await session.ConnectAsync("evaluation", token)).Success) throw new InvalidOperationException("The synthetic workspace did not connect.");
        if (application == "assets" && !session.SetArtifacts(await AssetSamples.CreateAsync(services.GetRequiredService<IWorkspaceContent>(), token)).Success)
            throw new InvalidOperationException("The synthetic image import failed.");
        var scenario = services.GetRequiredService<IExampleScenario>();
        if (!session.Open(await scenario.ProcessAsync(scenario.InitialRequest, session.Snapshot, token)).Success)
            throw new InvalidOperationException("The authored application seed is invalid.");
        return session;
    }

    public async ValueTask DisposeAsync()
    {
        if (Services is not null) await Services.DisposeAsync();
        // Only this uniquely created evaluation directory is owned by the runner.
        string target = Path.GetFullPath(DirectoryPath);
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!target.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(target).StartsWith("IntentBlazor-Evaluation-", StringComparison.Ordinal))
            throw new InvalidOperationException("The evaluation cleanup path escaped its temporary root.");
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
    }
}
