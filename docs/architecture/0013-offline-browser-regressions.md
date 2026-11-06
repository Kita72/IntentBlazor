# Offline browser regression verification

Status: implemented for foundation Step 3. No output schema, session API or required AiInput/AiOutput parameters change.

Revision, November 6, 2026: [decision 0014](0014-main-only-repository.md) removes GitHub CI. The local offline browser suite and its verification entry points remain; references below to CI describe the original implementation and historical evidence.

Use a separate non-packable .NET 10 xUnit/VSTest project with pinned Microsoft.Playwright. Run Chromium headless with GPU, WebGL and Vulkan disabled and test parallelism limited to one. The existing deterministic suite remains independent; browser checks exercise actual DOM events and Interactive Server circuits. Broader browser/platform certification is outside this initial matrix.

Start only owned dotnet fixture processes on dynamic loopback ports with private temporary data. Exercise the real Gaming News, Staff Business and Asset Workbench registrations and synthetic accounts. A separate Development-only browser host drives the default pair and actual AI pipeline with an in-process scripted client for rejected, incomplete, delayed, cancelled, timed-out, unavailable and over-budget responses. It requires an explicit fixture-enable flag. No local model executable, paid provider, secret lookup or remote business feed participates.

Use native clicks, keyboard and change events with server acknowledgment from rendered state/revisions. Do not simulate a successful UI test by mutating session state or assigning DOM values. Distinguish a surviving-circuit reconnect from a new circuit after reload and SQLite restoration after process restart. Drop an actual save reply on the browser's WebSocket route and use an independent authenticated observer to establish the committed write; reconnect and reload must not repeat it. The route forwards the real protocol without decoding it.

Build the quickstart outside the repository against packed libraries and exercise its public renderer from the browser. This preserves the two-component consumer contract beyond project-reference tests. Download actual image bytes, decode dimensions, retain identical originals, and deny another synthetic identity access to the private download.

Repository verification runs unit checks, packages and independent consumers, then browser checks and renderer budgets. Install only the pinned Chromium headless shell; CI explicitly installs Linux OS dependencies. Parse the actual TRX and reject zero, failed, skipped or incomplete execution. Retain failure-only trace/HTML/screenshot/owned-host logs and test reports as ignored artifacts and CI evidence. Traces contain synthetic fixture state and temporary test authentication, never production accounts, model credentials, private prompts or reasoning.

Keep public contracts unchanged when fixing browser-discovered defects. The initial regression exposed boolean ARIA rendering: selection controls now emit explicit true/false strings for aria-pressed in list and table renderers. Browser checks cover unselected and selected states.

Manual screen-reader/zoom review remains a maintainer or accessibility-contributor release task with the recorded environment and procedure in the keyboard guide. Browser assertions do not establish screen-reader behavior or WCAG conformance. Live provider reports remain optional under [decision 0012](0012-framework-verification-and-optional-model-reports.md); publication, deployment and .NET 11 compatibility remain separate work.
