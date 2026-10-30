# IntentBlazor

An open-source AI-centric Blazor framework being developed around two living components: **AiInput** receives user intent and content; **AiOutput** becomes the interactive workspace for the task.

**Status: Phase 5 implemented; experimental API.** AiInput and AiOutput share validated documents, protected drafts, registered actions, and optional durable workspaces. Optional packages add bounded content imports, private artifacts, renderer extensions, an image-editor pack, and embeddable AI setup/inspection. Three standalone example apps now demonstrate complete synthetic workflows. Ollama, LM Studio, OpenAI, and an experimental Codex CLI adapter have deterministic tests; live provider verification remains deferred. There are no published packages.

## What we are building

Developers will connect a model, expose C# capabilities through attributes or explicit registration, and place the two interaction components in their app. The runtime will coordinate data, UI composition, direct interaction, and session continuity.

The initial hosting target is a Blazor Web App with Interactive Server. Adapter availability and verified model compatibility are tracked separately.
The [model connection guide](docs/phase-2-model-connections.md) covers current registrations, C# data tools, limits, and the Codex adapter. The [provider matrix](docs/provider-evaluation.md) separates implemented adapters from live-model evidence.

## Run the playground

Install the .NET SDK selected by [global.json](global.json), Git, and PowerShell 7. No API key, model server, or database is needed.

~~~powershell
git clone https://github.com/Kita72/IntentBlazor.git
cd IntentBlazor
pwsh -File scripts/Verify.ps1
dotnet run --project apps/IntentBlazor.Docs --no-launch-profile -- --environment Development --urls http://localhost:5180
~~~

Open http://localhost:5180/playground. Type `news`, `staff`, or `assets`, then select records, sort the staff table, edit local drafts, and inspect the document and session state. These are synthetic fixtures, with no model calls or external application writes.

Open http://localhost:5180/workspaces for the **recovery demo**. Edit a synthetic staff follow-up, submit a deterministic follow-up request, reconnect, and reload the page. Simulate another staff member saving the shared job, then try your save: the version conflict retains your draft. This Development-only demo creates separate private-workspace and shared-business SQLite files under ignored `artifacts/phase3-demo`. Its protected synthetic identity cookie is a demo aid, not a production account system. See the [workspace and action guide](docs/phase-3-workspaces.md).

Open http://localhost:5180/content for the **content and extension demo**. Attach text, Markdown, JSON, CSV, PNG/JPEG, or a text-based PDF; inspect authorized previews, galleries and charts; resize an image to an actual derived PNG; compare/download versions; and export metadata. The same two components drive the workspace. The [Phase 4 guide](docs/phase-4-content-and-setup.md) explains content limits, public renderer registration, AI setup policy, and current capability boundaries.

Run the complete [Gaming News](examples/GamingNews/README.md), [Staff Business](examples/StaffBusiness/README.md), and [Asset Workbench](examples/AssetWorkbench/README.md) apps independently on ports 5181, 5182, and 5183. They use default AiInput/AiOutput, synthetic Development-only identities, private SQLite workspaces, and app-owned services. The [Phase 5 guide](docs/phase-5-example-apps.md) covers saved news comparisons, EF Core assignment conflicts, real image versions/exports, optional live configuration, and current boundaries.

The verification script restores locked dependencies, builds, checks formatting, runs contract/runtime/Blazor rendering tests, verifies documentation links, and creates local package artifacts. See the [Phase 1 developer guide](docs/phase-1-developer-guide.md) for a complete source-reference example using just `<AiInput />` and `<AiOutput />`.

## Repository

| Path | Purpose |
| --- | --- |
| src/IntentBlazor.Core | Output contracts, strict JSON parsing, and semantic validation. |
| src/IntentBlazor.Runtime | Scoped sessions, protected updates, conversation context, actions, and replaceable workspace storage. |
| src/IntentBlazor.AI | Optional model connections, data tools, bounded generation, and safe progress/usage metadata. |
| src/IntentBlazor.Blazor | Razor class library for the two components and internal renderers. |
| src/IntentBlazor.Storage.Sqlite | Optional SQLite checkpoints and durable action receipts. |
| src/IntentBlazor.Content | Optional content adapters, private artifact storage/delivery, and exports. |
| src/IntentBlazor.Packs.ImageEditor | Typed image comparison/zoom renderer and registered resize action. |
| src/IntentBlazor.Blazor.AI | Optional embeddable AiSetup and AiInspector tools. |
| apps/IntentBlazor.Docs | Documentation and an interactive document playground. |
| examples | Shared fixture catalog and three application directions. |
| specs | Versioned contract reference and authored fixture documents. |
| tests | Contract, session, and real Blazor HTML renderer tests. |

## Contribute

Start with [CONTRIBUTING.md](CONTRIBUTING.md), the [roadmap](ROADMAP.md), and [architecture decisions](docs/architecture/README.md). Discuss contract changes before implementing them. Small, focused pull requests are welcome, including documentation and reproducible output examples.

Use [Discussions](https://github.com/Kita72/IntentBlazor/discussions) for questions and proposals, and [issues](https://github.com/Kita72/IntentBlazor/issues) for defects and accepted work.

See the [implementation plan](docs/implementation-plan.md), [Phase 0 completion record](docs/phase-0-completion.md), [governance](GOVERNANCE.md), [code of conduct](CODE_OF_CONDUCT.md), and [security policy](SECURITY.md).

## License

[MIT](LICENSE). Copyright (c) 2026 Tory Bertram and contributors. Sample assets must have documented redistribution rights.
See [third-party notices](THIRD-PARTY-NOTICES.md) for the optional content dependencies.
