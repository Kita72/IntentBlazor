# .NET 11 migration plan

Status: **proposed for Tory's review, November 10, 2026 (America/Edmonton)**. This is a new upgrade track after the completed developer-preview foundation. Preparing this plan does not retarget, install an SDK, publish packages or deploy the site.

## Recommended target

Move all 20 projects to a single **`net11.0`** baseline, use the stable **11.0.100 SDK / 11.0.0 runtime** initially, and retain the default `<AiInput />` / `<AiOutput />` experience. Microsoft's [download information](https://dotnet.microsoft.com/en-us/download/dotnet/11.0) confirms those RTM versions and C# 15 support. Select the latest appropriate stable servicing release when implementation begins and record its exact version.

Propose **`0.2.0-preview.1`** for the next eight-package release. Raising the minimum framework is a consumer-facing compatibility change even when C# signatures stay the same. The existing .NET 10 `0.1.0-preview.1` packages and source `9c204d0f712e4cf355948f6405219db717e2fe73` remain immutable and installable. A .NET 10 app would keep that earlier version or upgrade its host to use the new package version.

This recommendation keeps one code line and one tested runtime baseline for our solo-maintainer workflow. Supporting new releases on both `net10.0` and `net11.0` is an alternative to decide before step 2: it requires both package asset groups, conditional dependencies, compatible public APIs and separate runtime/consumer acceptance. It is not included in the default estimate of scope. .NET 11 is STS; Microsoft's current [support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) lists both .NET 10 and .NET 11 as supported through November 14, 2028.

Record the accepted baseline/versioning decision in a new architecture decision when implementation starts. Keep MIT licensing, document schemas 0.1/0.2/0.3, saved-workspace ownership, public component parameters and C# capability/renderer contracts intact. Existing attribute and explicit capability registration remain supported.

## What the review established

| Area | Current observation, November 10 |
| --- | --- |
| Working source | `main` at `667b3241a5ba00e56f4d9d462c03e28188e1036a`, clean before this planning change. |
| SDK | `global.json` pins 10.0.403 with `latestPatch` and prerelease disabled. System SDK inventory has no .NET 11 RTM SDK. |
| Projects | All 20 explicitly target `net10.0`: eight public libraries, one docs app, seven example/shared projects, three test/browser projects and one verification tool. |
| Prior trial | [Step 5](foundation-step5-release-operations.md) used SDK 11 RC3. SDK 11/net10 passed; scratch net11 restore required a DI-package pruning adjustment, then build failed at BL0013. Runtime-11 tests and consumers never ran. |
| Last recorded tests | The November 7 follow-up recorded 493 unit/HTTP/rendering cases; the frozen release recorded 16 browser cases. Those are .NET 10 results, not new .NET 11 observations. |
| Public site | Read-only `/health/ready` returned source `667b3241...`, .NET 10.0.14, X64, fixture and InteractiveServer. No site configuration was changed for this review. |
| Release checks | Scripts and C# verification code contain explicit `net10.0` paths, assembly/PDB expectations and .NET 10 runtime gates. Retargeting project files alone would leave verification incomplete. |

