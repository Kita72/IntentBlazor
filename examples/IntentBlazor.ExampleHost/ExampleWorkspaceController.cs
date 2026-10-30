using IntentBlazor.AI;
using IntentBlazor.Core;
using IntentBlazor.Runtime;

namespace IntentBlazor.ExampleHost;

public interface IExampleScenario : IInputProcessor, IOutputDataSource, IWorkspaceAccess
{ string InitialRequest { get; } }
public sealed class ExampleWorkspaceController(AiSession session, IExampleScenario scenario)
{
    public string? Message { get; private set; }
    public async Task StartAsync()
    {
        if (session.WorkspaceKey is not null) return;
        OperationResult connected = await session.ConnectAsync("main");
        if (!connected.Success) { Set(connected); return; }
        if (session.Document is null)
        {
            try { Set(session.Open(await scenario.ProcessAsync(scenario.InitialRequest, session.Snapshot, default))); }
            catch (Exception error) when (error is InputRejectedException or HttpRequestException or IOException or OperationCanceledException)
            { Message = "Example content is unavailable. Retry through AiInput or check the host's configured source."; }
        }
    }
    public async Task SaveAsync() { await session.FlushAsync(); Set(await session.SaveAsync(), "Workspace checkpoint saved; business actions are separate."); }
    public async Task ReconnectAsync()
    {
        await session.FlushAsync(); OperationResult saved = await session.SaveAsync();
        if (!saved.Success) { Set(saved); return; }
        Set(await session.RestoreAsync(session.WorkspaceKey ?? "main", discardLocal: true), "Reconnected the saved workspace, including exact drafts.");
    }
    public async Task OpenAsync(string key) => Set(await session.RestoreAsync(key), "Saved workspace opened.");
    public async Task ForkAsync(string key) => Set(await session.ForkAsync(key), "Created a private workspace copy. Use its key to reopen it.");
    private void Set(OperationResult result, string? success = null) => Message = result.Success ? success : result.Errors.FirstOrDefault()?.Message;
}
