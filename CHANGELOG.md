# Changelog

## Unreleased

- Correct NuGet upload acknowledgement handling for clients that use the same success message for `.nupkg` and `.snupkg`. Nine offline regression cases cover both uploads, errors, wrong/missing/reversed artifacts and duplicate acknowledgements; the follow-up unit/HTTP/rendering suite passes 493 cases. Published framework artifacts are unchanged.

## 0.1.0-preview.1 — 2026-11-07

- First public developer preview: eight packages and their symbols published under Wilson72, pinned to source `9c204d0f712e4cf355948f6405219db717e2fe73`. Public signatures, original payloads, MIT/repository metadata, SourceLink and owners verified. Fresh public-only quickstart, three example apps and production-host recipe passed. All six foundation steps are complete; see the [release record](docs/foundation-step6-developer-preview.md).
- Current MonsterASP Production fixture deployment verified over HTTPS with exact source/fingerprint, .NET 10.0.14, X64 and native request/selection/draft circuit evidence. Corrected the site's app-pool bitness and retained/restored the prior bundle. No model inference or GitHub automation was enabled.
- Foundation Step 6 local preview publishing and public-feed verification tooling: scoped current-user-encrypted NuGet credentials, readiness checks before credential decryption, immutable artifact/acceptance archives, repository-signature-aware package payload comparison and fresh public-only consumers. The framework APIs and offline model-test boundary are unchanged.

- Foundation Step 5 isolated SDK 11 checks, protocol-2 exact candidate/full-suite/artifact evidence, typed HTTPS/circuit observations and an offline-compilable host recorder. SDK 11 builds/tests net10; native net11 targeting records a pruning adjustment and new Blazor analyzer failure without changing the .NET 10 baseline. Host-specific win-x64 IIS publishing preserves portable packages/symbols, includes dependency notices, validates a hashed payload inventory, exercises actual PNG/SQLite natives and rehearses browser upgrade/rollback while preserving private/unmanaged data. No deployment, public-host call, inference or publication occurs.

- Foundation Step 4 production hosting guide and standalone application-owned recipe: trusted circuit claims/current server grants, private SQLite workspaces/artifacts, stable host catalog keys, authorized C# tools and transactional versioned/idempotent actions, persistent per-user request admission and streaming-safe concurrency controls. Offline tests cover owner isolation, protected cookies, revocation, durable history/drafts, saved-checkpoint compatibility and consistent stopped-host backups. Added independent package-only compilation/execution; default AiInput/AiOutput and public framework contracts are unchanged. No model connection, sign-in shortcut, deployment or publication is enabled by default.
- Solo maintainer repository workflow: develop and verify locally in Codex, then commit and push directly to `main`. Removed Dependabot configuration and GitHub CI/publishing workflows; dependency maintenance is deliberate. Repository-scoped bots/Actions and old branches are cleaned up under decision 0014. Package publication requires a separate explicit instruction.
- Foundation Step 3 serial Chromium browser regressions for default components, offline failure modes, actual circuit/process recovery, lost action replies, all three private example workflows, content/download bounds, keyboard/narrow output, AI setup and a package-only public renderer. Verification parses TRX and CI retains failure diagnostics; no local model or paid inference is used.
- List/table selection buttons now expose explicit `aria-pressed="true"`/`"false"` values in the interactive DOM. Public component parameters and document contracts are unchanged.
- Framework release scope revised: live provider/native-tool reports are optional compatibility observations, with missing, failed, stale or unreadable reports left unverified. Offline framework, runtime, package, account and hosting checks remain required; no live local/paid inference is needed for publication. Tory's local model installations are reserved for Tory's own testing.
- Foundation Step 2 provider generation fixes: local document-object delivery, native reads before presentation formatting, compact context, retained tool data during correction, and metadata-only validation observations.
- Opt-in OpenAI and isolated Codex evaluation paths with explicit paid-call gates; protocol 2 fixture and real application-service workflows plus optional native-tool observations. Paid live inference is not run by framework verification.

### Added

- Foundation Step 1 contributor guides for native keyboard/focus behavior, rating-display versus score-editing controls, and provider capability reporting with a clearly fictional example. The documentation catalog, output specification and contribution/issue routes link the guides; optional live evaluation remains separate from framework publication.

- `0.1.0-preview.1` packaging with eight package-specific readmes, MIT/repository metadata, portable symbols and exact-commit Source Link audits.
- Independent package-only quickstart and three-example runtime checks, repeatable renderer budgets, source-bound live evaluation reports and explicit release-readiness gates.
- Production docs health/version reporting, Development-only model/identity checks and an IIS bundle/deployment helper. The earlier tag-triggered NuGet OIDC workflow was removed under decision 0014; the first public publication used the selected local method. Live model verification remains optional.
- Keyboard-focusable chart data tables with captions and scoped column headers.
- Public MonsterASP docs/explorer deployment with observed HTTPS, Production fixture mode, a WebSocket circuit, draft-preserving follow-up, keyboard interaction and small-screen chart-table checks.

