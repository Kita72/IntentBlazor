using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using IntentBlazor.Core;
using IntentBlazor.Runtime;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace IntentBlazor.AI;

public sealed record AiSetupPolicy(bool CanView = false, bool CanEdit = false, bool CanProbe = false, bool CanInspect = false,
    bool AllowAdvanced = false, int MaximumOutputTokens = 8000, int MaximumContextCharacters = 60000)
{
    public ImmutableHashSet<string> AllowedConnections { get; init; } = ImmutableHashSet.Create("default");
}
public interface IAiSetupAccess { ValueTask<AiSetupPolicy> GetPolicyAsync(CancellationToken cancellationToken); }
public sealed record NamedModelConnection(string Id, ModelProfile Profile, Func<IServiceProvider, IChatClient> Factory);
public sealed record AiConnectionView(string Id, string Provider, string Model, string? Endpoint,
    bool Temperature, bool OutputLimit, bool AdvancedSampling, bool ReasoningEffort);
public sealed record AiSetupSnapshot(long Revision, string? SelectedConnection, ImmutableArray<AiConnectionView> Connections,
    ModelOptions Effective, AiSetupPolicy Policy, string Notice);
public sealed record ModelProbeResult(bool Connected, bool ValidPresentation, bool ToolUseVerified, string Message, TimeSpan Elapsed);
internal sealed record EffectiveModelConnection(IChatClient Client, ModelProfile Profile, ModelOptions Options);

