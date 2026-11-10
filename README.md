# IntentBlazor

An open-source AI-centric Blazor framework being developed around two living components: **AiInput** receives user intent and content; **AiOutput** becomes the interactive workspace for the task.

**Status: `0.1.0-preview.1` developer preview is published on NuGet.org; the API is experimental.** AiInput and AiOutput share validated documents, protected drafts, registered actions, and optional durable workspaces. Optional packages add bounded content imports, private artifacts, renderer extensions, an image-editor pack, and embeddable AI setup/inspection. Three standalone example apps demonstrate complete synthetic workflows. All eight public packages and symbols were checked against the frozen release source, and five independent apps passed using fresh public-only package caches. Framework adapters are verified offline; live model reports are optional compatibility observations. See the [publication record](docs/foundation-step6-developer-preview.md) and [installation guide](docs/site/installation.md).

[Public docs and quickstart](https://intentblazor.runasp.net/docs/quickstart) · [Interactive document explorer](https://intentblazor.runasp.net/explorer) · [Demo hub](https://intentblazor.runasp.net/demos). The hosted site uses deterministic fixture mode; see the [hosting verification record](docs/phase-7-hosting.md).

## What we are building

Developers will connect a model, expose C# capabilities through attributes or explicit registration, and place the two interaction components in their app. The runtime will coordinate data, UI composition, direct interaction, and session continuity.

The initial hosting target is a Blazor Web App with Interactive Server. Adapter availability and verified model compatibility are tracked separately.
All six [foundation steps](docs/foundation-completion-plan.md) are complete. The [production hosting guide and compiled recipe](docs/foundation-step4-production-hosting.md) add trusted identity, private durable workspaces, authorized services/actions, per-user admissions and recovery procedures around the same two default components. [Step 5](docs/foundation-step5-release-operations.md) adds exact-candidate evidence, isolated SDK 11 checks and sealed IIS/native/browser rollback verification. [Step 6](docs/foundation-step6-developer-preview.md) publishes and verifies the first preview under [Wilson72](https://www.nuget.org/profiles/Wilson72), with matching MonsterASP HTTPS and interactive-circuit evidence. Native net11 targeting remains unverified after a recorded analyzer failure; the supported baseline stays net10 Interactive Server.
Developers supply their model, connection/credential configuration and application services through public registrations. They evaluate their chosen model and workload; a framework release does not require local or paid inference. [Decision 0012](docs/architecture/0012-framework-verification-and-optional-model-reports.md) defines the offline verification boundary.
The [model connection guide](docs/phase-2-model-connections.md) covers current registrations, C# data tools, limits, and the Codex adapter. The [provider matrix](docs/provider-evaluation.md) separates implemented adapters from live-model evidence.

The [.NET 11 migration plan](docs/net11-migration-plan.md), reviewed against the November 10 RTM release, proposes the next platform upgrade and its acceptance gates. The published preview currently targets .NET 10.

## Run the learning site and playground

Install the .NET SDK selected by [global.json](global.json), Git, and PowerShell 7. No API key, model server, or database is needed.

~~~powershell
git clone https://github.com/Kita72/IntentBlazor.git
cd IntentBlazor
pwsh -File scripts/Verify.ps1
dotnet run --project apps/IntentBlazor.Docs --no-launch-profile -- --environment Development --urls http://localhost:5180
~~~

Open http://localhost:5180/docs for installation, the compiled quickstart, service/renderer tutorials, contracts, hosting, and preview limits. Open http://localhost:5180/demos for labelled demos, or http://localhost:5180/explorer for the interactive document explorer (`/playground` remains an alias). Type `news`, `staff`, or `assets`, then select records, sort the staff table, edit local drafts, and inspect the document and session state. These are synthetic fixtures, with no model calls or external application writes.

Open http://localhost:5180/workspaces for the **recovery demo**. Edit a synthetic staff follow-up, submit a deterministic follow-up request, reconnect, and reload the page. Simulate another staff member saving the shared job, then try your save: the version conflict retains your draft. This Development-only demo creates separate private-workspace and shared-business SQLite files under ignored `artifacts/phase3-demo`. Its protected synthetic identity cookie is a demo aid, not a production account system. See the [workspace and action guide](docs/phase-3-workspaces.md).

Open http://localhost:5180/content for the **content and extension demo**. Attach text, Markdown, JSON, CSV, PNG/JPEG, or a text-based PDF; inspect authorized previews, galleries and charts; resize an image to an actual derived PNG; compare/download versions; and export metadata. The same two components drive the workspace. The [Phase 4 guide](docs/phase-4-content-and-setup.md) explains content limits, public renderer registration, AI setup policy, and current capability boundaries.

Run the complete [Gaming News](examples/GamingNews/README.md), [Staff Business](examples/StaffBusiness/README.md), and [Asset Workbench](examples/AssetWorkbench/README.md) apps independently on ports 5181, 5182, and 5183. They use default AiInput/AiOutput, synthetic Development-only identities, private SQLite workspaces, and app-owned services. The [Phase 5 guide](docs/phase-5-example-apps.md) covers saved news comparisons, EF Core assignment conflicts, real image versions/exports, optional live configuration, and current boundaries.

The verification script restores locked dependencies, builds, checks formatting, runs contract/runtime/Blazor rendering and offline browser tests, verifies documentation links, and creates local package artifacts. Browser checks install pinned Chromium headless and run serially with GPU/WebGL/Vulkan disabled; Linux hosts can use `-InstallBrowserDependencies` for required OS dependencies. See the [browser guide](docs/foundation-step3-browser-regressions.md), [current quickstart](docs/site/quickstart.md) and [runnable sample](examples/Quickstart/README.md) using just `<AiInput />` and `<AiOutput />`. Verification also builds and browses a copy against local packages outside the source tree.

## Repository

Tory develops and verifies this project in Codex, then commits and pushes directly to `main`. GitHub holds the source and history, with one upstream branch. Dependabot and GitHub Actions are disabled for this repository; dependency updates are handled deliberately during local development. Do not introduce bots, workflows or additional upstream branches without Tory's instruction. [Decision 0014](docs/architecture/0014-main-only-repository.md) records this workflow. Package publication and site deployment are separate, explicit tasks.

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
| examples/ProductionHost | Application-owned production integration recipe with offline output and no sign-in shortcut. |
| specs | Versioned contract reference and authored fixture documents. |
| tests | Contract, session, and real Blazor HTML renderer tests. |

## Contribute

Start with [CONTRIBUTING.md](CONTRIBUTING.md), the [roadmap](ROADMAP.md), and [architecture decisions](docs/architecture/README.md). Coordinate with Tory before substantial external work or public contract changes. Requested external contributions use contributor-owned forks, keeping the upstream repository on `main` only.

Use [Discussions](https://github.com/Kita72/IntentBlazor/discussions) for questions and proposals, and [issues](https://github.com/Kita72/IntentBlazor/issues) for defects and accepted work.

See the [implementation plan](docs/implementation-plan.md), [Phase 0 completion record](docs/phase-0-completion.md), [governance](GOVERNANCE.md), [code of conduct](CODE_OF_CONDUCT.md), and [security policy](SECURITY.md).

## License

[MIT](LICENSE). Copyright (c) 2026 Tory Bertram and contributors. Sample assets must have documented redistribution rights.
See [third-party notices](THIRD-PARTY-NOTICES.md) for the optional content dependencies.
