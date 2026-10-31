# Validation projects

Run `pwsh -File scripts/Verify.ps1` from the repository root. It restores locked dependencies, builds the solution in Release, verifies formatting, runs the test project without rebuilding, checks documentation links, and packs the eight source libraries locally.

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

Phase 4 adds the following behavior evidence. Files and parsed data in these tests are synthetic; SDK request tests use scripted HTTP handlers.

| Requirement | Evidence |
| --- | --- |
| All declared formats retain their exact original bytes and expose real extracted metadata | `SupportedFormats_PreserveOriginalBytesAndExtractBoundedMetadata`. |
| Malformed content and excessive byte/pixel/page/row inputs fail without an artifact | `MalformedAndMismatchedContent_IsRejectedWithoutAnArtifact`, `StreamLimits_CheckActualBytesAndCancellation_WhileTextPreviewIsBounded`, `ProcessingLimits_RejectExcessivePixelsPagesAndRows`. |
| Large structured previews fall back without losing the original | `OversizedStructuredPreview_RetainsTheOriginalAndBoundedTextWithoutAnInvalidDataset`. |
| Owner isolation, immutable content, reopened storage, quotas and parent-aware cleanup | `Artifacts_AreOwnerScopedImmutableAndReadableAfterReopening`, `StorageQuotas_RejectAdditionalBytesAndCleanupRetainsDerivedParents`. |
| Authorized downloads return actual bytes with safe delivery headers | `Downloads_RequireIdentityAndReturnAuthorizedExactBytesWithSafeHeaders`. |
| Dataset exports produce real JSON/CSV and avoid spreadsheet formula execution | `DatasetExports_ProduceValidJsonAndCsvWithoutSpreadsheetFormulaExecution`. |
| A separately registered renderer handles typed protected local events | `RegisteredRenderer_UsesTypedConfigurationAndRevisionCheckedProtectedEvents`. |
| Missing optional extensions render encoded fallback; required packs fail closed | `MissingExtension_UsesEncodedOptionalFallbackOrRejectsRequiredVersion`. |
| An image action creates a real PNG with recoverable original and resumable comparison state | `ImagePack_CreatesAnActualDerivedPngAndRetainsOriginalAcrossRecovery`. |
| Content nodes and uploads use the existing component pair | `NewNodes_RenderGalleryChartMarkdownAndAuthorizedArtifactThroughAiOutput`, `DefaultAiInput_DiscoversUploadsAndShowsOwnedFileNamesWithoutNewParameters`. |
| Model context uses only authorized bounded attachment content | `ModelContext_IncludesOwnedBoundedAttachmentContentAndRejectsAnotherOwner`. |
| Applied setup values affect the next scoped generation and are guarded by current server policy | `AppliedProfileAndOptions_AffectTheNextGenerationAndRemainScoped`, `SettingsGuards_RejectUnauthorizedStaleUnsupportedAndExcessiveChanges`, `PermissionRevocationDuringApply_PreventsInstallingTheSettings`, `PolicyReductionAfterApply_RejectsTheNextGenerationBeforeContactingTheModel`. |
| Unsupported controls are visible and actual SDK request options/preset transports are verified | `UnsupportedAdapterOptions_AreRejectedAndShownInTheSetupUi`, `ProviderSpecificReasoningAndSampling_ReachTheRealSdkRequestBody`, `NamedProviderPreset_UsesItsOwnHostConfiguredTransportWithoutResolvingDefaultCredentials`. |
| Explicit probes separate connection from presentation, avoid tools, and enforce current policy | `ExplicitProbe_DistinguishesConnectionFromValidPresentationAndNeverInvokesTools`, `Probe_RechecksRevokedPermissionBeforeSendingAndSanitizesFailures`, `ProbeConnectionRevokedDuringResponse_DoesNotPublishTheCapabilityResult`. |

Rendering regressions also reject literal uncompiled `@onclick`, `@onchange`, and `@bind` attributes in the new Razor libraries. Browser checks of actual event handling, the browser file-selection limitation, and the package-only consumer are recorded separately in the [Phase 4 completion record](../docs/phase-4-completion.md). None of these checks certify a real provider/model profile.

Phase 5 adds app-owned acceptance scenarios, with real temporary SQLite/EF databases, actual image bytes, default component rendering, and an in-process ASP.NET Core TestServer. No external feed/model is called by these tests.

