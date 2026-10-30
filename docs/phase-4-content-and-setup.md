# Phase 4: content, renderer packs, and AI setup

Phase 4 adds optional content/artifact services, public renderer registration, a working image-editor pack, and embeddable AI setup and inspection. **AiInput and AiOutput remain the two interaction components.** The supporting setup tools are optional. APIs and packages remain experimental; local packing is not a public release.

## Run the deterministic demo

~~~powershell
dotnet run --project apps/IntentBlazor.Docs --no-launch-profile -- --environment Development --urls http://localhost:5180
~~~

Open http://localhost:5180/content. Start a private demo workspace if prompted. The Development-only host uses the same protected synthetic browser identity as the recovery demo. It is not a production account system.

1. Create the synthetic 64 by 64 PNG, or use **Attach content** in AiInput.
2. Select an attachment. AiOutput renders the gallery, size chart, and authorized artifact preview.
3. For an image, change target dimensions and use **Resize preview**. This executes a registered server action and creates an actual PNG with a parent reference to the original.
4. Use **Show original**, **Show preview**, and local zoom to inspect the result. **Download displayed image** downloads that selected version's actual bytes.
5. Upload a text/Markdown, JSON, CSV, or text-based PDF file, then submit a request or refresh the content view. Select it to inspect the bounded extraction and download the original.
6. Export attachment metadata as JSON or CSV. These create real downloadable files, not status-only responses.
7. Reload to recover the workspace, selection, artifact references, and comparison choice from the checkpoint.

The content view is deterministic and makes no model calls. AI setup displays host-approved connections when configured. Its probe is a separate explicit action; automatic verification never calls a live model. See [ContentDemo.cs](../apps/IntentBlazor.Docs/ContentDemo.cs), [the demo page](../apps/IntentBlazor.Docs/Components/Pages/Content.razor), and [host registration](../apps/IntentBlazor.Docs/WorkspaceDemo.cs).

## Optional packages

| Package | Responsibility |
| --- | --- |
| IntentBlazor.Content | Parsers, bounded imports, artifact storage/delivery, JSON/CSV export, and image processing. |
| IntentBlazor.Packs.ImageEditor | Typed comparison events, local zoom, the registered resize action, and a starter document factory. |
| IntentBlazor.Blazor.AI | AiSetup, its focused editor/probe components, and AiInspector. |

Core, Runtime, and the ordinary Blazor package do not reference the parser/native-image package. The two components discover the runtime content seam through DI when the host opts in.

## Add content to an authenticated host

~~~csharp
builder.Services.AddIntentContent(privateArtifactDirectory);
builder.Services.AddBlazorWorkspaceIdentity();
builder.Services.AddIntentImageEditor();
~~~

Use the `IntentBlazor.Content`, `IntentBlazor.Blazor`, and `IntentBlazor.Packs.ImageEditor` namespaces. Configure the host's authentication normally, and keep the artifact directory outside publicly served content. Map authenticated artifact delivery after authentication and authorization middleware:

~~~csharp
app.MapIntentContent();
~~~

The default HTTP delivery identity is the authenticated name-identifier claim. It must match `IWorkspaceIdentity`. A tenant-qualified or custom identity implementation must supply the same mapping to `MapIntentContent(context => ...)`; the delegate derives identity from trusted server authentication, never from a submitted owner ID.

Connect the scoped session to an explicit private workspace key as described in the [Phase 3 guide](phase-3-workspaces.md). AiInput then enables its attachment controls without a new consumer parameter:

~~~razor
<AiInput />
<AiOutput />
~~~

For multiple explicitly owned sessions, use the existing `Session` parameter. The host's `IWorkspaceAccess` must reauthorize attached artifacts as well as datasets during save/restore. The [demo access service](../apps/IntentBlazor.Docs/ContentDemo.cs) composes domain access with artifact lookup; it is not a grant of access for arbitrary datasets.

## Content capabilities and bounds

