# Phase 2 model connections

Status: adapters implemented; deterministic tests pass. **Live API and model verification is deferred by the maintainer.** No provider/model profile is currently certified by this repository. The Phase 2 live evaluation gate remains open.

Reference `IntentBlazor.AI` alongside `IntentBlazor.Blazor`. Until publication, use project references or locally packed artifacts. Both target .NET 10. The consuming page still uses only:

~~~razor
@page "/"
@rendermode InteractiveServer
@using IntentBlazor.Blazor

<AiInput />
<AiOutput />
~~~

## Register a connection

Registration is server-side. Choose one connection per service collection. The app owns model identifiers, endpoints, credentials, and application data authorization. Specify an installed or enabled model ID; record its exact version or digest when evaluating it.

~~~csharp
using IntentBlazor.AI;

builder.Services.AddIntentBlazorAI()
    .UseOllama("your-installed-model-id")
    .AddTools<NewsService>();
~~~

Alternative connection registrations:

~~~csharp
// LM Studio: choose the model identifier exposed by the local server.
builder.Services.AddIntentBlazorAI()
    .UseLMStudio("your-loaded-model-id")
    .AddTools<NewsService>();

// OpenAI: the host resolves its credential from an approved secret/configuration source.
builder.Services.AddIntentBlazorAI()
    .UseOpenAI("your-enabled-model-id", sp =>
        sp.GetRequiredService<IConfiguration>()["Ai:ApiKey"]
        ?? throw new InvalidOperationException("Model credential is not configured."))
    .AddTools<NewsService>();
~~~

These are alternatives, not three registrations in one app. No key was created or configured during Phase 2. Keep credentials out of documents, browser fields, Git, and logs. Choose and authorize credentials separately before live verification.

Ollama defaults to `http://localhost:11434/v1/`; LM Studio defaults to `http://localhost:1234/v1/`. Both accept an endpoint override. LM Studio also accepts a host credential resolver for servers with authentication enabled. OpenAI uses `https://api.openai.com/v1/`. The adapters use the official OpenAI .NET client behind Microsoft.Extensions.AI and the providers' documented Chat Completions compatibility. Localhost refers to the app server. A deployed server cannot reach a visitor's local model through its own localhost.

Base URIs must end in `/`, use HTTPS or loopback HTTP, and contain no embedded credentials, query, or fragment. The local adapters translate the SDK's max_completion_tokens field to the providers' documented max_tokens field. Redirects and automatic SDK retries are disabled to keep credential destinations and request counts explicit. Transient HTTP failures return an error; a host can implement additional connection policy through `UseChatClient`.

