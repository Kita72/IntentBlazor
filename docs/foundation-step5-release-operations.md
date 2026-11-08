# Foundation Step 5: compatibility and release operations

Step 5 implements offline release/deployment checks while preserving default `<AiInput />` and `<AiOutput />`, the .NET 10 package baseline and the main-only source repository. The [decision](architecture/0016-candidate-evidence-and-isolated-compatibility.md) records the evidence and deployment boundaries. No provider inference, local model service, public-host request, deployment or publication is part of these checks.

The original Phases 0–6 are implemented and Phase 7 remains an unpublished `0.1.0-preview.1` candidate. **Foundation Steps 1–5 are implemented; one step remains:** Step 6 explicitly prepares/publishes the preview and verifies public-feed consumers. That step also needs a NuGet owner/publishing method and refreshed public hosting evidence for the intended source. Existing hosted docs are historical; a source push does not deploy this guide.

## Isolated .NET 11 observations

The November 7 check uses SDK **11.0.100-rc.3.26522.148** from Microsoft's [release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json), with its archive SHA-512 verified before extraction. SDK 11 and copied .NET 10.0.14 shared runtimes live under ignored `artifacts/compatibility/net11-sdk`. The working repository still selects SDK **10.0.403**, targets `net10.0`, and runs on .NET **10.0.14**. No global SDK installation or baseline retarget occurred.

| Isolated case on Windows 10.0.26300 / x64 | Observed result |
| --- | --- |
| SDK 11, target `net10.0`, execute .NET 10.0.14 | Locked restore, complete solution build, 476 offline tests, eight local packs and all five independent package consumers pass. |
| SDK 11, scratch target `net11.0` | Initial restore reports NU1510 for Runtime's DI abstractions reference, now supplied by net11. After removing that reference only in the copy and refreshing its locks, build stops at BL0013 in `BlazorWorkspaceIdentity`. |
| Runtime 11.0.0-rc.3.26522.148 tests and consumers | Not executed because the net11 build failed. Installing the runtime does not prove compatibility. |