| Format | Preview / extraction | Editing | Export / download |
| --- | --- | --- | --- |
| UTF-8 text | Encoded text excerpt | No text editor in this phase | Original download |
| Markdown | Headings, paragraphs, and bullet blocks; raw HTML is encoded, other inline syntax remains literal | No | Original download |
| JSON | Bounded text; scalar object arrays can become a validated dataset | No | Original download; host datasets export as JSON |
| CSV | Quoted-field parsing into a bounded dataset plus text preview | No | Original download; host datasets export as CSV |
| PNG / JPEG | Image and decoded dimensions | Nearest-neighbor resize to a new PNG | Original or derived image download |
| PDF | Bounded text extraction; no OCR or full-layout reproduction | No | Original PDF download; no PDF generation |

Images contribute metadata, not pixel content, to this phase's model context. Selecting a chat provider does not imply vision or image generation support. SVG uploads, Office formats, arbitrary HTML, scripts, and remote file fetching are not adapters in this phase. Application-local legacy sample SVGs remain available through the original image node.

Defaults: 8 MiB per uploaded file, 8 attachments through AiInput, 16 million decoded pixels, 20 PDF pages, and 16,000 extracted text characters per artifact. Text previews mark truncation while retaining original bytes. Structured extraction supports up to 250 rows and 31 source columns, scalar cells of the existing document types, and the 256 KiB output-content budget. A valid JSON/CSV file whose extracted table exceeds that budget keeps its original and bounded text without exposing an invalid dataset. The model receives a separate aggregate attachment-text budget of 16,000 characters, subject to its overall context budget.

Configure `ContentLimits` in `AddIntentContent`. Actual streamed bytes are checked against both the declared length and configured limit. Image signatures/dimensions are checked before full decode; PDFs require a signature, strict parsing, bounded stack depth, and a page limit. Parsing is synchronous once bytes are loaded; cancellation is checked at stream/page boundaries. These limits are not a process-isolation or hard CPU/memory sandbox for arbitrary hostile parsers.

Implement `IContentAdapter` to add a format with an explicit capability list and bounded analysis. Implement `IArtifactStore` to replace file storage. Both are host extension contracts; adding a format does not make its contents authoritative instructions.

## Artifact ownership, provenance, and cleanup

The file adapter stores content under hashed owner directories and generated IDs, with SHA-256 versions and integrity-checked immutable bytes. Metadata records name, media type, byte count, provenance, timestamp, dimensions/extracted data where available, and a parent reference for derived content. Downloads recheck the current HTTP identity, use no-store/nosniff headers, and force non-image files to download rather than execute as a page.

The adapter is for single-instance hosts. Default owner quotas are 64 MiB and 128 artifacts. Removing an attachment detaches its reference; it does not delete the original. A host can call `CleanupAsync(owner, before, retained)` with retained references from all relevant workspaces. Cleanup keeps the transitive parents of retained derived artifacts and never crosses owners. No background deletion is enabled automatically. Hosts own backups, retention policy, filesystem protection, and distributed storage implementations.

`WorkspaceContent.ExportAsync` writes a validated dataset as JSON or CSV. CSV export quotes cells and prefixes formula-like text so opening an export in a spreadsheet cannot treat that text as a formula. JSON preserves the original scalar values. A resize re-encodes to PNG; original source metadata/bytes remain recoverable through the parent artifact.

## Register a renderer without editing core

Schema 0.3 adds `markdown`, `chart`, `gallery`, `artifact`, and `extension`. Cards/layouts/forms can compose them. The [schema reference](../specs/output-document/v0.3-content-and-extensions.md) describes their bindings.

Create an `OutputExtensionContract<TConfiguration,TEvent>` with:

- an `ExtensionDescriptor` containing pack, exact version, renderer ID, description, and a canonical example configuration;
- typed configuration validation against the document;
- declared local state bindings through `EditableStates`;
- a pure `Reduce` method returning bounded typed `ExtensionEffect` values.

Then create a Razor component inheriting `IntentExtensionComponent<TConfiguration>` and register it:

~~~csharp
builder.Services.AddIntentRenderer<MyContract, MyRenderer>();
~~~