public sealed class ModelSetupState(IOptions<ModelOptions> defaults, IEnumerable<ModelProfile> profiles, IEnumerable<IChatClient> clients,
    IEnumerable<NamedModelConnection> connections, IAiSetupAccess access, IServiceProvider services, AiSession session) : IDisposable
{
    private readonly ModelProfile? _defaultProfile = profiles.SingleOrDefault();
    private readonly IChatClient? _defaultClient = clients.SingleOrDefault();
    private readonly ImmutableDictionary<string, NamedModelConnection> _connections = connections.ToImmutableDictionary(item => item.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, IChatClient> _created = new(StringComparer.Ordinal);
    private ModelOptions _options = defaults.Value.Copy();
    private string _selected = "default";
    private long _revision;
    private int _busy;
    public ModelProbeResult? LastProbe { get; private set; }

    public async ValueTask<AiSetupSnapshot> ReadAsync(CancellationToken token = default)
    {
        AiSetupPolicy policy = await access.GetPolicyAsync(token);
        var available = Available(policy);
        return new(_revision, policy.CanView ? _selected : null, policy.CanView ? available.Select(item => View(item.Key, item.Value)).ToImmutableArray() : [],
            policy.CanView ? _options.Copy() : new(), policy, available.Count == 0 ? "No host-approved model connection is configured." :
                "Connection endpoints and credentials are controlled by the host. Adapter capabilities do not certify a specific model.");
    }
    public async ValueTask<OperationResult> ApplyAsync(string connectionId, ModelOptions proposed, long expectedRevision, CancellationToken token = default)
    {
        if (session.IsBusy || Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return OperationResult.Fail("setup-busy", "Finish the active request before changing AI settings.");
        try
        {
            AiSetupPolicy policy = await access.GetPolicyAsync(token);
            if (!policy.CanView || !policy.CanEdit || expectedRevision != _revision || !Available(policy).TryGetValue(connectionId, out ModelProfile? profile))
                return OperationResult.Fail("setup-denied", "The current policy or settings revision does not permit this change.");
            ModelOptions next = proposed.Copy(); next.Validate();
            if (next.MaximumOutputTokens > policy.MaximumOutputTokens || next.MaximumContextCharacters > policy.MaximumContextCharacters ||
                next.Timeout > defaults.Value.Timeout || next.MaximumModelCalls > defaults.Value.MaximumModelCalls || next.MaximumToolCalls > defaults.Value.MaximumToolCalls ||
                next.MaximumCorrections > defaults.Value.MaximumCorrections || next.MaximumReportedTokens > defaults.Value.MaximumReportedTokens)
                return OperationResult.Fail("setup-budget", "The proposed settings exceed the host's generation budget.");
            if (!policy.AllowAdvanced && (next.TopP != defaults.Value.TopP || next.FrequencyPenalty != defaults.Value.FrequencyPenalty ||
                next.PresencePenalty != defaults.Value.PresencePenalty || next.Seed != defaults.Value.Seed || next.ReasoningEffort != defaults.Value.ReasoningEffort))
                return OperationResult.Fail("setup-denied", "Advanced settings are controlled by the administrator.");
            if (Unsupported(profile, next) is { } unsupported) return OperationResult.Fail("setup-unsupported", unsupported);
            AiSetupPolicy current = await access.GetPolicyAsync(token);
            if (session.IsBusy || !current.CanView || !current.CanEdit || !Available(current).ContainsKey(connectionId) ||
                next.MaximumOutputTokens > current.MaximumOutputTokens || next.MaximumContextCharacters > current.MaximumContextCharacters ||
                (!current.AllowAdvanced && policy.AllowAdvanced)) return OperationResult.Fail("setup-denied", "Permission changed while applying settings.");
            _selected = connectionId; _options = next; _revision++; LastProbe = null;
            return OperationResult.Ok;
        }
        catch (OperationCanceledException) { return OperationResult.Fail("cancelled", "The settings change was cancelled."); }
        catch (ArgumentException) { return OperationResult.Fail("setup-invalid", "The proposed settings are outside the supported ranges."); }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }
    public async ValueTask<ModelProbeResult> ProbeAsync(CancellationToken token = default)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        if (session.IsBusy || Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return new(false, false, false, "A request is already running.", elapsed.Elapsed);
        try
        {
            AiSetupPolicy policy = await access.GetPolicyAsync(token);
            if (!policy.CanView || !policy.CanProbe) return new(false, false, false, "The host policy does not permit a model probe.", elapsed.Elapsed);
            EffectiveModelConnection connection = await CaptureAsync(token);
            AiSetupPolicy sending = await access.GetPolicyAsync(token);
            if (!sending.CanView || !sending.CanProbe || !Available(sending).ContainsKey(_selected) || session.IsBusy)
                return new(false, false, false, "Probe access changed before submission.", elapsed.Elapsed);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            ChatOptions options = CreateChatOptions(connection.Profile, connection.Options);
            options.MaxOutputTokens = connection.Profile.SupportsOutputTokenLimit ? Math.Min(512, connection.Options.MaximumOutputTokens) : null;
            options.ResponseFormat = ChatResponseFormat.Json;
            ChatResponse response = await connection.Client.GetResponseAsync([
                new(ChatRole.System, "Return exactly a JSON object with documentJson containing a valid IntentBlazor schema 0.1 document with a single text node and no datasets. No tools or application data are available."),
                new(ChatRole.User, "Perform the presentation capability probe.")], options, timeout.Token);
            bool valid = false;
            if (response.Text.Length <= OutputJson.MaximumBytes)
            {
                try
                {
                    using JsonDocument envelope = JsonDocument.Parse(response.Text);
                    if (envelope.RootElement.TryGetProperty("documentJson", out JsonElement value) && value.ValueKind == JsonValueKind.String)
                        valid = new DocumentValidator().Validate(new() { Document = OutputJson.ReadDocument(value.GetString()!) }).Success;
                }
                catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException) { }
            }
            AiSetupPolicy completed = await access.GetPolicyAsync(token);
            if (!completed.CanView || !completed.CanProbe || !Available(completed).ContainsKey(_selected))
                return new(false, false, false, "Probe access changed before completion.", elapsed.Elapsed);
            return LastProbe = new(true, valid, false, valid ? "Connected; a valid presentation was returned. Tool use was not tested." :
                "Connected; the presentation probe did not validate. Tool use was not tested.", elapsed.Elapsed);
        }
        catch (OperationCanceledException) { return LastProbe = new(false, false, false, "The probe was cancelled or timed out.", elapsed.Elapsed); }
        catch (Exception error) when (error is not OutOfMemoryException) { return LastProbe = new(false, false, false, "The connection probe failed. Check the trusted host configuration.", elapsed.Elapsed); }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }
    internal async ValueTask<EffectiveModelConnection> CaptureAsync(CancellationToken token)
    {
        AiSetupPolicy policy = await access.GetPolicyAsync(token);
        if (!Available(policy).TryGetValue(_selected, out ModelProfile? profile)) throw new InputRejectedException("The selected model connection is no longer permitted.");
        if (_options.MaximumOutputTokens > policy.MaximumOutputTokens || _options.MaximumContextCharacters > policy.MaximumContextCharacters ||
            (!policy.AllowAdvanced && (_options.TopP != defaults.Value.TopP || _options.FrequencyPenalty != defaults.Value.FrequencyPenalty ||
                _options.PresencePenalty != defaults.Value.PresencePenalty || _options.Seed != defaults.Value.Seed || _options.ReasoningEffort != defaults.Value.ReasoningEffort)))
            throw new InputRejectedException("The current host policy no longer permits the applied generation settings.");
        if (Unsupported(profile, _options) is { } unsupported) throw new InputRejectedException(unsupported);
        IChatClient client;
        if (_selected == "default") client = _defaultClient ?? throw new InputRejectedException("No model connection is configured.");
        else if (!_created.TryGetValue(_selected, out client!)) _created[_selected] = client = _connections[_selected].Factory(services);
        return new(client, profile, _options.Copy());
    }
    private Dictionary<string, ModelProfile> Available(AiSetupPolicy policy)
    {
        var result = _connections.Where(pair => policy.AllowedConnections.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value.Profile);
        if (_defaultProfile is not null && policy.AllowedConnections.Contains("default")) result.Add("default", _defaultProfile);
        return result;
    }
    private static AiConnectionView View(string id, ModelProfile profile) => new(id, profile.Provider, profile.Model,
        profile.Endpoint is null ? null : profile.Endpoint.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped),
        profile.SupportsTemperature, profile.SupportsOutputTokenLimit, profile.SupportsAdvancedSampling, profile.SupportsReasoningEffort);
    internal static string? Unsupported(ModelProfile profile, ModelOptions options)
    {
        if (!profile.SupportsTemperature && options.Temperature is not null) return "This connection does not support temperature.";
        if (!profile.SupportsAdvancedSampling && (options.TopP is not null || options.FrequencyPenalty is not null || options.PresencePenalty is not null || options.Seed is not null))
            return "This connection does not support advanced sampling options.";
        if (!profile.SupportsReasoningEffort && options.ReasoningEffort is not null) return "Reasoning effort is unavailable for this connection.";
        return null;
    }
    internal static ChatOptions CreateChatOptions(ModelProfile profile, ModelOptions options) => new()
    {
        ModelId = profile.Model,
        Temperature = options.Temperature,
        MaxOutputTokens = profile.SupportsOutputTokenLimit ? options.MaximumOutputTokens : null,
        TopP = options.TopP,
        FrequencyPenalty = options.FrequencyPenalty,
        PresencePenalty = options.PresencePenalty,
        Seed = options.Seed,
        Reasoning = options.ReasoningEffort is null ? null : new()
        {
            Effort = options.ReasoningEffort switch
            { "low" => Microsoft.Extensions.AI.ReasoningEffort.Low, "medium" => Microsoft.Extensions.AI.ReasoningEffort.Medium, _ => Microsoft.Extensions.AI.ReasoningEffort.High }
        }
    };
    public void Dispose() { foreach (IChatClient client in _created.Values) client.Dispose(); _created.Clear(); }
}

public static class ModelSetupRegistration
{
    public static IServiceCollection AddIntentAiSetup<TAccess>(this IServiceCollection services) where TAccess : class, IAiSetupAccess
    {
        services.AddIntentBlazor(); services.AddOptions<ModelOptions>();
        services.TryAddScoped<TAccess>(); services.AddScoped<IAiSetupAccess>(sp => sp.GetRequiredService<TAccess>());
        services.TryAddScoped<ModelSetupState>(); return services;
    }
    public static IServiceCollection AddIntentModelPreset(this IServiceCollection services, NamedModelConnection connection)
    {
        if (connection.Id == "default" || connection.Id.Length is < 1 or > 64 || !connection.Id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ||
            services.Any(item => item.ImplementationInstance is NamedModelConnection prior && prior.Id == connection.Id)) throw new ArgumentException("Use a unique bounded connection preset ID.");
        services.AddSingleton(connection); return services;
    }
}
