using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json.Nodes;
using IntentBlazor.Runtime;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenAI;

namespace IntentBlazor.AI;

public static class ModelRegistration
{
    public static IntentModelBuilder AddIntentBlazorAI(this IServiceCollection services, Action<ModelOptions>? configure = null)
    {
        services.AddIntentBlazor();
        services.AddOptions<ModelOptions>();
        if (configure is not null) services.Configure(configure);
        services.RemoveAll<IInputProcessor>();
        services.AddScoped<ModelInputProcessor>();
        services.AddScoped<IInputProcessor>(sp => sp.GetRequiredService<ModelInputProcessor>());
        return new(services);
    }
}

public sealed class IntentModelBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    public IntentModelBuilder UseOllama(string model, Uri? endpoint = null) =>
        UseCompatible("Ollama", model, endpoint ?? new("http://localhost:11434/v1/"), _ => "ollama");

    public IntentModelBuilder UseLMStudio(string model, Uri? endpoint = null, Func<IServiceProvider, string>? apiKey = null) =>
        UseCompatible("LM Studio", model, endpoint ?? new("http://localhost:1234/v1/"), apiKey ?? (_ => "lm-studio"));

    public IntentModelBuilder UseOpenAI(string model, Func<IServiceProvider, string> apiKey, bool supportsReasoningEffort = false) =>
        UseCompatible("OpenAI", model, new("https://api.openai.com/v1/"), apiKey, supportsReasoningEffort);

    public IntentModelBuilder AddOllamaPreset(string id, string model, Uri? endpoint = null) =>
        UseCompatible("Ollama", model, endpoint ?? new("http://localhost:11434/v1/"), _ => "ollama", presetId: id);
    public IntentModelBuilder AddLMStudioPreset(string id, string model, Uri? endpoint = null, Func<IServiceProvider, string>? apiKey = null) =>
        UseCompatible("LM Studio", model, endpoint ?? new("http://localhost:1234/v1/"), apiKey ?? (_ => "lm-studio"), presetId: id);
    public IntentModelBuilder AddOpenAIPreset(string id, string model, Func<IServiceProvider, string> apiKey, bool supportsReasoningEffort = false) =>
        UseCompatible("OpenAI", model, new("https://api.openai.com/v1/"), apiKey, supportsReasoningEffort, id);

    public IntentModelBuilder UseChatClient(Func<IServiceProvider, IChatClient> factory, ModelProfile profile)
    {
        CheckModel(profile.Model);
        if (Services.Any(descriptor => descriptor.ServiceType == typeof(ModelProfile)))
            throw new InvalidOperationException("Select one model connection per service collection.");
        Services.AddSingleton(profile);
        Services.AddChatClient(factory, ServiceLifetime.Scoped);
        return this;
    }

    public IntentModelBuilder UseCodex(CodexOptions options)
    {
        options.Validate();
        Services.AddSingleton(options);
        Services.TryAddSingleton<ICodexProcessRunner, CodexProcessRunner>();
        return UseChatClient(sp => new CodexChatClient(options, sp.GetRequiredService<ICodexProcessRunner>()),
            new("Codex CLI (experimental)", options.Model, null, NativeTools: false,
                SupportsOutputTokenLimit: false, SupportsTemperature: false, IncrementalText: false, SupportsAdvancedSampling: false));
    }

    public IntentModelBuilder AddDataSource<T>() where T : class, IOutputDataSource
    {
        Services.AddScoped<IOutputDataSource, T>();
        return this;
    }

    public IntentModelBuilder AddTools<T>() where T : class
    {
        foreach (var method in typeof(T).GetMethods())
            if (method.GetCustomAttributes(typeof(AiDataToolAttribute), inherit: true).FirstOrDefault() is AiDataToolAttribute attribute)
                AddTool<T>(method.Name, attribute.Name, attribute.Description);
        return this;
    }

    public IntentModelBuilder AddTool<T>(string method, string name, string description) where T : class
    {
        DataToolRegistration registration = DataToolRegistration.Create(typeof(T), method, name, description);
        if (Services.Any(d => d.ImplementationInstance is DataToolRegistration existing && existing.Name == name))
            throw new ArgumentException("Tool names must be unique.");
        Services.TryAddScoped<T>();
        Services.AddSingleton(registration);
        return this;
    }

    private IntentModelBuilder UseCompatible(string provider, string model, Uri endpoint, Func<IServiceProvider, string> key, bool supportsReasoningEffort = false, string? presetId = null)
    {
        CheckModel(model);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("http" or "https") ||
            (endpoint.Scheme == "http" && !endpoint.IsLoopback) || !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment) || !endpoint.AbsolutePath.EndsWith('/'))
            throw new ArgumentException("Use an HTTPS base URI, or loopback HTTP, ending in / without credentials or query strings.");
        string httpName = $"IntentBlazor-{Guid.NewGuid():N}";
        IHttpClientBuilder httpBuilder = Services.AddHttpClient(httpName, http => http.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        if (provider != "OpenAI") httpBuilder.AddHttpMessageHandler(() => new LocalTokenLimitHandler());
        Func<IServiceProvider, IChatClient> factory = sp => new DeferredChatClient(() =>
        {
            string credential = key(sp);
            if (string.IsNullOrWhiteSpace(credential)) throw new InvalidOperationException("The selected model credential is not configured.");
            var client = new OpenAIClient(new ApiKeyCredential(credential), new OpenAIClientOptions
            {
                Endpoint = endpoint,
                Transport = new HttpClientPipelineTransport(sp.GetRequiredService<IHttpClientFactory>().CreateClient(httpName)),
                RetryPolicy = new ClientRetryPolicy(0)
            });
            return client.GetChatClient(model).AsIChatClient();
        });
        ModelProfile profile = new(provider, model, endpoint, SupportsReasoningEffort: supportsReasoningEffort);
        if (presetId is null) return UseChatClient(factory, profile);
        Services.AddIntentModelPreset(new(presetId, profile, factory)); return this;
    }

    private static void CheckModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model) || model.Length > 200 || model.Any(char.IsControl))
            throw new ArgumentException("Specify the model identifier installed or enabled on the selected service.");
    }

    private sealed class DeferredChatClient(Func<IChatClient> factory) : IChatClient
    {
        private readonly Lazy<IChatClient> _client = new(factory);
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => _client.Value.GetResponseAsync(messages, options, cancellationToken);
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => _client.Value.GetStreamingResponseAsync(messages, options, cancellationToken);
        public object? GetService(Type serviceType, object? serviceKey = null) => _client.Value.GetService(serviceType, serviceKey);
        public void Dispose() { if (_client.IsValueCreated) _client.Value.Dispose(); }
    }

    // Local compatibility APIs document max_tokens rather than OpenAI's newer field.
    private sealed class LocalTokenLimitHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is { } original)
            {
                JsonObject body = JsonNode.Parse(await original.ReadAsStringAsync(cancellationToken))!.AsObject();
                if (body.TryGetPropertyValue("max_completion_tokens", out JsonNode? maximum))
                {
                    body.Remove("max_completion_tokens");
                    body["max_tokens"] = maximum;
                    request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
                    original.Dispose();
                }
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
