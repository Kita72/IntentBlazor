using System.Text.Json;
using Microsoft.Extensions.Http;

namespace IntentBlazor.Verification;

// Record request shape only, never credentials, prompts, model text or reasoning.
public sealed class EvaluationDiagnostics : IHttpMessageHandlerBuilderFilter
{
    public List<string> Observations { get; } = [];
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder); builder.AdditionalHandlers.Add(new ShapeHandler(Observations));
    };
    public static string Describe(string body)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        int tools = root.TryGetProperty("tools", out var available) ? available.GetArrayLength() : 0;
        string format = root.TryGetProperty("response_format", out var response) && response.TryGetProperty("type", out var type) ? type.GetString() ?? "unset" : "unset";
        return $"tools={tools}; response_format={format}";
    }
    private sealed class ShapeHandler(List<string> observations) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.Content is { } content) observations.Add(Describe(await content.ReadAsStringAsync(token)));
            return await base.SendAsync(request, token);
        }
    }
}