The [shared-framework change](https://learn.microsoft.com/en-us/dotnet/core/compatibility/extensions/11/extensions-in-shared-framework) explains NU1510. [ASP.NET Core's analyzer release notes](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview7/aspnetcore.md) describe BL0013 for authentication-state reads without a change subscription. The current identity service reads the provider on each call; a future net11 migration must review the analyzer's applicability and all affected authentication consumers, then pass the full retargeted suite. This task records the failure without changing authentication behavior or suppressing the analyzer. **Native net11 support is unverified and is not advertised.** This is a recorded compatibility limitation, outside the .NET 10 preview's acceptance.

To repeat after installing an isolated SDK and the required .NET 10 runtime in that installation:

~~~powershell
pwsh -File scripts/Verify-Net11.ps1 -SdkDirectory artifacts/compatibility/net11-sdk -SdkVersion 11.0.100-rc.3.26522.148
~~~

The script clones `main` into unique ignored copies, overlays current reviewed source, pins the exact isolated SDK and sets process-only runtime resolution. Only the net11 copy is retargeted/adjusted. It records candidate identity, actual SDK/runtime/OS, counts, consumers, failed stages and command logs under `artifacts/compatibility/run-*/`. A failed retarget makes the command exit nonzero and leaves its dependent checks unverified. It restores process environment and verifies the root SDK file is unchanged. This optional probe is separate from baseline `Verify.ps1`; it is not a complete runtime/platform support matrix. No .NET 11 RTM or Linux result is claimed.

## Evidence for one exact candidate

`pwsh -File scripts/Verify.ps1` performs the required baseline sequence and creates `artifacts/phase7/evidence/repository.json`. The receipt is invalidated before any check starts; it is completed only after locked restore, Release build/format/docs checks, passing unit/browser TRX, eight packs/audits, the quickstart/three examples/production-host package consumers, native IIS artifact checks, browser upgrade/rollback and offline compilation of the public-host recorder.

Protocol 2 fingerprints executable evaluator/tool code, app services, scripts, tests, guides, configuration, fixtures/binary assets and license inputs alongside the libraries. Reports bind version, exact revision, fingerprint/protocol, clean-state and timestamp. Package audits include SHA-256 for both `.nupkg` and `.snupkg`. Readiness rehashes those files, receipt TRX and the actual sealed bundle. Current code with another commit, modified docs/config/tooling, dirty source, old reports, altered artifacts, missing checks or malformed evidence cannot establish readiness. Reports expire after seven days; future timestamps have a five-minute tolerance.

~~~powershell
pwsh -File scripts/Verify.ps1
pwsh -File scripts/Verify-Readiness.ps1 -RequireReady
~~~

The second command is expected to fail until the NuGet owner/method and current public-host gates are satisfied. There are six mandatory gates: clean candidate source, completed repository verification, runtime budgets, package metadata/artifact hashes, NuGet policy and public host. Optional model observations never affect `ready`. The former `INTENT_PUBLIC_HOST_VERIFIED` variable has no effect. Freeze/commit the candidate, rerun the full sequence from that clean commit, and keep exact artifacts before any later publishing/login operation. Worktree evidence gathered before a commit remains useful development evidence but cannot be promoted to the new commit's release evidence.

The full IIS release receipt requires Windows x64. On other hosts the script runs portable unit/package/browser checks, filters out the Windows-specific published-bundle case and leaves the IIS/repository-release gate unverified. This Step 5 records Windows results only; it does not relabel portable checks as native IIS acceptance.

## Lean IIS artifact and native checks

`Publish-Docs.ps1` creates a unique, sealed `net10.0/win-x64`, framework-dependent Production fixture bundle. RID selection stays at the docs host boundary; its library references retain their portable restore graphs. Separate publish intermediates leave package outputs intact. Nothing is trimmed. Linux/macOS/x86/ARM runtime payloads are absent and only recognized `libSkiaSharp.pdb` native debug symbols are removed. Managed portable symbols are retained. The initial optimized payload was approximately **32.5 MiB**, compared with the earlier **516.9 MiB** portable publish; copied dependency notices add a small amount to the final inventory.

The publisher executes the actual output with `--verify-offline-publish`: Skia encodes/decodes a PNG and SQLite opens an in-memory database and returns its native version. This checks the deployed native dependencies, not synthetic replacements. The manifest validates the x64 PE header, framework target, in-process Production IIS configuration and every file's size/hash. It records the complete payload inventory and aggregate hash. Any later modification, missing file or extra file invalidates the seal.

The November 7 NuGet advisory query over all 20 baseline projects, including transitive packages, reported no affected packages. This is a dated advisory result, not a guarantee of absence of vulnerabilities. Existing dependency versions remain pinned. `Export-DependencyNotices.ps1` reads the exact restored docs graph, records package license metadata and copies the packages' bundled licenses/notices into the artifact, including Skia's third-party notices. Keep these files when distributing the host. Framework packages keep their MIT metadata and original portable symbol/provenance audits; dependent packages retain their own terms.

## Upgrade and rollback without losing host files

`deployment-manifest.json` format 2 owns a precise set of relative files. The planner validates the incoming bundle and predecessor inventory, refuses changed managed files, case-only renames, directory/file collisions, private payloads and linked/traversing paths. It deletes only unchanged obsolete files from the prior managed inventory and preserves everything unmanaged, including private databases/configuration. Missing predecessor ownership never permits deletion of unknown files.

~~~powershell
$tool = 'tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll'
$bundle = Get-Content artifacts/phase7-docs-path.txt
dotnet $tool bundle-verify $bundle
dotnet $tool deployment-plan $bundle C:\private\inspected-site-mirror artifacts/step5/deployment-plan.json
~~~

The `deployment-rehearsal` command applies a plan only to a local directory containing `.intentblazor-offline-fixture`. It is a test tool; do not add that marker to a real site. The browser regression copies the actual published app into old/new sealed fixture bundles, stops it before each replacement, upgrades and rolls back, and opens each version in Production mode on loopback. It verifies health identity, a selected staff record, a committed note edit and a follow-up through the same two components. It checks obsolete managed file removal and private/unmanaged preservation after rollback. The evidence remains labelled `offline-loopback-kestrel-not-iis` and cannot satisfy the public-host gate.

Keep the complete previous sealed bundle off the server before any later deployment. `Deploy-Docs.ps1 -WhatIf` validates all payload bytes without opening the publish profile or starting MSDeploy. The real helper retains `DoNotDeleteRule`: it does **not** prune remote files from an uninspected host. A later remote cleanup requires an inspected remote inventory and an explicit deployment task. Rollback uploads the validated prior bundle under the same checks; the local rehearsal does not certify a live Web Deploy/IIS rollback, app-pool architecture or public TLS/WebSockets.

Historical format-1 portable archives are retained as historical artifacts; the new helper rejects them. Before the first format-2 deployment, explicitly prepare and review rollback for the existing legacy host. Do not edit an old manifest to call it format 2 or claim that this two-format-2 local rehearsal validated a legacy remote rollback. Subsequent format-2 rollbacks can use the retained, validated predecessor bundle directly.

## Current public hosting remains a separate gate

The shared `DocsHostSmoke` routine checks `/health/ready` version/revision/fingerprint, mode, hosting, process architecture and runtime, then performs actual browser events and waits for server revision changes. The publisher embeds the source fingerprint in the health response. The local test uses this routine, but its loopback observation is explicitly rejected as public hosting evidence.

After a **separately requested deployment and public verification**, the following records current-candidate HTTPS and circuit evidence:

~~~powershell
pwsh -File scripts/Record-HostEvidence.ps1 -AllowPublicHost
~~~

It requires clean source, validates certificates, uses serial headless Chromium with GPU/WebGL/Vulkan disabled, restricts requests to the expected origin, and emits `host.json` only after matching health, selection/edit and draft-preserving follow-up. Missing or mismatched observations cannot pass. `-PrepareOnly`, run by baseline verification, compiles the recorder without contacting the host. This Step 5 task uses only that offline preparation and loopback fixture checks. No current-candidate public-host or remote rollback success is claimed.

## Behavioral evidence

| Requirement | Evidence |
| --- | --- |
| Broader source identity with private/output exclusions | `Fingerprint_BindsEvaluatorServicesConfigurationAndBinaryAssets`, `Fingerprint_IgnoresOutputsAndPrivateLocalMaterial` |
| Exact candidate, time bounds, clean state and explicit hosting | `Readiness_RejectsEvidenceFromAnotherCandidateOrTime`, `Readiness_RequiresCleanSourceAndExplicitHostObservation`, `HostObservation_RejectsUnmatchedHealthOrIncompleteCircuit` |
| Stale success, changed packages and forged report fields fail | `FailedReadinessCommand_ReplacesOldSuccessWithIncompleteEvidence`, `PackageEvidence_RehashesBothPackagesAndSymbolsBeforeAcceptingReport`, `EvidenceReader_RejectsDuplicatePropertiesAtEveryDepth`, `Readiness_EnvironmentBooleanCannotVerifyThePublicHost` |
| Complete receipt and actual artifact hashes | `RepositoryEvidence_RejectsFailedSkippedEmptyAndIncompleteTrx`, `RepositoryEvidence_RehashesTrxAndSealedBundleAndRejectsMissingChecks` |
| Safe payload, native-symbol handling and tamper rejection | `Manifest_RejectsUnsafeWindowsManagedPaths`, `Manifest_RejectsPrivateFilesEvenWithValidHashes`, `BundleValidation_RejectsChangedIncompleteOrAmbiguousPayload`, `Seal_RemovesOnlyRecognizedNativeSymbolsAndRetainsPortableSymbols` |
| Managed upgrade/rollback retains private data; conflicts cannot partially apply | `Rehearsal_UpgradesAndRollsBackOnlyUnchangedManagedFilesPreservingPrivateData`, `Rehearsal_RejectsConflictsBeforeAnyWriteOrDelete`, `Rehearsal_RequiresOwnedMarkerAndNonOverlappingRoots` |
| Dry-run avoids credentials and public work remains opt-in | `DeployWhatIf_ValidatesBundleWithoutReadingCredentialsOrStartingWebDeploy`, `DeployWhatIf_RejectsTamperingBeforeOpeningTheProfile`, `HostRecorder_RequiresExplicitOptInBeforeAnyNetworkOrBrowserWork` |
| Actual published health/circuit after upgrade and rollback | `IisBundle_HealthCircuitAndRollbackPreserveUnmanagedData` |

The full baseline run contains 476 passing unit/HTTP/rendering cases and 16 passing serial browser cases, with no failures/skips. Full-suite, native output, manifest and compatibility reports remain under ignored artifacts; source contains the reproducible checks and dated outcomes. Existing recovery/authorization/content tests and all package-only consumers still exercise default AiInput/AiOutput. Complete manual assistive-technology review, real identity-provider integration, current public hosting, NuGet publication and a future net11 migration retain their explicit boundaries.