The integration relies on documented interfaces, not a claim that every model supports them: [Microsoft IChatClient](https://learn.microsoft.com/en-us/dotnet/ai/ichatclient), [Ollama compatibility](https://docs.ollama.com/api/openai-compatibility), [LM Studio structured output](https://lmstudio.ai/docs/developer/openai-compat/structured-output), [LM Studio tool use](https://lmstudio.ai/docs/developer/openai-compat/tools), and [OpenAI structured output](https://developers.openai.com/api/docs/guides/structured-outputs).

## Expose a C# data service

An opt-in attribute is the easy path. This complete service supplies a synthetic, bounded dataset:

~~~csharp
using System.Collections.Immutable;
using System.ComponentModel;
using IntentBlazor.AI;
using IntentBlazor.Core;

public sealed class NewsService
{
    [AiDataTool("read_news", "Read the synthetic gaming news articles. Returns the articles dataset.")]
    public ValueTask<OutputDataSet> ReadAsync(
        [Description("Optional game name filter; an empty value returns all sample articles.")] string game,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fields = ImmutableDictionary<string, DataField>.Empty
            .Add("id", new() { Kind = StateKind.Text })
            .Add("title", new() { Kind = StateKind.Text });
        var row = ImmutableDictionary<string, System.Text.Json.JsonElement>.Empty
            .Add("id", JsonValues.From("sample-1"))
            .Add("title", JsonValues.From("Example RPG: synthetic update"));
        return ValueTask.FromResult(new OutputDataSet
        {
            Id = "articles", KeyField = "id", Fields = fields,
            Rows = string.IsNullOrEmpty(game) || "Example RPG".Contains(game, StringComparison.OrdinalIgnoreCase)
                ? [row] : []
        });
    }
}
~~~

Explicit registration uses the same metadata, schema generation, invocation, and validation:

~~~csharp
builder.Services.AddIntentBlazorAI()
    .UseOllama("your-installed-model-id")
    .AddTool<NewsService>(nameof(NewsService.ReadAsync), "read_news", "Read synthetic gaming articles.");
~~~

Supported data methods are public instance methods returning `OutputDataSet`, `Task<OutputDataSet>`, or `ValueTask<OutputDataSet>`. Parameters may be string, int, bool, or CancellationToken. Parameter/result schemas come from Microsoft.Extensions.AI; descriptions can use `DescriptionAttribute`. Extra/missing arguments and oversized argument payloads are rejected before invocation. Services are registered scoped if the app has not already registered them.

All tools in this phase are **declared read-only data capabilities**. The framework cannot prove a C# method has no side effects: developers must expose only reads here and enforce user authorization inside the service. Application write actions, idempotency, and business concurrency belong to Phase 3. Tool failures return sanitized errors to the model. Dataset IDs must be unique within a request; duplicate results are rejected.

For data that should be supplied every turn, implement `IOutputDataSource.LoadAsync(workspace, cancellationToken)` and register it with `AddDataSource<T>()`. It returns an immutable array of datasets authorized for the current user. Results are validated before entering context. Neither tool attributes nor generated UI can grant access to other records.

## Generation, streaming, and limits

`ModelInputProcessor` implements the existing input seam. HTTP models use Microsoft Agent Framework over Microsoft.Extensions.AI for function orchestration. The toolkit instructions are built from the supported node enum plus the schema's composition/binding rules. Current document, exact state, selection, and sorting are supplied as context; current dirty drafts block replacement before a model request.

The response envelope is `{ "documentJson": "...presentation JSON..." }`. This avoids requiring a strict-output provider to represent dynamic state dictionaries directly. The model supplies the presentation only. The host joins declared dataset IDs to actual data returned by its registered sources/tools, then runs the Phase 1 strict codec and semantic validator. The model cannot submit authoritative dataset rows. Unknown records, actions, fields, capabilities, and invalid graphs are rejected.

Streaming consumes model deltas and reports stage, model/data call counts, and received character counts through the existing session. Partial document JSON is never rendered. A complete validated document replaces the output atomically. Codex CLI supplies a completed final message rather than incremental document text.

~~~csharp
builder.Services.AddIntentBlazorAI(options =>
{
    options.Timeout = TimeSpan.FromSeconds(90);
    options.MaximumModelCalls = 6;
    options.MaximumToolCalls = 8;
    options.MaximumCorrections = 1;
    options.MaximumContextCharacters = 60000;
    options.MaximumOutputTokens = 8000;
    options.MaximumReportedTokens = 32000;
    options.Temperature = null;
    options.UseJsonSchema = true;
}).UseOllama("your-installed-model-id").AddTools<NewsService>();
~~~

Temperature is omitted by default because some reasoning models reject it. Set it only for a compatible profile. `UseJsonSchema = false` requests JSON mode while retaining the exact same local validation. This is an explicit fallback, not automatic capability detection or a silent downgrade.

The overall timeout includes data loading, model calls, and correction. Host data services and custom clients must honor cancellation. Model-call and data-call budgets cover the entire request, including correction. The input snapshot is revision checked before output installation. Failure, cancellation, timeout, or a stale response preserves the working view. Correction is bounded; authentication/network failures are not retried as malformed output.

`ModelInputProcessor.LastRun` reports provider/model, outcome, elapsed time, calls, corrections, character count, and provider-reported token counts. Unknown counts remain null. Context limits are measured in characters, not estimated tokens. Reported token limits can stop later work but cannot prevent charges already incurred, and cannot enforce a provider's unreported usage. There is no pricing calculator or hard monetary budget. Prompts, datasets, model text, keys, and hidden reasoning are not included in the report.

## Codex workstation adapter

Codex has a [documented noninteractive CLI](https://developers.openai.com/codex/noninteractive) with JSON events and schema-constrained final output. This optional adapter uses that interface. It does not connect to, control, or borrow credentials from this desktop conversation.

~~~csharp
builder.Services.AddIntentBlazorAI()
    .UseCodex(new CodexOptions
    {
        ExecutablePath = "codex",
        Model = "your-enabled-codex-model-id"
    })
    .AddTools<ParameterlessNewsService>();
~~~

`ParameterlessNewsService` must expose an attributed method with no parameters other than CancellationToken, returning an OutputDataSet. The repository's `NewsDataService.GetArticlesAsync` is a complete example. Alternatively use IOutputDataSource. Codex receives preloaded authorized data and composition instructions; this adapter does not pass native application function tools to the CLI. Parameterized tools and temperature settings are rejected for this connection. The CLI adapter does not expose a per-generation output-token limit; it enforces timeout/event/output size limits and checks token usage when reported. `ModelProfile` exposes these differences explicitly.

Use a separately installed and authenticated CLI owned by the developer. On Windows, point `ExecutablePath` to the native Codex executable rather than a PowerShell/npm command shim. CLI 0.154.0 help/version was inspected locally; inference was not run. The adapter requires `exec`, JSON events, ephemeral runs, schema output, and the ignore-config/rules flags. Older versions missing them will fail rather than silently relaxing settings.

Each request uses a fresh temporary directory, passes the prompt through stdin, and uses an argument list without a command shell. It requests read-only sandboxing, ignores user configuration and execpolicy rules, disables shell tools/apps/web search, and does not load the application repository as its workspace. It does not resume existing Codex threads. The process tree is stopped on cancellation/disposal; temporary output is cleaned up. Stderr is drained without exposing private diagnostics. Do not use a shared developer CLI sign-in as an anonymous production model service.

## Run the examples

The docs website has `/models` for optional model mode and `/playground` for an explicitly separate fixture session. Configure `Ai:Provider` as Ollama, LMStudio, OpenAI, or Codex and set `Ai:Model` on the trusted host. Local servers may use `Ai:Endpoint`; Codex may use `Ai:CodexExecutable`. Credentials are separately authorized server configuration. No automatic probe or inference occurs on page load.

The [Gaming News starter](../examples/GamingNews/README.md) is independently runnable and uses the same registration and two components. Both sample hosts default to fixtures and permit configured model mode only in Development. They are not production authentication examples. Staff Business and Asset Workbench remain authored fixtures until their later application phase.

See the [provider evaluation matrix](provider-evaluation.md). Deterministic CI verifies adapters and failure paths against substitutes; a real local and frontier run is still required before closing the live acceptance gate.