The migration guidance installed on this machine covers early previews. The RTM [ASP.NET Core migration guide](https://learn.microsoft.com/en-us/aspnet/core/migration/100-to-110?view=aspnetcore-11.0) and current [.NET breaking-change index](https://learn.microsoft.com/en-us/dotnet/core/compatibility/11) take precedence over its outdated release-status assumptions.

## Implementation steps

| Step | Work | Exit gate |
| --- | --- | --- |
| 1. Prove the GA toolchain in isolation | Install a verified official RTM SDK into an ignored local directory. Use disposable source copies for the first trial, preserving the working SDK and published archives. Record actual SDK, TFM, runtime, OS and architecture for each case. | Separate SDK-11/net10 and SDK-11/net11 observations; diagnose every restore/build failure before adopting the new baseline. |
| 2. Retarget and update required dependencies | Update `global.json`, all 20 project TFMs, the new package version, central versions and reviewed lock files. Resolve shared-framework pruning. Keep current test-runner and AI connection abstractions. | Stable-SDK locked restore succeeds with a reviewed dependency graph; no RC runtime or suppressed pruning warning is used to manufacture success. |
| 3. Resolve compiler, Blazor, identity and persistence behavior | Reproduce RTM diagnostics, fix relevant consumers, exercise saved workspaces/business records and check native image/SQLite operations. | Warning-free Release build and passing focused behavior checks, including identity changes and recovery. |
| 4. Upgrade verification and package acceptance | Update framework-aware tool paths, package/PDB auditing, release/runtime evidence, consumer copies, IIS manifests and rollback handling. Run the complete offline suite on runtime 11. | Full verification passes for the intended source, eight net11 package/symbol pairs, five fresh local-package apps and the actual published native payload. |
| 5. Prepare the production host and rollback | Confirm the exact site has a stable .NET 11 ASP.NET Core runtime/IIS support, retain x64/InProcess, preserve the current net10 rollback payload and verify upgrade/rollback locally. | A sealed net11 bundle and tested recovery procedure, plus host-specific RTM availability evidence. |
| 6. Freeze and release when explicitly requested | Freeze a clean candidate, re-run its required checks, update/verify the live docs host and publish the new version using a fresh scoped NuGet credential. Verify public downloads and consumers. | All six publication gates pass for that exact candidate; signed public artifacts and five public-only apps pass. |

These are implementation checkpoints. Work stays on `main`; use ignored scratch copies for exploratory failures and push completed, appropriately verified changes. Do not create branches, PRs, Actions, Dependabot configuration or scheduled updates. Steps 2–4 may share one completed code commit if intermediate changes cannot independently satisfy verification. The current request authorizes the review/plan; public deployment and package publication in step 6 need a separate explicit instruction.

### 1. Isolated SDK and runtime trial

Use Microsoft's [release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json) to select the official archive and verify its SHA-512 before extraction. Check the documented [x86/x64 CPU requirements](https://learn.microsoft.com/en-us/dotnet/core/compatibility/jit/11/minimum-hardware-requirements) for development and deployment hosts. This is a CPU/toolchain migration; local model loading is excluded.

The SDK-11/net10 case requires an actual .NET 10 runtime in the isolated environment. The net11 case must execute on a stable .NET 11 runtime. Record each independently; a newer SDK or major-runtime roll-forward is not evidence that a project targets and runs on net11. Keep `allowPrerelease=false`, avoid preview language/runtime-async switches and keep Native AOT/trimming outside this upgrade.

Adapt `scripts/Verify-Net11.ps1` deliberately. Its current replacement logic and case labels assume a net10 source baseline. After the main baseline changes, it must not label an unchanged net11 project as a net10 case or rewrite historical release records. Preserve November 7 RC observations as historical evidence.

### 2. Target frameworks and dependency decisions

Change the explicit TFM in each project; retain project-local framework declarations as required by CONTRIBUTING.md and the existing script helper. Set the new candidate version before packing so newly built artifacts cannot be confused with published preview.1 files. Pin SDK 11 with the existing patch-roll-forward policy.

| Dependency | Current | Proposed treatment |
| --- | --- | --- |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.14 | 11.0.0 or a reviewed stable 11.0 servicing version. Its new package targets net11. |
| Microsoft.Data.Sqlite | 10.0.14 | 11.0.0 or reviewed servicing. Its NuGet metadata includes a compatible net10 asset and SQLitePCLRaw 3.0.5; verify the changed native graph. |
| Microsoft.AspNetCore.TestHost | 10.0.14 | Match the ASP.NET Core 11 train. |
| Microsoft.Extensions.Http | 10.0.14 | Use the compatible 11.0 train. |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.14 | Remove Runtime's redundant direct reference for net11 and remove the central entry if no direct reference needs it. |
| Microsoft.Extensions.AI / .AI.OpenAI | 10.11.0 | Keep 10.11.0 initially. It is the latest stable version observed today; no stable 11.0.0 exists in their feeds. |
| Microsoft.Agents.AI / OpenAI | 1.26.0 / 2.14.0 | Retain initially if compatible. 1.28.0 / 2.15.0 are available, but adopting them is a separate dependency decision, not an automatic .NET-version substitution. |
| SkiaSharp/native assets, PdfPig, Markdig, Playwright, test SDK, xUnit/runner | Existing locked versions | Keep unless a demonstrated compatibility requirement or advisory warrants a deliberate update. |

Package availability/dependency groups were checked through the public NuGet index/nuspec endpoints on November 10. See the primary package records for [EF SQLite 11](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/11.0.0), [Data SQLite 11](https://www.nuget.org/packages/Microsoft.Data.Sqlite/11.0.0) and [MEAI 10.11](https://www.nuget.org/packages/Microsoft.Extensions.AI/10.11.0). These observations establish candidate dependencies, not a passing project build.

The [shared-framework change](https://learn.microsoft.com/en-us/dotnet/core/compatibility/extensions/11/extensions-in-shared-framework) explains Runtime's earlier NU1510 failure: DI abstractions are now supplied by net11. Remove the applicable reference instead of disabling the warning. Refresh lock files intentionally, inspect transitive changes, verify locked restores and refresh dependency advisory/license evidence without repository bots.

### 3. Source and behavior repairs

Treat warnings as errors and review the actual RTM diagnostics. The [ASP.NET diagnostic list](https://github.com/dotnet/aspnetcore/blob/main/docs/list-of-diagnostics.md) documents BL0013 and other Blazor rules; the previous RC failure does not prove which diagnostics RTM will emit.

Audit `BlazorWorkspaceIdentity`, shared `ExampleIdentity`, ProductionHost `HostIdentity`, and the Content/Workspaces pages. Stateful consumers need lifecycle-safe authentication-change handling and cleanup. Stateless authorization reads must continue to use fresh identity/current grants. Do not cache an identity once merely to silence an analyzer. Any targeted diagnostic exception needs a local justification after applicability review; blanket suppression is not an acceptance route.

Exercise identity changes, sign-out and permission revocation while a circuit exists. Cancel or reject work that no longer has authorization, keep drafts bound to their original owner, and prevent another identity from inheriting the previous workspace or artifact access. Preserve `IWorkspaceIdentity` and existing registration/component signatures unless a separately reviewed contract change becomes necessary.

Check [ASP.NET Core behavioral changes](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/11/overview?view=aspnetcore-11.0) against our actual middleware, particularly antiforgery and enhanced navigation/static assets. Exercise current protected forms and Development-only sign-in boundaries. Review relevant JS interop and disposal diagnostics without swallowing deliberate library exceptions.

For SQLite/EF, use copies of representative existing checkpoint and business databases. Verify draft/history restoration, owner isolation, version conflicts, idempotent receipts, stopped-host backup and transactions. Check any EF schema difference explicitly; changing the runtime must not silently reset or recreate existing data. Execute actual PNG encode/decode/resize and SQLite open/read/write against the new publish output. Review native package notices and licenses.

Keep the existing wire/document formats and typed renderer extensions. C# 15 unions, JSON enhancements and new process helpers are optional later improvements; adopting them is not needed to change the runtime. ZIP payload/audit code must continue rejecting corrupt archives under the new CRC validation behavior.

### 4. Make verification certify the new target

| Files/area | Required work |
| --- | --- |
| Verify, Verify-Packages, Verify-Readiness, Verify-Browser, Publish-Docs and Record-HostEvidence scripts | Resolve tool/test/generated-project output paths from the expected project framework; remove stale net10-only assumptions. |
| `PackageAudit` | Require each of our eight packages' `lib/net11.0` assembly/PDB assets, intended dependencies/version, MIT metadata and exact source commit/SourceLink. Third-party dependencies may legitimately use earlier compatible TFMs. |
| `ReleaseReadiness`, `EvidenceStamp`, `DocsHostSmoke` | Check actual stable runtime 11 and intended framework/SDK identity. Keep complete receipts, artifact hashes and source freshness checks. |
| `DeploymentBundle` and deployment tests | Seal/validate net11 runtimeconfig and x64 native files. Version changed manifest rules where needed and retain an explicitly bounded path for validated format-2/net10 rollback bundles. Do not accept an arbitrary framework or omit a field to pass verification. |
| Consumers, browser fixtures and docs | Build/run Quickstart, the three apps and ProductionHost using net11 packages; update active installation/hosting instructions and keep old release evidence dated. |

Run full `scripts/Verify.ps1` after the tooling is ready. Required coverage includes documents/rendering, direct-edit protection, reconnect/process recovery, conflicting saves, lost action responses, identity/permission changes, content/download bounds and real image bytes. Keep browser tests serial with GPU/WebGL/Vulkan disabled and native events plus server acknowledgements. Keep xUnit v2/VSTest; .NET 11's newer test-template defaults are not a request to migrate runners.

Require nonempty passing TRX, all discovered cases accounted for, all eight package audits, all five fresh package consumers, nine existing renderer workloads and the actual published native/rollback checks. Extend focused regression coverage where the upgrade changes behavior; do not replace assertions or loosen budgets merely to obtain a pass. Record counts and any explicit platform limits. Historical net10 receipts cannot satisfy new source/runtime acceptance.

### 5. MonsterASP and rollback preparation

MonsterASP [advertises .NET 11 hosting](https://www.monsterasp.net/.NET11-Hosting/) and side-by-side runtimes. That does not establish stable 11.0.0 availability on our specific server. Confirm the exact site's RTM runtime/IIS support before uploading. Preserve its x64, InProcess, Production fixture setup and the .NET 10 runtime needed for rollback.

Retain the current sealed bundle, manifest/hashes, source identity and stopped-host private data/configuration backup outside Git. Build a new framework-dependent win-x64 bundle with separate publish intermediates and real native checks. Rehearse net10-to-net11 upgrade and rollback locally while preserving unmanaged/private files and owned drafts. If the provider's RTM runtime is not ready, finish local migration evidence and keep live deployment/publication pending; do not weaken the host gate or substitute an RC runtime.

### 6. Separately authorized preview release

Freeze a clean candidate and rerun all release checks for that exact source. Under explicit deployment authorization, deploy the sealed bundle using validated TLS/no remote deletion and verify stable runtime 11, TFM/source/fingerprint, x64, fixture mode, native browser interactions, public guides and disabled Development-only routes. Roll back with the retained payload if those checks fail.

For an explicitly requested NuGet release, confirm availability of the proposed version, obtain a fresh scoped credential through the approved secure local flow and remove preview.1-specific credential-file assumptions from the publisher. Keep all readiness gates before credential decryption, partial-acceptance inspection and immutable archives. After publishing, verify signatures, unchanged public payloads, portable symbols, ownership and all five public-only consumers in fresh caches. Update current guides and retain the first release's dated record. No GitHub Release, announcement, automation or account-wide change is implied.

## .NET 11 AI and UI opportunities

Microsoft's [experimental Blazor AI components](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/ai-components?view=aspnetcore-11.0) provide chat, tool/approval rendering and shared-state building blocks. They use `IChatClient`; AG-UI supports richer remote-agent interactions. The package remains experimental/prerelease throughout .NET 11, and its package feed currently has no stable version.

Our existing MEAI/Agent Framework connections already cover the provider-abstraction layer. After the runtime migration passes, assess an optional interoperability adapter with offline event fixtures. Keep IntentBlazor's validated document, deterministic renderer, revision/dirty-field rules and host authorization as the application boundary. Do not make AG-UI, a remote agent service or extra components mandatory for ordinary consumers.

Also assess static docs caching, asynchronous validation and circuit pause/resume as separate follow-up features. Authentication/private workspace output must never be shared through a public cache. Automatic circuit pausing needs its own recovery evidence before enabling it. WASM/MAUI Hybrid, RAG/memory expansion, process-API refactors and new output primitives retain their own designs.

## Completion definition and standing constraints

The migration is technically complete when stable SDK 11 builds the 20 net11 projects, focused and complete offline checks pass on actual runtime 11, all eight local packages and five consumers pass, API/document/persistence compatibility is reviewed, and the sealed native IIS payload plus rollback pass. Public release completion additionally requires the separately authorized exact-source MonsterASP and NuGet operations.

Maintain only `main` and deliberate locked dependency updates. Do not start Ollama, LM Studio or their workers, including activating status commands; do not run paid OpenAI/Codex inference. Use HTTP/process/model substitutes. Live provider reports remain optional under [decision 0012](architecture/0012-framework-verification-and-optional-model-reports.md). Preserve the repository/publication boundaries in [decision 0014](architecture/0014-main-only-repository.md) and the evidence distinctions in [decision 0016](architecture/0016-candidate-evidence-and-isolated-compatibility.md).
