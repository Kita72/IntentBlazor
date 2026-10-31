# Install the experimental framework

The framework is currently available **from source and locally packed artifacts**. There is no public NuGet release yet. All eight library projects target `net10.0`; use the SDK in [global.json](../../global.json). The docs and examples are applications, not framework packages.

## Clone and verify

Install Git, PowerShell 7, and a .NET 10.0.4xx SDK satisfying the repository's SDK policy. Then:

~~~powershell
git clone https://github.com/Kita72/IntentBlazor.git
cd IntentBlazor
pwsh -File scripts/Verify.ps1
dotnet run --project apps/IntentBlazor.Docs --no-launch-profile -- --environment Development --urls http://localhost:5180
~~~

Verification restores locked dependencies, builds the solution, checks formatting, runs tests and documentation checks, packs the libraries, and builds a fresh copy of the quickstart against those local packages. No model account or private data is required. The docs site opens at `http://localhost:5180/docs`.

## Choose packages

| Package | Add it for |
| --- | --- |
| IntentBlazor.Blazor | AiInput, AiOutput, built-in rendering; includes Runtime and Core transitively. |
| IntentBlazor.AI | Ollama, LM Studio, OpenAI, custom IChatClient, and experimental Codex integration. |
| IntentBlazor.Storage.Sqlite | Private durable workspaces, checkpoint concurrency, and action receipts. |
| IntentBlazor.Content | Bounded file adapters, private artifact storage, authenticated delivery, and exports. |
| IntentBlazor.Packs.ImageEditor | Typed image comparison/zoom and a registered PNG resize action. |
| IntentBlazor.Blazor.AI | Optional AiSetup and AiInspector components for a host-approved setup policy. |
| IntentBlazor.Core | Document/data contracts, strict parsing, semantic validation, and extension contracts. |
| IntentBlazor.Runtime | Session coordination, state, actions, ownership, and storage interfaces without a Razor dependency. |

## Use a fresh Blazor app

The smallest runnable consumer is [Quickstart](../../examples/Quickstart/README.md). To start from an empty app instead, create one with `dotnet new blazor -n MyIntentApp -int Server --empty`. Add project references to `src/IntentBlazor.Blazor` and, for models, `src/IntentBlazor.AI`. Copy the [quickstart registrations and page](quickstart.md), adapting the namespace and service to your app. Keep the template's static assets, antiforgery middleware, and Interactive Server registrations.

For a package-only consumer, first run verification to produce `artifacts/packages/*.nupkg`. Add that **absolute local directory** as a package source alongside nuget.org in your consumer's `NuGet.Config`. Add `IntentBlazor.Blazor` and optionally `IntentBlazor.AI` at version `0.1.0-alpha.0`; the public feed supplies their third-party dependencies. Never remove your normal package sources to install the local artifacts.

The reproducible version of that process is [Verify-Quickstart.ps1](../../scripts/Verify-Quickstart.ps1). It copies the runnable sample to a new OS-temporary directory, substitutes package references, creates an isolated package-source configuration, and restores/builds it outside the repository. It never publishes a package or edits your user-level NuGet configuration.

## Where to go next

Run the [credential-free quickstart](quickstart.md), then [connect a provider and expose data](services.md). Read [hosting and migration](hosting.md) before enabling durable sessions or deploying a public model-backed app. WebAssembly and MAUI/Hybrid hosting have not been verified.
