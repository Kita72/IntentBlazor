using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using IntentBlazor.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace IntentBlazor.Tests;

internal sealed class ScriptChatClient(params ChatResponse[] responses) : IChatClient
{
    public List<(ChatMessage[] Messages, ChatOptions? Options)> Calls { get; } = [];
    public Func<CancellationToken, Task>? BeforeResponse { get; set; }
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls.Add((messages.ToArray(), options));
        if (BeforeResponse is not null) await BeforeResponse(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return responses[Math.Min(Calls.Count - 1, responses.Length - 1)];
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (ChatMessage message in response.Messages)
        {
            yield return new() { Role = message.Role, Contents = message.Contents, FinishReason = ChatFinishReason.Stop };
        }
        if (response.Usage is not null) yield return new() { Contents = [new UsageContent(response.Usage)] };
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}

internal static class ModelTestSupport
{
    public static string Envelope(string fixture = "news") => JsonSerializer.Serialize(new
    { documentJson = OutputJson.SerializeDocument(TestContent.Catalog.Get(fixture).Document) });
    public static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));
    public static ChatResponse Tool(string name = "get_gaming_news", IDictionary<string, object?>? arguments = null) =>
        new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", name, arguments ?? new Dictionary<string, object?>())]));

    public static ServiceProvider Provider(ScriptChatClient client, Action<ModelOptions>? configure = null, bool tools = true, bool nativeTools = true)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(TestContent.Catalog);
        IntentModelBuilder builder = services.AddIntentBlazorAI(configure)
            .UseChatClient(_ => client, new("Scripted test double", "test-only", null, nativeTools));
        if (tools) builder.AddTools<NewsDataService>();
        return services.BuildServiceProvider(validateScopes: true);
    }
}

internal sealed class RecordingHandler : HttpMessageHandler
{
    public List<(Uri? Uri, string? Authorization, string Body)> Requests { get; } = [];
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = await request.Content!.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.RequestUri, request.Headers.Authorization?.ToString(), body));
        if (Status != HttpStatusCode.OK) return new(Status) { Content = new StringContent("{\"error\":{\"message\":\"private upstream detail\",\"type\":\"invalid_request_error\"}}") };
        string text = JsonSerializer.Serialize(new { documentJson = "{\"id\":\"answer\",\"rootId\":\"text\",\"nodes\":[{\"id\":\"text\",\"kind\":\"text\",\"value\":{\"literal\":\"Connected test double\"}}]}" });
        string chunk = JsonSerializer.Serialize(new
        {
            id = "test-completion",
            created = 0,
            model = "test-model",
            @object = "chat.completion.chunk",
            choices = new[] { new { index = 0, delta = new { role = "assistant", content = text }, finish_reason = "stop" } }
        });
        return new(HttpStatusCode.OK) { Content = new StringContent($"data: {chunk}\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream") };
    }
}

internal sealed class TestHandlerFilter(RecordingHandler handler) : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    { next(builder); builder.PrimaryHandler = handler; };
}

internal sealed class ScriptCodexRunner(params string[] lines) : ICodexProcessRunner
{
    public CodexProcessRequest? Request { get; private set; }
    public async IAsyncEnumerable<string> RunAsync(CodexProcessRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Request = request;
        foreach (string line in lines) { cancellationToken.ThrowIfCancellationRequested(); yield return line; }
        await Task.CompletedTask;
    }
}