| Requirement | Evidence |
| --- | --- |
| News preferences, comparisons, bookmarks, attribution, follow-up selection and reopen; private libraries remain isolated | `PreferencesComparisonBookmarksAndSources_SurviveFollowupAndReopenWithoutAnotherOwnersLibrary`. |
| Changed articles and forged owners cannot save; library quotas and request payloads are enforced | `ChangedArticleOrForgedOwner_CannotCommitAComparisonOrBookmark`, `PrivateNewsLibrary_EnforcesQuotaAndIdempotentRequestPayloads`. |
| Configured feeds use the exact endpoint and reject unsafe, duplicate, unknown, oversized and cancelled content | `ConfiguredNewsFeed_RejectsInvalidRecordsBeforeTheyEnterTheWorkspace`, `ConfiguredNewsFeed_ReadsTheExactEndpointAndEnforcesActualStreamBytesAndUnknownMembers`. |
| Two staff can share records without losing a private draft; a conflict requires explicit reviewed rebasing | `TwoStaff_SaveConflictsRetainPrivateDraftsAndRequireAnExplicitReviewedRebase`. |
| A reader or forged owner cannot write through generated UI or direct service calls | `ReaderCannotSaveFromGeneratedUiOrDirectHandler_AndForgedOwnersCannotWrite`. |
| EF-backed receipts prevent repeat writes; invalid versions/values have no side effects | `ApplicationReceipt_PreventsARepeatedWriteAndRejectsChangedPayloadAfterReopeningContexts`, `DirectBusinessSave_RejectsInvalidValuesWithoutChangingRecords`. |
| Assets import real files, report updated dimensions, create real resized PNGs, recover originals, select versions and reopen privately | `ImportsDimensionGroupsResizeVersionSelectionOriginalRecoveryAndPngExport_UsePublicComponents`. |
| All three apps use default AiInput/AiOutput without credentials; named copies preserve private values | `EachStandaloneScenario_UsesDefaultAiInputAndAiOutputWithCredentialFreeFixtureMode`, `WorkspaceControls_ForkAndReopenRetainPrivateValuesAndDenyAnotherOwner`. |
| Optional model mode uses each app's authorized source and does not infer during initialization | `OptionalModelMode_UsesTheAppsAuthorizedSourceAndDefersInferenceUntilSubmission` uses the real SDK with scripted HTTP. |
| An unavailable initial source leaves a usable input and permits recovery without exposing private details | `UnavailableInitialSource_LeavesTheInputUsableAndCanRecoverWithoutLeakingDetails`. |
| Synthetic identities are server-allowlisted; HTTP sign-in/out require antiforgery and cannot escalate a role | `SyntheticIdentities_AreServerIssuedAllowlistedAndIsolatedByApp`, `SyntheticSignInAndSignOut_RequireAntiforgeryAndIssueOnlyAllowlistedRoles`. |

Native browser checks exercise the standalone scenarios separately from these deterministic tests. Live provider compatibility, production identity, and other hosting modes remain unverified.

Phase 6 adds source-backed documentation and a credential-free consumer. These checks use the real parser, validator, sessions, components, and public registration APIs; none sends a live provider request.

| Requirement | Evidence |
| --- | --- |
| Every guide/snippet resolves; samples render as code and links cannot execute unsafe schemes | `Articles_ResolveEmbeddedResourcesAndCompiledSamples`, `Links_MapGuidesAndSourceWhileRejectingUnsafeSchemes`, `Markdown_EncodesHtmlAndPreservesNavigableHeadingsAndCode`. |
| Node/binding/event references track the implemented enums; guide search handles matches and empty results | `Reference_CoversEverySupportedNodeBindingAndEvent`, `Search_ReturnsMatchingTopicsAndAnEmptyResultForUnknownTopics`. |
| All fixtures expose their own host data and inspectable trees; invalid proposals retain output/state/revision and editable JSON | `Fixtures_RenderWithOnlyTheirHostDataAndInspectableTrees`, `InvalidProposals_RetainOutputDataStateRevisionAndEditableJson`. |
| JSON drafts survive local events and block fixture/input replacement; proposals cannot create host datasets | `JsonDraft_SurvivesLocalEventsAndBlocksFixtureReplacementUntilExplicitRestore`, `JsonDraft_BlocksInputCommandsAndValidPresentationCannotIntroduceHostData`. |
| Output drafts remain intact; accepted edits stay isolated and a successful new document clears stale editor notices | `OutputDraft_SurvivesRenderAndFixtureSwitchAttempts`, `ValidEdits_RenderWithoutSharingStateAcrossExplorers`. |
| Prerendering cannot expose unwired controls; table scroll regions are named and keyboard focusable | `PrerenderedExplorer_DisablesControlsUntilInteractiveHandlersAreReady`, `Table_ExposesAFocusableNamedScrollRegion`. |
| Default components consume an independent app service and renderer; attribute/explicit tools are equivalent | `DefaultPair_RendersAuthoredDataAndRegisteredInteractiveRendererWithoutAModel`, `Service_RegistersTheSameReadToolThroughAttributesOrExplicitMethods`. |
| Provider registration does not infer eagerly; typed renderer events enforce bounds, configuration, draft tracking and revisions | `ProviderConfiguration_SelectsTheAdapterWithoutMakingALiveCall`, `Renderer_ClampsBoundsRejectsMalformedAndStaleEventsAndRetainsDrafts`, `Renderer_RejectsConfigurationWithoutItsDeclaredNumericRange`. |

`scripts/Verify-Quickstart.ps1`, called after packing by repository verification, restores/builds a copied consumer outside the source tree with package references, an empty package cache, and explicit package source mapping. `scripts/Export-ErrorReference.ps1 -Verify` checks source/error-index drift. Keyboard and narrow-screen observations are recorded separately in [Phase 6 completion](../docs/phase-6-completion.md); they do not imply live model or comprehensive accessibility certification.
