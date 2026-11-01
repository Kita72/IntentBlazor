using System.Text.Json;
using Microsoft.Extensions.Http;

namespace IntentBlazor.Verification;

public sealed record EvaluationRequestMeasurement(string Phase, int SerializedMessageCharacters, int SerializedToolCharacters, bool Stream);

// Record request shape only, never credentials, prompts, model text or reasoning.
public sealed class EvaluationDiagnostics : IHttpMessageHandlerBuilderFilter
{
    public List<string> Observations { get; } = [];
    public List<EvaluationRequestMeasurement> Requests { get; } = [];
    public string Phase { get; set; } = "initial";
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder); builder.AdditionalHandlers.Add(new ShapeHandler(this));
    };
    public static string Describe(string body)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        int tools = root.TryGetProperty("tools", out var available) ? available.GetArrayLength() : 0;
        string format = root.TryGetProperty("response_format", out var response) && response.TryGetProperty("type", out var type) ? type.GetString() ?? "unset" : "unset";
        return $"tools={tools}; response_format={format}";
    }
    public static EvaluationRequestMeasurement Measure(string body, string phase)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        return new(phase, root.TryGetProperty("messages", out var messages) ? messages.GetRawText().Length : 0,
            root.TryGetProperty("tools", out var tools) ? tools.GetRawText().Length : 0,
            root.TryGetProperty("stream", out var stream) && stream.ValueKind == JsonValueKind.True);
    }
    private sealed class ShapeHandler(EvaluationDiagnostics diagnostics) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.Content is { } content)
            {
                string body = await content.ReadAsStringAsync(token);
                diagnostics.Observations.Add(Describe(body));
                diagnostics.Requests.Add(Measure(body, diagnostics.Phase));
            }
            return await base.SendAsync(request, token);
        }
    }
}
