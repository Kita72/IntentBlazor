# IntentBlazor

An open-source AI-centric Blazor framework being developed around two living components: **AiInput** receives user intent and content; **AiOutput** becomes the interactive workspace for the task.

**Status: Phase 0 foundation.** The repository builds and includes a documentation website. The AI components, output renderer, model adapters, and persistence implementation are planned work. There are no published packages yet.

## What we are building

Developers will connect a model, expose C# capabilities through attributes or explicit registration, and place the two interaction components in their app. The runtime will coordinate data, UI composition, direct interaction, and session continuity.

The initial hosting target is a Blazor Web App with Interactive Server. Local models through Ollama and LM Studio and selected frontier providers are planned; compatibility will be reported only after verification.

## Run the foundation

Install the .NET SDK selected by [global.json](global.json), Git, and PowerShell 7. No API key, model server, or database is needed.

~~~powershell
git clone https://github.com/Kita72/IntentBlazor.git
cd IntentBlazor
pwsh -File scripts/Verify.ps1
dotnet run --project apps/IntentBlazor.Docs --no-launch-profile -- --environment Development --urls http://localhost:5180
~~~

Open http://localhost:5180 for the local documentation site. The verification script restores locked dependencies, builds, checks formatting and documentation links, and produces local package artifacts. Empty library scaffolds are not published or advertised as a usable preview.

Automated behaviour tests begin with the Phase 1 contracts. The workflow discovers test projects when introduced and runs them; Phase 0 does not claim a passing test suite.

## Repository

| Path | Purpose |
| --- | --- |
| src/IntentBlazor.Core | Output and extension contracts, beginning in Phase 1. |
| src/IntentBlazor.Runtime | Sessions, model orchestration, tool execution, and document updates. |
| src/IntentBlazor.Blazor | Razor class library for the two components and internal renderers. |
| src/IntentBlazor.Storage.Sqlite | Optional session persistence adapter. |
| apps/IntentBlazor.Docs | Documentation and future demos using public APIs. |
| examples | Gaming News, Staff Business, and Asset Workbench entry points. |
| specs | Output contract notes and future authored fixtures. |
| tests | Validation responsibilities and future test projects. |

## Contribute

Start with [CONTRIBUTING.md](CONTRIBUTING.md), the [roadmap](ROADMAP.md), and [architecture decisions](docs/architecture/README.md). Discuss contract changes before implementing them. Small, focused pull requests are welcome, including documentation and reproducible output examples.

Use [Discussions](https://github.com/Kita72/IntentBlazor/discussions) for questions and proposals, and [issues](https://github.com/Kita72/IntentBlazor/issues) for defects and accepted work.

See the [implementation plan](docs/implementation-plan.md), [Phase 0 completion record](docs/phase-0-completion.md), [governance](GOVERNANCE.md), [code of conduct](CODE_OF_CONDUCT.md), and [security policy](SECURITY.md).

## License

[MIT](LICENSE). Copyright (c) 2026 Tory Bertram and contributors. Sample assets must have documented redistribution rights.
