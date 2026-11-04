using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using IntentBlazor.AI;
using IntentBlazor.Core;
using IntentBlazor.Runtime;
using Microsoft.Extensions.AI;

namespace IntentBlazor.BrowserHost;

public sealed class BrowserFixtureState
{
    private TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string Instance { get; } = Guid.NewGuid().ToString("N");
    public bool Waiting { get; private set; }
    public float? Temperature { get; private set; }
    public event Action? Changed;
    public void Observe(float? temperature) { Temperature = temperature; Changed?.Invoke(); }
    public async Task HoldAsync(CancellationToken token)
    {
        _release = new(TaskCreationOptions.RunContinuationsAsynchronously); Waiting = true; Changed?.Invoke();
        try { await _release.Task.WaitAsync(token); }
        finally { Waiting = false; Changed?.Invoke(); }
    }
    public void Release() => _release.TrySetResult();
}
public sealed class OfflineChatClient(BrowserFixtureState fixture) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        string command = messages.Last(message => message.Role == ChatRole.User).Text.Trim().ToLowerInvariant();
        fixture.Observe(options?.Temperature);
        if (command is "cancel" or "timeout") await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        if (command == "hold") await fixture.HoldAsync(cancellationToken);
        if (command == "unavailable") throw new HttpRequestException("Synthetic private upstream failure detail must not reach the page.");
        string text = command switch
        {
            "partial" => "{\"documentJson\":\"unfinished",
            "invalid" => JsonSerializer.Serialize(new { documentJson = "{\"id\":\"bad\",\"rootId\":\"absent\",\"nodes\":[]}" }),
            _ => JsonSerializer.Serialize(new { documentJson = OutputJson.SerializeDocument(FixtureDocuments.Create("Accepted offline fixture").Document) })
        };
        return new(new ChatMessage(ChatRole.Assistant, text))
        { Usage = command == "budget" ? new() { InputTokenCount = 2000, OutputTokenCount = 100 } : null };
    }
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new() { Role = ChatRole.Assistant, Contents = response.Messages[0].Contents, FinishReason = ChatFinishReason.Stop };
        if (response.Usage is not null) yield return new() { Contents = [new UsageContent(response.Usage)] };
    }
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}
public sealed class FixtureIdentity : IWorkspaceIdentity
{
    public ValueTask<string?> GetOwnerIdAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>("offline-browser-owner");
}
public sealed class FixtureAccess : IWorkspaceAccess
{
    public ValueTask<WorkspaceAccess> ReauthorizeAsync(string owner, ImmutableArray<string> ids, ImmutableArray<ArtifactReference> artifacts, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new WorkspaceAccess(owner == "offline-browser-owner" && ids.All(id => id == "jobs") && artifacts.IsEmpty, FixtureDocuments.Create().Data));
}
public sealed class FixtureData : IOutputDataSource
{
    public ValueTask<ImmutableArray<OutputDataSet>> LoadAsync(WorkspaceSnapshot workspace, CancellationToken cancellationToken) => ValueTask.FromResult(FixtureDocuments.Create().Data);
}
public sealed class FixtureSetupAccess : IAiSetupAccess
{
    public ValueTask<AiSetupPolicy> GetPolicyAsync(CancellationToken cancellationToken) => ValueTask.FromResult(new AiSetupPolicy(true, true, false, true, true));
}
