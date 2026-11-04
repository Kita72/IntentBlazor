# Offline browser regressions

Foundation Step 3 adds actual browser coverage while preserving default AiInput/AiOutput. The suite uses Chromium headless, .NET 10 and xUnit/VSTest. It runs serially with GPU/WebGL/Vulkan disabled, uses private loopback hosts and synthetic data, and never starts Ollama, LM Studio or paid inference. [Decision 0013](architecture/0013-offline-browser-regressions.md) records the boundaries.

## Run and diagnose

Run the full repository acceptance command:

~~~powershell
pwsh -File scripts/Verify.ps1
~~~

On a Linux host needing browser system dependencies, use pwsh -File scripts/Verify.ps1 -InstallBrowserDependencies. This explicit flag may require sudo; hosted CI includes it on both OS jobs, and it applies only on Linux. The pinned Playwright installer downloads only Chromium's headless shell and required support files. Package restore/browser installation requires network access; application scenarios use only offline fixtures and loopback traffic.

For browser-only iteration, pwsh -File scripts/Verify-Browser.ps1 builds the solution and freshly packed quickstart, installs the pinned headless browser and runs the suite. Its -NoBuild switch is for an already-built, freshly packed tree such as the full verification script. To isolate an existing case after preparing those prerequisites:

~~~powershell
dotnet test tests/IntentBlazor.BrowserTests/IntentBlazor.BrowserTests.csproj --no-build --no-restore -c Release --filter "FullyQualifiedName~Recovery_DistinguishesSurvivingCircuitFromReloadAndProcessRestore"
~~~

The suite creates only owned IntentBlazor-Browser-GUID temporary folders with independent workspace/business/artifact storage. It terminates only its own dotnet child processes and validates the temporary root before cleanup. It does not use personal browser profiles, model installations or the public MonsterASP site.

The actual artifacts/browser/browser.trx is parsed after successful execution: every discovered case must execute and pass, with no skips. The summary.json records counters and source revision. Failures retain a case-named subdirectory containing trace.zip, page.html, page.png, browser errors and owned-host logs. Open traces locally with the generated Playwright script's show-trace command. Artifacts contain synthetic UI/authentication and must not be repurposed for production accounts. CI retains them with candidate evidence.

The harness blocks unexpected external HTTP requests and fails on unhandled page errors. It uses roles/labels and existing renderer identities, condition-based browser assertions and explicit server acknowledgments. A failed request must retain accepted output and allow a later valid request; a DOM value alone is insufficient proof of a saved draft.

## Requirement coverage

| Requirement | Evidence |
| --- | --- |
| Default pair, service-backed selection/sort, accepted follow-up, exact protected edit and explicit selected ARIA state | DefaultPair_SelectsSortsAndPreservesCommittedDraftAndSelectionThroughFollowup. |
| Invalid/incomplete documents, unavailable provider, reported-token budget and timeout retain output/drafts and recover | FailedRequests_KeepAcceptedOutputAndDraftAndAllowRecovery, five separate offline cases. |
| Explicit cancellation ends busy state and permits a later successful request | CancelRequest_EndsBusyStatePreservesDraftAndAllowsNextRequest. |
| A response prepared before a newer committed edit is rejected and the focused field survives | LateResponse_RejectsOlderRevisionRetainsNewEditAndFocusedField. |
| WebSocket reconnect retains the same scoped instance; reload/process restart create new scopes and restore exact draft, selection and conversation | Recovery_DistinguishesSurvivingCircuitFromReloadAndProcessRestore. |
| Applied temperature reaches the next request; unsupported settings stay disabled; Tab order, chart table focus and narrow contained scrolling | SetupAndKeyboard_ApplyNextRequestAndExposeUnsupportedOptionsAndNarrowChartAlternative. |
| Source-attributed news selection, saved bookmark/comparison, reload and private identities | News_UsesSourceAttributionSavesLibraryAndKeepsEachIdentityPrivate. |
| Staff conflicts retain drafts; reviewed rebase succeeds; reader denial and identity switching retain private drafts without duplicate history | Staff_ConflictsRetainDraftsReviewedSaveSucceedsAndIdentityChangesCannotReplayOrLeakEdits. |
| A save commits while its browser reply is dropped; reconnect/reload do not repeat it | Staff_LostActionReplyReconnectsWithoutRepeatingTheCommittedBusinessWrite. |
| Real file input, malformed/oversized/excess-file rejection, resize, decoded PNG dimensions, unchanged original bytes, provenance and private download denial | Assets_ImportRejectBoundsResizeDownloadRealPngAndRetainPrivateOriginal. |
| A public renderer from packed libraries handles real selection/keyboard events and protects its score through follow-up | PackedQuickstart_PublicRendererHandlesRealEventsAndRetainsDraftThroughFollowup. |

## Manual accessibility and remaining work

The [keyboard/focus guide](contributing/keyboard-and-focus.md) remains the manual procedure for a maintainer or accessibility contributor. Before publication, record source revision, browser/OS, screen reader/version, viewport/zoom, reading order, pressed/sort/meter announcements, live errors/status announcements, focus visibility and failed recovery. Repeat the three synthetic walkthroughs and expanded chart alternative. Record failures as defects with reproducible steps. A passing browser suite is not a completed screen-reader review or WCAG certification; no full assistive-technology audit is claimed here.

Step 3 implements automated browser/recovery checks and a repeatable diagnostic/manual-review procedure. Production host integration, compatibility/evidence/operations and public package publication remain foundation Steps 4–6. The example apps and scripted client verify framework mechanisms without certifying a chosen model or production identity system.
