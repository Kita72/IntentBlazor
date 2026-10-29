# Phase 1 developer guide

Phase 1 implements the common output document and deterministic renderer. Its public API is experimental and packages have not been published. The tested host is a .NET 10 Blazor Web App using Interactive Server. Provider integration is Phase 2; file attachments, persistence, application saves, and custom renderer registration are not available yet.

## Try the three workspaces

From the repository root:

~~~powershell
pwsh -File scripts/Verify.ps1
dotnet run --project apps/IntentBlazor.Docs --no-launch-profile -- --environment Development --urls http://localhost:5180
~~~

Open http://localhost:5180/playground. Submit `news`, `staff`, or `assets`. News selection reveals an article, source, and rating. The staff table supports sorting, record selection, owner choice, and notes edits. Asset selection reveals a preview and working label. Clear edited drafts before switching records or fixtures.

The JSON explorer edits only the presentation document against the current fixture's host-owned datasets. Render a proposal to validate and replace it; try the invalid-schema control to see a rejection that preserves the working view. Expand state and history to inspect local changes. Refreshing the page loses this in-memory session.

## Add the two components to a fresh app

Create a Blazor Web App with Server interactivity using the SDK in [global.json](../global.json). Until packages are published, add a project reference from your app to this checkout's `src/IntentBlazor.Blazor/IntentBlazor.Blazor.csproj`. It brings the Core and Runtime projects transitively. The SDK-generated app already includes the Blazor script and its own scoped stylesheet bundle; retain those template files.

In `Program.cs`, alongside the app's existing Razor component registrations, register the framework and your processor:

~~~csharp
using IntentBlazor.Runtime;
using IntentDemo.Services;

builder.Services.AddIntentBlazor();
builder.Services.AddScoped<IInputProcessor, GreetingProcessor>();
~~~

Add this complete `Services/GreetingProcessor.cs` to an app named `IntentDemo`:

~~~csharp
using IntentBlazor.Core;
using IntentBlazor.Runtime;

namespace IntentDemo.Services;

public sealed class GreetingProcessor : IInputProcessor
{
    public ValueTask<OutputContent> ProcessAsync(
        string input, WorkspaceSnapshot workspace, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new OutputContent
        {
            Document = new OutputDocument
            {
                Id = "greeting",
                RootId = "result",
                Nodes = [new OutputNode
                {
                    Id = "result",
                    Kind = NodeKind.Text,
                    Title = "Your request",
                    Value = ValueBinding.Constant(input)
                }]
            }
        });
    }
}
~~~

Replace the app's home page with:

~~~razor
@page "/"
@rendermode InteractiveServer
@using IntentBlazor.Blazor

<PageTitle>Intent demo</PageTitle>
<h1>Intent demo</h1>
<AiInput />
<AiOutput />
~~~

This processor echoes the request as encoded text. It proves the integration seam without a model. Your processor can inject C# services through the app's normal dependency injection and return authorized host data plus a presentation document. The future attribute and explicit tool-registration APIs are still planned; `IInputProcessor` is not an implemented AI tool system.

`AddIntentBlazor()` alone supplies a safe rejecting default processor, so an app can render authored output before connecting input processing. Both UI components automatically use the same scoped `AiSession`; no component references, event relays, or page-level state binding are required. Keep them within the same interactive server circuit. The documentation shell stays static SSR with page-level interactivity on the playground.

AiInput stays disabled with a Connecting label during prerendering and enables itself after its first interactive render. This prevents an early click from posting an interactive form through the static SSR form handler.

## Supply data and open a document

Inject `AiSession` into your interactive page or input processor's host integration, then call `Open(outputContent)`. To load a presentation proposal separately from host data, use `OpenJson(documentJson, hostData)`. Check `OperationResult.Success` and `Errors` when calling these APIs directly; `AiOutput` also displays session errors.

The host supplies only records authorized for the current user and validates its own data sources. The document references dataset IDs and declared fields; it cannot fetch arbitrary data. For a complete example with real selection bindings, use the [news fixture](../specs/fixtures/news.json), [staff fixture](../specs/fixtures/staff.json), or [assets fixture](../specs/fixtures/assets.json). `ExampleCatalog` in the non-packable examples project loads those embedded files and `ExampleInputProcessor` maps the three exact commands to them.

For multiple independent workspaces in one circuit, create separately owned sessions and pass the same explicit `Session` to each pair:

~~~razor
<AiInput Session="@workspace" />
<AiOutput Session="@workspace" />
~~~

Dispose sessions you create yourself; DI disposes its own scoped session. Use the Blazor dispatcher for session operations and external notifications. This version does not support simultaneous updates from arbitrary background threads or shared sessions across staff members.

## Composition, interaction, and continuity

The built-in toolkit composes layout, card, text, list, table, rating, input, select, button, image, and form nodes. New combinations are new document trees, not generated executable code. [Schema 0.1](../specs/output-document/README.md) describes each envelope member, bindings, local effects, and limits. Custom primitives and capability discovery come later.

Selection, sorting, and editing execute locally. Selection effects assign typed state atomically. Inputs mark edited state as dirty. Another selection cannot overwrite those targets, and document replacement refuses a dirty workspace by default. A declared clear-draft button restores the current baseline; it never writes application data. An explicit host `Open(..., discardDraft: true)` opts into losing that draft.

Each successful local event increments the session revision. Controls submit the revision they were rendered against. Input processing receives a snapshot; a response prepared before another local change is rejected as stale. A busy request can be cancelled. Validation errors, cancellation, and processor failures retain the current output.

Memory currently consists of the current document, selection/state, table sorting, dirty-field baselines, and the latest 20 input outcomes in one circuit. There are no folders, durable chats, restoration, or automatic model memory yet. Those require explicit ownership, authorization, retention, and reconciliation in Phase 3.

## Validation and contribution

The strict JSON codec rejects unknown and duplicate members, integer enums, excess nesting, and oversized payloads. Semantic validation checks graph integrity, references, typed values, available fields, supported toolkit versions, and event effects. Rendering encodes scalar text and limits image delivery to host-provided application-local paths. Application data access still belongs to your app.

Run the repository verification script before submitting changes. Tests exercise all three domains through the same production contracts and components; see [the test guide](../tests/README.md). Discuss breaking contract changes first and keep new fields, renderer behavior, fixtures, and regression tests in sync.
