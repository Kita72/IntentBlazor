# IntentBlazor

An open-source AI-centric Blazor framework being developed around two living components: **AiInput** receives user intent and content; **AiOutput** becomes the interactive workspace for the task.

**Status: Phase 2 adapters implemented; live verification deferred.** AiInput and AiOutput share versioned documents, validation, deterministic rendering, and an optional model pipeline. Ollama, LM Studio, OpenAI, and an experimental Codex CLI adapter have deterministic tests. No real model profile is certified yet. Application actions and durable persistence remain planned. There are no published packages; the public API is experimental.

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

The verification script restores locked dependencies, builds, checks formatting, runs contract/runtime/Blazor rendering tests, verifies documentation links, and creates local package artifacts. See the [Phase 1 developer guide](docs/phase-1-developer-guide.md) for a complete source-reference example using just `<AiInput />` and `<AiOutput />`.

## Repository

| Path | Purpose |
| --- | --- |
| src/IntentBlazor.Core | Output contracts, strict JSON parsing, and semantic validation. |
| src/IntentBlazor.Runtime | Scoped in-memory sessions, local events, drafts, and an input processor seam. |
| src/IntentBlazor.AI | Optional model connections, data tools, bounded generation, and safe progress/usage metadata. |
| src/IntentBlazor.Blazor | Razor class library for the two components and internal renderers. |
| src/IntentBlazor.Storage.Sqlite | Reserved for the optional persistence adapter; not implemented. |
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