### Fixed

- Registered C# read tools can atomically refresh a dataset already preloaded or returned in the same request. Invalid replacements retain the prior authorized data; independent preload sources still reject duplicate IDs.

### Earlier implementation

- Phase 6 static SSR documentation library, searchable guides, complete node/binding/event and generated error references, provider/hosting/migration limits, and labelled demo hub.
- Interactive fixture document explorer with node/data/state inspection, structured validation, protected JSON drafts, and isolated sessions.
- Standalone two-component quickstart with compiled C# service/registration/renderer snippets embedded in the site; fresh package-only consumer verification.
- Maintainer-authorized direct commits and pushes after verification; external contributions retain the PR process.

- Phase 5 standalone Gaming News, Staff Business, and Asset Workbench apps with the default AiInput/AiOutput pair and optional existing model adapters.
- Synthetic Development-only protected-cookie identities, antiforgery-protected sign-in/out, private named workspace controls, and optional AI setup/inspection shared as non-packable example source.
- News preferences, safe linked source/date/version attribution, bounded host-configured JSON feeds, private bookmarks, and immutable saved comparison snapshots.
- Example-owned EF Core SQLite jobs, assignment permissions, explicit conflict review/rebase, shared operation history, and transactional idempotency receipts separate from framework checkpoints.
- An app-owned asset version-picker extension with live dimension groups, retained-original selection, actual sample imports, and working PNG exports through the existing pack.
- Phase 4 optional content, image-editor pack, and Blazor AI setup packages; AiInput/AiOutput retain their existing consumer surface.
- Bounded UTF-8 text/Markdown, JSON/CSV, PNG/JPEG, and PDF adapters; owner-scoped immutable artifacts, authenticated delivery, provenance, parent-aware cleanup, and actual JSON/CSV/image downloads.
- Experimental schema 0.3 Markdown/chart/gallery/artifact/extension nodes, public typed renderer/event registration, versioned capabilities, and safe optional fallbacks.
- A real nearest-neighbor resize action, recoverable original image, local zoom, and persisted comparison state.
- Host-approved model presets, policy-enforced scoped generation settings, explicit connectivity/presentation probes, and metadata-only run inspection.
- A deterministic private content demo, format/extension/setup guidance, and third-party dependency notices.
- Phase 3 owner-scoped checkpoints with replaceable in-memory and SQLite stores, exact drafts/baselines, selections, sorting, artifact references, and retained conversation records.
- Revision-checked follow-up reconciliation that preserves edited fields and rejects proposals that remove or hide them.
- Experimental schema 0.2 registered application actions, trusted button labels, server authorization, typed arguments, expected record versions, durable request claims, and structured outcomes.
- Extractive bounded conversation context, explicit checkpoint conflict/fork recovery, and fail-closed saved-format compatibility checks.
- Development-only SQLite recovery demo with protected synthetic browser identities, separate business records, permission simulation, and conflicting staff saves.
- Phase 2 optional IntentBlazor.AI package with Ollama, LM Studio, OpenAI, and experimental Codex CLI connections.
- Scoped C# data-tool attributes and explicit registration, Agent Framework orchestration, strict presentation envelopes, correction limits, cancellation, progress, and usage metadata.
- Optional model documentation page and an independently runnable Gaming News starter; both retain credential-free fixture defaults.
- Provider matrix documenting deferred live verification and the Codex adapter's narrower capabilities.
- IntentBlazor project identity and MIT license.
- Five-project .NET 10 solution with a Blazor documentation shell.
- Contributor, governance, conduct, security, architecture, and roadmap documents.
- Deterministic repository verification, CI, dependency management, and example-app directories.
- Experimental schema 0.1 output contracts with separate host datasets, strict JSON parsing, and semantic validation.
- AiInput and AiOutput with a shared scoped session and deterministic layout, card, text, list, table, rating, form, input, select, button, and image rendering.
- Local selection, typed sorting, atomic state effects, draft protection, revision checks, cancellable input processing, and bounded in-memory input history.
- Three authored fixture workspaces, original MIT sample sprites, and an interactive documentation playground with document/state inspection.
- Contract, runtime, and Blazor rendering tests plus the Phase 1 developer guide.

### Fixed

- Draft status no longer claims that no application data was saved when a successful action leaves other fields dirty.

No public framework package has been released. Live model verification is deferred. Phase 6 documentation-site work is implemented. Developer-preview release work remains in Phase 7.
