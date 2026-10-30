# IntentBlazor Roadmap

The target is a developer preview demonstrating the same input, output-document, session, and tool mechanisms across three different app domains.

| Phase | Milestone | Status |
| --- | --- | --- |
| 0 | Open-source foundation, buildable projects, docs shell, contributor workflow | Complete |
| 1 | Versioned documents and deterministic renderer | Implemented; experimental API |
| 2 | First verified local and frontier model interaction | Adapters implemented; live gate deferred |
| 3 | Stateful updates, actions, persistence, and conflict recovery | Implemented; deterministic recovery demonstrated |
| 4 | Content adapters, extensions, and embeddable AI setup | Planned |
| 5 | Gaming News, Staff Business, and Asset Workbench apps | Planned |
| 6 | Complete docs, document explorer, and demos | Planned |
| 7 | Package verification and public developer preview | Planned |

See [the implementation plan](docs/implementation-plan.md) for phase deliverables and acceptance gates. No release dates are committed.

Phase 1 evidence: [developer guide](docs/phase-1-developer-guide.md), [contract reference](specs/output-document/README.md), [authored fixtures](specs/fixtures/README.md), and [automated checks](tests/README.md). The three fixture workspaces share one renderer. Phase 2 adds the [connection guide](docs/phase-2-model-connections.md), [provider matrix](docs/provider-evaluation.md), and a runnable Gaming News starter. The complete standalone application workflows remain Phase 5 work.

First contributor opportunities: review the experimental output contract, author representative documents, improve setup instructions, and expand keyboard interaction guidance. Coordinate through an issue before adding public types.

Phase 3 evidence: [workspace and action guide](docs/phase-3-workspaces.md), [schema 0.2 action contract](specs/output-document/v0.2-actions.md), and [durable workspace decision](docs/architecture/0006-durable-workspaces.md). The Development-only recovery demo uses real SQLite files and shared-record version checks. Four phases remain after Phase 3: **4 content/extensions/setup**, **5 the complete three apps**, **6 the complete docs and demo site**, and **7 developer preview**. The deferred Phase 2 live-model acceptance gate must also pass before the preview.

Later directions include additional renderers, content formats, providers, storage adapters, and supported hosting models. Arbitrary generated scripts and real-time collaborative editing require separate designs.
