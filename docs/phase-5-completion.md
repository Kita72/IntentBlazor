# Phase 5 completion record

Recorded October 30, 2026. Phase 5 implements the three standalone experimental Interactive Server examples. Phase 6 has not started; no package/site has been publicly released. Live-provider evaluation remains the separately deferred Phase 2 gate.

## Delivered

| App | Concrete workflow |
| --- | --- |
| Gaming News and Tips | Game preference, dated/attributed articles and version-aware tips, contextual comparisons, private bookmarks, immutable saved comparison snapshots, and reopened workspaces. Optional fixed-endpoint HTTPS JSON source. |
| Staff Business | Synthetic authenticated dispatchers/readers, real EF Core SQLite assignments and operation history, private drafts, shared record-version conflicts, explicit reviewed rebasing, and transactional application receipts. |
| Asset Workbench | Actual private imports, dimension inspection, typed version-picker extension, real resize through the public image pack, retained-original recovery, local zoom, working-version selection, and PNG download/reopen. |

Each Home page uses default AiInput and AiOutput. The app-owned non-packable ExampleHost shares synthetic identity setup, private workspace controls, optional model registration/setup, and styling. Business services contain no provider SDK calls. Public document contracts did not change. AiOutput's draft wording was corrected so a partial successful save no longer claims that no application data was saved.

Each app has its own [news](../examples/GamingNews/README.md), [staff](../examples/StaffBusiness/README.md), or [asset](../examples/AssetWorkbench/README.md) run guide. See the [integration guide](phase-5-example-apps.md), [decision 0008](architecture/0008-standalone-example-apps.md), and [test matrix](../tests/README.md).

## Deterministic evidence

- Full repository verification passed: locked restore, Release build, formatting, documentation links, and eight locally packed source libraries. **Passed: 251, Failed: 0, Skipped: 0.** The 27 new cases include the three app acceptance scenarios, feed validation, library quota/request checks, EF conflicts/receipts/permissions, real image bytes, named workspace privacy, and HTTP antiforgery/role enforcement.
- A non-incremental solution Release build passed with zero warnings/errors. Tests use real temporary SQLite/EF databases, SkiaSharp decoding/pixel checks, actual HtmlRenderer output, and an in-process ASP.NET Core TestServer.
- All three optional model paths were exercised through the existing real SDK with scripted HTTP responses: initialization performed no inference, submitted requests included the app's authorized hostData, and validated output reached the session. No live model or feed endpoint was contacted.
- Copies of all three apps and the shared host were built outside the checkout with an isolated NuGet cache and package references replacing every framework source reference. All four projects passed with zero warnings/errors. This check found a Staff Business prerelease dependency restore issue; making its direct Blazor dependency explicit fixed the isolated restore/build.
- An unavailable initial news source leaves a usable input and bounded retry guidance. It does not substitute synthetic facts or expose the upstream exception details.

## Native Edge evidence

The standalone hosts were run separately on localhost ports 5181, 5182, and 5183, in credential-free deterministic mode with synthetic identities.

News: selected Emberfall, chose its 1.4.2 article and 1.4.1 comparison, submitted a contextual follow-up, bookmarked and saved through actual registered actions, selected the saved comparison, saved the workspace, and reloaded. The selected snapshot retained both original sources, dates, versions, and tips.

Staff: used separate Alex/Sam circuits with independent scoped principals. Both selected job-101 before saving. Alex saved version 2; Sam's save reported a real EF record conflict while retaining the exact text `Sam draft survives conflict.`. Reconnect loaded Alex's saved text/current version 2 while preserving Sam's draft and expected version 1. Explicit review/rebase followed by Save committed version 3 and shared history. Riley's generated-UI Save was rejected. The actual Switch identity form completed its antiforgery-protected sign-out navigation. Separate browser profiles are recommended for ordinary simultaneous-user evaluation because tabs share cookies.

Assets: imported two actual procedural PNGs of 64×64 and 48×32, inspected inconsistent dimensions, selected gold-tile, created a 16×24 derived PNG, selected that version, compared its retained 64×64 original, used the original as working version, reselected the derived version, downloaded its actual bytes, and saved/reloaded the working references. Automated rendering additionally verifies that the new dimension group appears immediately after resize.

Screenshots, downloaded-file evidence, package-copy scripts, and local logs are under ignored artifacts/phase5-proof. The native asset import used the real sample-import service; this run does not claim a retest of operating-system file selection through the browser extension. Its previously documented file-URL permission limitation remains separate from the passing streamed-import tests.

## Scope and remaining gates

Synthetic accounts are Development-only. Hosts own production identity, revalidation/revocation, tenant policy, source-content rights, backups, and retention. Staff uses EnsureCreated for its isolated synthetic EF database; a production migration history is not supplied. Checkpoints are separate from domain storage. Local file/SQLite adapters target single-instance hosts.

Optional model adapters/configuration are available; compatibility with real Ollama/LM Studio/OpenAI/Codex profiles remains unverified. Images contribute metadata/text to this phase's model context; chat configuration does not imply vision or image generation. No WebAssembly/Hybrid, distributed-store, production-account, or public-preview certification is claimed.

Two planned phases remain: **6 complete docs/demos** and **7 developer preview**. The deferred live-model gate must pass before the public preview. Maintainer review/merging follows [CONTRIBUTING](../CONTRIBUTING.md).
