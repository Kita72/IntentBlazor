# IntentBlazor Roadmap

The target is a developer preview demonstrating the same input, output-document, session, and tool mechanisms across three different app domains.

[Decision 0014](docs/architecture/0014-main-only-repository.md), approved November 6, 2026, makes GitHub a source/history backup with only `main`. Development, verification and deliberate dependency maintenance happen locally in Codex. Dependabot and GitHub Actions are disabled. Earlier CI and automated-publishing descriptions are historical; publication remains a separately requested task with a publishing method still to be selected.

The [foundation completion plan](docs/foundation-completion-plan.md) was approved by Tory on October 31, 2026 and revised on November 4. Steps 1–3 are implemented: offline browser regressions and contributor guides, provider generation fixes and offline-verified connection/evaluation paths. Live local/paid model evaluation is optional developer compatibility work, outside the framework release requirements. Do not start local inference on Tory's current machine or run paid inference. Steps 4–6 cover developer-host and release work, with later platform/media expansion kept separate.

| Phase | Milestone | Status |
| --- | --- | --- |
| 0 | Open-source foundation, buildable projects, docs shell, contributor workflow | Complete |
| 1 | Versioned documents and deterministic renderer | Implemented; experimental API |
| 2 | Configurable local/frontier adapters and C# tool interaction | Implemented with offline contract checks; live reports optional |
| 3 | Stateful updates, actions, persistence, and conflict recovery | Implemented; deterministic recovery demonstrated |
| 4 | Content adapters, extensions, and embeddable AI setup | Implemented; experimental packages and deterministic demo |
| 5 | Gaming News, Staff Business, and Asset Workbench apps | Implemented; standalone synthetic workflows |
| 6 | Complete docs, document explorer, and demos | Implemented; compiled quickstart and deterministic explorer |
| 7 | Package verification and public developer preview | Candidate verification and release tooling implemented; publication gates open |

See [the implementation plan](docs/implementation-plan.md) for phase deliverables and acceptance gates. No release dates are committed.

Phase 1 evidence: [developer guide](docs/phase-1-developer-guide.md), [contract reference](specs/output-document/README.md), [authored fixtures](specs/fixtures/README.md), and [automated checks](tests/README.md). The three fixture workspaces share one renderer. Phase 2 adds the [connection guide](docs/phase-2-model-connections.md), [provider matrix](docs/provider-evaluation.md), and a runnable Gaming News starter. The complete standalone application workflows remain Phase 5 work.

First contributor opportunities: review the experimental output contract, author representative documents, improve setup instructions, and expand keyboard interaction guidance. Coordinate through an issue before adding public types.

Phase 3 evidence: [workspace and action guide](docs/phase-3-workspaces.md), [schema 0.2 action contract](specs/output-document/v0.2-actions.md), and [durable workspace decision](docs/architecture/0006-durable-workspaces.md). The Development-only recovery demo uses real SQLite files and shared-record version checks. Four phases followed Phase 3: **4 content/extensions/setup**, **5 the complete three apps**, **6 the complete docs and demo site**, and **7 developer preview**.

Phase 4 evidence: [completion record](docs/phase-4-completion.md), [content/setup guide](docs/phase-4-content-and-setup.md), [schema 0.3](specs/output-document/v0.3-content-and-extensions.md), [decision 0007](docs/architecture/0007-content-extensions-and-setup.md), and [automated checks](tests/README.md). Three phases followed Phase 4: **5 complete example apps**, **6 complete docs/demos**, and **7 developer preview**.

Later directions include additional renderers, content formats, providers, storage adapters, and supported hosting models. Arbitrary generated scripts and real-time collaborative editing require separate designs.

Phase 5 evidence: [completion record](docs/phase-5-completion.md), [standalone app guide](docs/phase-5-example-apps.md), [decision 0008](docs/architecture/0008-standalone-example-apps.md), and the [workflow test matrix](tests/README.md). Two phases followed Phase 5: **6 complete docs/demos** and **7 developer preview**. The examples' synthetic authentication is Development-only.

Phase 6 evidence: [completion record](docs/phase-6-completion.md), [learning guides](docs/site/start.md), [compiled quickstart](examples/Quickstart/README.md), [decision 0009](docs/architecture/0009-documentation-and-samples.md), and [test evidence](tests/README.md).

Phase 7 candidate evidence: [readiness record](docs/phase-7-readiness.md), [release/deployment guide](docs/site/releasing.md), [decision 0012](docs/architecture/0012-framework-verification-and-optional-model-reports.md), and [recorded provider attempts](verification/providers/README.md). Package publication requires verified framework/packages, a confirmed NuGet owner/policy and current hosting evidence. Live model reports are optional. This candidate is not a completed public package release.

The [public docs and fixture explorer](https://intentblazor.runasp.net/) are deployed on MonsterASP; the [hosting record](docs/phase-7-hosting.md) separates actual HTTPS/browser checks from package publication.
