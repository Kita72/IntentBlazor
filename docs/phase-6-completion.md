# Phase 6 completion record

Status: implemented; experimental API. Phase 7 developer preview and the deferred live-model acceptance gate remain.

Scope: static SSR learning site, embedded source-controlled guides, complete output reference and error index, independent compiled quickstart, an isolated fixture explorer, demo navigation, provider/hosting limitations, and keyboard/small-screen checks. No package publication, deployment, or live API certification is part of this phase.

## Delivered

- `/docs` searches 23 embedded guides by title/topic. Static SSR articles serve the current guides, specifications, provider evidence, architecture index, contribution policy, security policy, and release notes without creating a circuit.
- Installation, the two-component developer journey, services through attributes/explicit registration, all 16 node kinds, all five binding sources, all four event kinds, actions, files/setup, typed extensions, ownership/persistence, concurrency, hosting, migration, and preview boundaries are documented. Verification checks a generated index of 109 literal framework error codes against source.
- `examples/Quickstart` runs independently on port 5184. The page uses default AiInput/AiOutput. Its authored processor, synthetic read service, optional provider registrations, and typed score renderer compile with the app; the docs embed those exact source files. A package-only copy restores/builds outside the repository using an empty package cache and mapped local/public sources.
- `/explorer` (`/playground` alias) provides three authored fixture workspaces, presentation editing, node tree/bindings, host data, state/history, revisions, and structured validation. It retains the last valid output and protects JSON/output drafts. Each explorer owns its session; documents cannot register actions, create authoritative datasets, or install renderer types. Controls stay disabled during prerendering until handlers are connected.
- `/demos` separates anonymous read-only fixtures, Development-only recovery/content demonstrations, optional configured model mode, and the three independently runnable apps. Model mode stays disabled by default, exposes request budgets, and refuses anonymous production activation.
- Maintainer-directed verified work may be committed/pushed directly under Tory's authorization. External contributors retain the PR workflow; repository protections/access are unchanged.

## Automated evidence

`pwsh -File scripts/Verify.ps1` checks locked restore, Release build, formatting, tests, documentation links/error-index drift, all eight library packages, and a fresh package-only quickstart build. The [test matrix](../tests/README.md) names the regression methods for guide resources/samples, safe links, reference completeness, explorer state/error transitions, service registration, renderer behavior, and prerendering. No live inference is performed.

The package-only consumer has no project references or docs/example-host dependency. Its isolated configuration maps IntentBlazor packages to this checkout's packed artifacts and third-party packages to nuget.org. The check does not publish or modify user-level NuGet settings.

## Browser evidence

Native Edge checks used the ordinary desktop viewport plus 390×844 and 320×844 responsive viewports; overrides were reset afterward.

- Guide search returned the renderer tutorial. Source snippets rendered as code. Keyboard article links retained the guide route and scrolled to the correct section. The skip link focused `main-content`; an unknown guide returned HTTP 404 with a useful heading and navigation.
- Invalid schema retained a selected news article and session revision 2. Pending JSON blocked a fixture switch until explicit restore. A keyboard-edited staff note remained dirty after a blocked switch. The table's named focusable region scrolled horizontally without widening the page.
- The explorer, guide article, demo hub, content/setup workspace, recovery page, and standalone quickstart had no page-wide horizontal overflow at the tested narrow sizes. Code/table blocks retained local scrolling. AiInput's grid/file-picker sizing was corrected in the library so consuming hosts receive the same behavior.
- The content demo created and selected an original 64×64 PNG by keyboard and exposed its preview/editor and unavailable model controls. No model was contacted. This phase did not re-exercise native file selection; the existing [Phase 4 browser picker limitation](phase-4-completion.md) remains recorded.
- The recovery demo checkpointed and reconnected to a new session, restoring the exact note `Phase 6 recovery draft` from SQLite at 320 pixels.
- The independent quickstart submitted an authored request, selected an item, changed the custom renderer score from 3 to 4 by keyboard, and displayed the dirty state and matching rating at 320 pixels.

Local proof images are under ignored `artifacts/phase6-proof` (`learning-site.png`, `explorer-mobile-draft.png`, `quickstart-mobile.png`). These are deterministic UI checks, not a complete assistive-technology audit or a production hosting/model certification. Phase 7 retains the final accessibility, performance, provider, packaging/release, and deployment gates.