Include `Microsoft.AspNetCore.Components.Web` in the Razor library's imports so DOM event directives are compiled as handlers. Dispatch a local event with `Session.DispatchExtension(Node.Id, Session.Revision, typedEvent)`. The runtime checks revision, source visibility, event/configuration types, declared state targets, ranges, and dirty-field protection before committing effects. These reductions cannot authorize application writes; use registered server actions for those.

Use `OutputChildren` to compose ordinary framework nodes inside a registered renderer. The working [image contract](../src/IntentBlazor.Packs.ImageEditor/ImageEditorContract.cs), [renderer](../src/IntentBlazor.Packs.ImageEditor/ImageEditorView.razor), and [starter document](../src/IntentBlazor.Packs.ImageEditor/ImageEditorDocuments.cs) demonstrate the complete pattern. [ExtensionRendererTests.cs](../tests/IntentBlazor.Tests/ExtensionRendererTests.cs) also registers an independent counter renderer using only public APIs.

Required packs appear in `requires` with exact versions and fail validation if unavailable. Optional unavailable extensions render their encoded fallback and cannot contain interactive children. The model never selects a CLR type or executes generated C#/Razor/JavaScript. Unknown document schemas and incompatible required versions preserve the current working output.

## Embed AI setup and diagnostics

Keep the existing default model registration. Optional named presets reuse the same adapters and server-owned credential resolvers:

~~~csharp
var ai = builder.Services.AddIntentBlazorAI();
ai.UseOllama(installedLocalModel)
  .AddLMStudioPreset("desktop", installedDesktopModel)
  .AddOpenAIPreset("frontier", enabledFrontierModel, resolveServerCredential);
builder.Services.AddIntentAiSetup<AppAiSetupAccess>();
~~~

The model identifiers and credential resolver above are supplied by host configuration. They are not client text fields. For custom clients use `AddIntentModelPreset(new NamedModelConnection(...))`; its factory must create a client owned by the setup scope. The ordinary `UseCodex` registration can remain the default workstation adapter; its narrower controls are shown as unsupported.

Implement `IAiSetupAccess` to return an `AiSetupPolicy` for the current trusted user. The policy separately controls viewing, editing, probing, inspection, advanced options, connection IDs, and output/context budgets. The service rechecks policy on changes and before a probe is sent. Restricted controls remain enforced in C#, even when a caller bypasses the UI. Select one default connection before adding optional presets.

Add the optional supporting tools wherever the host wants them:

~~~razor
@using IntentBlazor.Blazor.AI
<AiSetup />
<AiInspector />
~~~

AiSetup edits scoped effective options for the next generation, without changing an in-flight request. It supports temperature, output/context budgets, JSON-schema mode, Top P, frequency/presence penalties, seed, and opt-in reasoning effort. Host safety limits on timeout, calls, corrections, and usage remain enforced. Reasoning effort must be enabled for a suitable host-configured OpenAI profile through `supportsReasoningEffort`; a provider name alone does not verify a model's support. Unsupported adapter controls are disabled/explained. Effective settings are visible separately from an unapplied edit.

Connection endpoints, model presets, credentials, and policy remain host-owned. Scope settings are not saved into output documents or durable checkpoints; applications may persist permitted user preferences through their own store. Setup is not an account/secret-management screen.

The explicit probe sends a small request with no application tools or datasets. It reports connectivity and valid presentation separately and always states that tool use was not tested. It does not certify a provider profile. The inspector exposes validated output/state, bounded errors, timing, reported usage, and tool name/outcome/dataset/count metadata. It does not collect raw tool payloads, hidden reasoning, or credentials. Restrict inspector access to users permitted to view that workspace's content.

## Acceptance and remaining phases

The [test record](../tests/README.md) maps the content, storage, renderer, action, setup, and transport behavior to executable tests. Browser evidence and the package-only consumer check are distinct from deterministic tests. Live-provider/model verification remains deferred by maintainer direction.

After Phase 4, phases **5 (complete example apps)**, **6 (complete docs/demo website)**, and **7 (developer preview release)** remain. Phase 4's deterministic demo is not a claim that those standalone applications or the preview release are complete.
