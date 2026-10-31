# Expose a C# service to a model

The framework connects a provider through `Microsoft.Extensions.AI.IChatClient`, then exposes only explicitly registered read methods. Domain services need no provider SDK reference or prompt-building code. The consumer's page remains the [two-component quickstart](quickstart.md).

## Write the service

This is the compiled [CatalogService.cs](../../examples/Quickstart/CatalogService.cs). It uses synthetic public data; in a real app, inject the current user/resource service and authorize the query before returning rows.

{{sample: CatalogService.cs}}

`[AiDataTool]` is opt-in on a public instance method. Supported returns are `OutputDataSet`, `Task<OutputDataSet>`, or `ValueTask<OutputDataSet>`. Parameters may be `string`, `int`, `bool`, or `CancellationToken`; overloads, generic/ref methods, arbitrary DTO parameters, and write operations are unsupported. Tool names start with a letter and use letters, digits, or underscores, at most 64 characters. Descriptions are nonempty and at most 500 characters.

Dataset schemas and rows must pass normal document-data validation. Tool calls do not authorize access. Do not expose secrets or use a model-controlled string as a raw SQL statement, URL, or filesystem path.

## Register attributes or methods explicitly

The actual [QuickstartRegistration.cs](../../examples/Quickstart/QuickstartRegistration.cs) supports both styles. `Ai:ExplicitTools=true` selects `AddTool<T>(method, name, description)`; otherwise `AddTools<T>()` discovers attributes. Use one registration style per tool name.

{{sample: QuickstartRegistration.cs}}

`AddDataSource<T>()` preloads authorized datasets for every request. This example intentionally also exposes a read tool for filtered access; the processor replaces repeated results for the same dataset ID rather than treating model-supplied rows as authoritative. Do not register two preload sources that return the same dataset ID.

Each request and tool call receives bounded context. The synthetic sample's configuration allows at most four model calls, four tool calls, one correction, 4,096 output tokens, and 45 seconds. These are per-request limits, not a cross-user cost cap. Your host owns quotas and rate limits.

## Configure a provider

Set server-side configuration keys `Ai:Provider` and `Ai:Model` in the sample. Values for the provider are `Ollama`, `LMStudio`, or `OpenAI`. Use the model identifier actually installed or enabled for your account. The sample leaves `Ai:Provider` unset by default.

Ollama defaults to `http://localhost:11434/v1/`; LM Studio defaults to `http://localhost:1234/v1/`. Those addresses refer to the **Blazor server's machine**. For a remote model host use the endpoint overload and HTTPS. A deployed website cannot reach a visitor's local model through the server's localhost address.

OpenAI reads `Ai:ApiKey` on the server through the supplied credential delegate. Keep it in host secret storage, never a document, browser field, checked-in setting, or log. Example local development setup:

~~~powershell
dotnet user-secrets init --project examples/Quickstart
dotnet user-secrets set "Ai:Provider" "OpenAI" --project examples/Quickstart
dotnet user-secrets set "Ai:Model" "YOUR_ENABLED_MODEL_ID" --project examples/Quickstart
dotnet user-secrets set "Ai:ApiKey" "YOUR_LOCAL_SECRET" --project examples/Quickstart
dotnet run --project examples/Quickstart --no-launch-profile -- --environment Development --urls http://localhost:5184
~~~

Enter your real secret only on your own machine. Environment variables or a managed secret store are alternatives. User secrets load only in Development. The provider guide covers [custom endpoints, Codex CLI, model profiles, and capability limits](../phase-2-model-connections.md); optional [AiSetup and AiInspector](../phase-4-content-and-setup.md) work under a host-approved setup policy.

## Keep reads and writes separate

Read tools return authorized data; the model composes presentation. Registered [actions](../../specs/output-document/v0.2-actions.md) run only after an explicit user activation and server checks. They require an owned, connected workspace and a versioned application handler. Do not hide a business save behind an attributed read method.

A connectivity or presentation probe is not a compatibility certificate. See the [provider matrix](../provider-evaluation.md) for actual evidence and the [hosting guide](hosting.md) for authentication, isolation, and operational requirements.
