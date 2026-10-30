# Validation projects

Run `pwsh -File scripts/Verify.ps1` from the repository root. It restores locked dependencies, builds the solution in Release, verifies formatting, runs the test project without rebuilding, checks documentation links, and packs the five source libraries locally.

`IntentBlazor.Tests` uses xUnit with conventional VSTest. No live model, credentials, external database server, or browser installation is required by the automated suite. Phase 3 tests create temporary SQLite files and remove their own test directories.

| Area | Evidence |
| --- | --- |
| Contract compatibility | All three fixtures round trip with separate host data; strict JSON and semantic validation reject unsupported content. |
| Boundaries | Exact UTF-8 size, tree depth, node/state/row counts, and numeric input limits test accepted and rejected boundaries. |
| Local interactions | Shared selection contract, numeric sorting, atomic assignments, record validation, hidden ancestors, draft protection, and explicit clearing. |
| Input orchestration | Configured processor, scoped sessions, busy guard, cancellation, safe failure messages, and stale response rejection. |
| Blazor output | Actual `HtmlRenderer` renders all three domains, responds to state changes, retains output on failure, encodes text, and blocks nonlocal images. |
| Drop and go | Default registration and accessible input controls; explicit session override; busy/cancel states retain the workspace. |

The playground is also checked manually in a native browser for selection, sorting, form editing, draft rejection and clearing, fixture input, invalid proposals, and responsive layout. These checks are distinct from automated HTML rendering tests. Live provider verification is explicitly deferred. The complete standalone business application remains Phase 5 work.

Phase 2 adds scripted HTTP-handler tests around the real OpenAI SDK adapters, Agent Framework data-tool invocation tests, bounded generation/correction/context/token checks, source scoping, and Codex CLI event/registration/argument tests. Tests never call a model server, create a key, run Codex inference, or require paid credentials. CLI process events are substitutes; runtime execution against a real CLI is part of deferred live evaluation.

Phase 3 adds the following behavioral evidence using the public contracts:

| Requirement | Evidence |
| --- | --- |
| Exact drafts, baselines, selections, sorting, artifacts, and conversation survive a new scope | `EditFollowupAndReconnect_RestoresExactDraftBaselineAndContext` for in-memory and SQLite stores. |
| A competing checkpoint cannot overwrite a private draft; it can be forked | `ConcurrentCheckpointConflict_RetainsDraftAndCanFork`. |
| Owners and current data permissions are rechecked | `OwnersAndReauthorization_PreventPrivateWorkspaceLeakage`, `ReceiptRead_RechecksOwnershipAfterAccessResolution`. |
| Late edits and identity changes cannot install an outdated restore | `InFlightRestoration_RejectsNewEditsAndChangedOwners`. |
| Unknown formats and inconsistent checkpoint state preserve current output | `MalformedOrIncompatibleCheckpoint_IsRejectedWithoutOverwritingLocalState`. |
| Bounded context retains original turns | `History_ContextIsBoundedWhileOriginalTurnsSurviveStorage`. |
| Actual business saves acknowledge fields only after a receipt; replay cannot repeat the write | `ApplicationSave_AcknowledgesActualResultAndPreventsReplay`. |
| Two staff share records without sharing their private drafts | `SharedRecordConflict_RetainsOtherStaffDraftAndPrivateWorkspaces`. |
| Permission, revoked record, forged, stale, and cancelled action requests cannot write | `DeniedInvalidAndCancelledRequests_DoNotMutateTheApplication`. |
| A committed-but-uncertain action retains its draft and is not retried | `UncertainSubmittedAction_NeverReexecutesOrClearsTheDraft`. |
| Buttons use trusted action labels and actual execution outcomes | `Renderer_UsesRegisteredActionLabelAndShowsConfirmedOutcome`. |

The Development-only `/workspaces` page was checked in native Edge for typed notes and owner edits, a follow-up request, new-session reconnect, browser reload, and an actual SQLite shared-record conflict retaining the exact draft. The demo's inspector was used to confirm committed session values separately from the input's visible DOM value. These browser checks use synthetic cookie identities; they do not certify production authentication or live model compatibility.

Use conventional VSTest-mode SDK projects initially. scripts/Verify.ps1 discovers projects explicitly declaring IsTestProject=true and runs each with dotnet test using no-build and no-restore. Changes to runner conventions require corresponding script and CI changes.

Provider tests requiring real credentials must be optional and separated from deterministic contributor checks.
