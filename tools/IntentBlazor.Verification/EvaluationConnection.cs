using IntentBlazor.AI;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Verification;

// Credentials are deliberately absent from report metadata and never read before explicit opt-in.
public sealed class EvaluationConnection
{
    public string Provider { get; }
    public string Model { get; }
    public string ModelRevision { get; }
    public string ServerVersion { get; }
    public Uri? Endpoint { get; }
    public bool NativeTools => Provider != "Codex CLI (experimental)";
    private readonly Action<IntentModelBuilder> _connect;

    private EvaluationConnection(string provider, string model, string revision, string serverVersion, Uri? endpoint,
        Action<IntentModelBuilder> connect)
    {
        if (new[] { model, revision, serverVersion }.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(char.IsControl)))
            throw new ArgumentException("Record bounded model, revision and service/client version identifiers.");
        Provider = provider; Model = model; ModelRevision = revision; ServerVersion = serverVersion; Endpoint = endpoint; _connect = connect;
    }

    public static EvaluationConnection Local(string provider, string model, string revision, string serverVersion, Uri endpoint)
    {
        ModelEvaluationRunner.ValidateLocalConnection(provider, endpoint);
        return new(provider, model, revision, serverVersion, endpoint, builder =>
        {
            if (provider == "Ollama") builder.UseOllama(model, endpoint); else builder.UseLMStudio(model, endpoint);
        });
    }

    public static EvaluationConnection OpenAI(string model, string revision, string apiVersion,
        Func<IServiceProvider, string> credential, bool liveApproved = false)
    {
        RequirePaidOptIn(liveApproved);
        ArgumentNullException.ThrowIfNull(credential);
        return new("OpenAI", model, revision, apiVersion, new("https://api.openai.com/v1/"),
            builder => builder.UseOpenAI(model, credential));
    }

    public static EvaluationConnection Codex(string model, string revision, string cliVersion, string executable, bool liveApproved = false)
    {
        RequirePaidOptIn(liveApproved);
        if (!Path.IsPathFullyQualified(executable) || (OperatingSystem.IsWindows() && !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("The evaluator requires an explicit native Codex executable path, not a shell shim.");
        return new("Codex CLI (experimental)", model, revision, cliVersion, null,
            builder => builder.UseCodex(new() { Model = model, ExecutablePath = executable }));
    }

    public IntentModelBuilder Register(IServiceCollection services, Action<ModelOptions>? configure = null)
    {
        IntentModelBuilder builder = services.AddIntentBlazorAI(configure); _connect(builder); return builder;
    }

    private static void RequirePaidOptIn(bool approved)
    {
        if (!approved) throw new ArgumentException("Paid inference is deferred. A future approved run must explicitly use --allow-paid.");
    }
}
