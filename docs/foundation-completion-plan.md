# IntentBlazor foundation completion plan

Status: **approved by Tory on October 31, 2026; framework scope revised November 4**. Steps 1–3 are implemented with offline verification; Steps 4–6 remain. Step 3 adds automated browser checks and a repeatable manual accessibility-review procedure; no complete screen-reader audit is claimed. Live model evaluation is optional developer compatibility work, not a deferred release requirement. Originally recorded October 30, 2026, America/Edmonton. AiInput and AiOutput remain the two default interaction components; setup, storage, content and specialist renderers stay optional.

The outcome is a framework another developer can install, configure with their chosen model, expose an authorized C# service, and use to build an interactive application with recoverable state. We verify connection primitives, tool routing, document handling, state and rendering offline. Developers evaluate their model's behavior, hardware suitability, latency, costs and application workflows. [Decision 0012](architecture/0012-framework-verification-and-optional-model-reports.md) replaces the earlier mandatory live-provider requirements.

November 6 revision: [decision 0014](architecture/0014-main-only-repository.md) makes GitHub a main-only source/history backup. Develop, verify and update dependencies here in Codex. Dependabot and GitHub Actions are disabled and the publishing workflow is removed. CI references in completed-step evidence below are historical; future verification is local. Step 6 needs an explicitly selected publishing method, not a recreated GitHub pipeline.

## Current position

The Phase 7 baseline at `b88fc592dcb0e770f6aa0b5142f1edc353301a65` recorded 316 passing tests, independent package-only execution of the quickstart and three examples, eight package/symbol audits, and nine renderer workloads within their budgets. [Windows and Ubuntu CI passed](https://github.com/Kita72/IntentBlazor/actions/runs/41172546182). The [public MonsterASP site](https://intentblazor.runasp.net/) has verified HTTPS and interactive fixture behavior. These are completed foundations to preserve.

Repository maintenance has addressed the two open Dependabot pull requests: [#1 setup-dotnet 6.0.0](https://github.com/Kita72/IntentBlazor/pull/1) and [#2 checkout 7.0.1](https://github.com/Kita72/IntentBlazor/pull/2). Both passed Windows/Ubuntu PR checks and were merged sequentially. [Main CI also passed with the combined versions](https://github.com/Kita72/IntentBlazor/actions/runs/41174077728). Their changes retain full commit pins, read-only ordinary CI permissions and disabled checkout credential persistence.

The remaining gaps are a complete host integration guide, compatibility/operations checks and publication setup. Step 2 added offline-verified adapters and opt-in evaluation paths for Ollama, LM Studio, OpenAI and Codex. Historical local evaluations remain unverified observations; no paid inference has been performed. They do not need to be completed before framework publication.

## Scope decisions

| Item | Treatment in this milestone |
| --- | --- |
| Ollama, LM Studio and OpenAI | Offer configurable connections and capabilities; verify our SDK requests, tools, parsing and errors offline. Live profile reports are optional developer observations. |
| Codex | Offer the offline-verified experimental independent process adapter and document its preload-only capabilities. No live certification is claimed; it does not connect to this desktop chat. |
| Browser regression tests | Automate the critical fixture workflows and keep a documented manual accessibility review. |
| Open issues #4 and #5 | Complete their acceptance criteria, link the results, then close them. |
| Identity, ownership and durable state | Complete a compiled host integration recipe and verify isolation/recovery; the host continues to own accounts and business rules. |
| Memory and workspace organization | Prove durable conversation/context within a workspace and document its limits. Workspace naming/listing belongs to host application chrome; semantic recall and folder hierarchies remain expansion items. |
| .NET 11 | Perform isolated compatibility verification and record the exact SDK/runtime result. Keep .NET 10 as the published baseline unless a separate migration is approved. |
| NuGet and public preview | Finish owner/policy setup, verify the release workflow, publish exact tagged artifacts, and test consumption from the public feed. |
| MonsterASP | Preserve the working fixture deployment; improve bundle size, repeatable smoke checks and rollback. |
| WASM/MAUI Hybrid, distributed storage and advanced media | Keep explicit expansion work, outside this first supported Interactive Server foundation. |

## 1 Close the existing backlog and reconcile status

The [keyboard and focus guide](contributing/keyboard-and-focus.md) fulfills [issue #4](https://github.com/Kita72/IntentBlazor/issues/4) and is linked from the output specification, binding reference, contribution guide and docs catalog. It covers the current controls, rating display versus score editing, errors/focus during updates, all three synthetic fixture walkthroughs, and automated versus manual checks.

The [provider capability report template](contributing/provider-capability-report.md) and [fictional example](contributing/provider-capability-report-example.md) fulfill [issue #5](https://github.com/Kita72/IntentBlazor/issues/5). The template records exact profile identity, modes, settings, budgets, capability outcomes, recovery, usage and evidence. Contribution routes and the provider issue form link it. Fictional observations cannot serve as live evidence, and a successful probe does not imply working tools or application workflows.

Reconcile the roadmap, current guides, test inventory and original unchecked preview checklist against their evidence. Preserve dated completion records as history. Give every outstanding item a disposition: finish in this milestone, obtain an external decision, or retain in the expansion backlog. Preserve the existing deterministic suite and target the unverified boundaries and missing automated workflows.

Acceptance: both documentation issues meet their original criteria, current status pages agree, and main CI passes with both action upgrades. No release or feature is marked complete solely to empty a checklist.

## 2 Complete the framework provider primitives and offline checks

Step 2 is implemented. The [provider workflow guide](foundation-step2-provider-workflows.md) covers local generation fixes, explicit live-call opt-ins, optional six-workflow protocol-2 reports and native-tool observations. Offline tests verify supported primitives and failure handling. Do not start Ollama, LM Studio or their workers on Tory's current machine, even through service-activating status commands; those installations are reserved for Tory's own testing. Do not run paid OpenAI/Codex inference. A future live diagnostic run requires a new explicit user decision and a suitable host; no such run is required for this milestone.

Retain strict validation, protected drafts and bounded calls/corrections. Scripted HTTP/process/model substitutes exercise actual SDK serialization, tool/presentation sequencing, accepted and rejected envelopes, correction data, cancellation, errors, budgets and metadata privacy. A demonstrated framework defect needs a focused regression case; optional live observations must not be relabelled as successful to close a task.

The optional shared harness supports explicit opt-in OpenAI and Codex diagnostics alongside Ollama and LM Studio. Keep provider SDK calls in the AI boundary. Preloaded C# data and model-initiated native read tools remain separate profiles. Codex uses an isolated fixture workspace and restricted capabilities, verified through process substitutes; it remains an experimental independent preload-only adapter.

Developers provide endpoint/model selection, credential resolution, supported capability/options and their authorized C# services. Unsupported settings remain explicit; default AiInput/AiOutput need no additional wiring. Build, test and publication never start a model service or make paid inference calls.

Offline checks also exercise the real example-app service boundaries; the optional evaluator reuses these outcomes:

| App | Deterministic framework/service workflow |
| --- | --- |
| Gaming News | Read authorized news data, compose a source-attributed interactive view, select an item, retain a bookmark/comparison draft through a follow-up. |
| Staff Business | Read authorized jobs/staff, compose a useful table/editor, preserve direct edits, and route an explicitly confirmed synthetic save through the existing role/version checks. A conflicting save must retain the draft. |
| Asset Workbench | Read authorized asset metadata, compose an inspection/editing workspace, retain a label/dimension draft, and invoke the existing registered resize/export workflow only through its authorized action boundary. Retain the original image. |

Acceptance: offline tests verify actual request/process shapes, native/preloaded C# data routing, accepted and rejected documents, protected edits and real example services/actions. Default AiInput/AiOutput remain unchanged. Live reports are optional and cannot determine framework release readiness; when supplied, report failures and exact observed settings/usage without certifying untested capabilities. Codex is documented as experimental and preload-only. This acceptance is implemented; model certification is not outstanding milestone work.

## 3 Automate the important regressions

Implemented: the [offline browser suite](foundation-step3-browser-regressions.md) exercises the default pair, real example apps, failure recovery, actual circuit reconnect and process restoration, lost action replies, private files, keyboard behavior and a packed public renderer. Local verification installs Chromium headless, runs serially without GPU/model services, validates TRX and retains failure diagnostics. The guide records the manual assistive-technology review owner/procedure separately; no complete screen-reader audit is claimed.

Map each required behavior to existing tests, then add missing checks where they protect an observable outcome. Preserve the existing suite and add a focused browser test project for fixture mode, using Playwright for .NET or an equivalent supported runner. Start with Chromium on CI; keep Windows/Linux repository checks and make broader browser coverage a deliberate matrix choice. [Playwright's CI guidance](https://playwright.dev/dotnet/docs/ci) describes the browser/dependency installation requirements.

| Scenario | Required result |
| --- | --- |
| Fresh default AiInput/AiOutput | Interactive readiness, service data and a useful output without additional component wiring. |
| Selection, sorting and follow-up | Correct details/order, stable record identity and protected edits. |
| Disconnect and circuit loss | Distinguish reconnecting a surviving circuit from restoring a durable workspace in a new circuit/process. Recover edits and context without replaying uncertain writes. |
| Conflicting save and role/owner change | Recheck access/version, refuse unauthorized or stale operations, retain the user's private draft where safe. |
| Invalid, partial or late model result | Keep the last accepted output; show a recoverable error; reject a result prepared before a newer local edit. |
| Cancel, timeout, unavailable provider and exhausted budget | End the run predictably, retain the workspace and allow a subsequent valid request. |
| Attachments, artifacts and image versions | Enforce bounds/access, download the actual authorized bytes and preserve original/derived provenance. |
| Keyboard and responsive output | Test actual change events, tab order, focus after updates, errors, chart alternatives and contained scrolling at narrow widths. Include a manual screen-reader review. |
| Settings and renderer extensions | A permitted setup change affects the next request; unsupported settings are visible; a public extension works from packed libraries. |

Live evaluation stays separate and opt-in. Deterministic CI should not need model accounts, public endpoints or an API key. Retain useful failure artifacts without credentials, private prompts or model reasoning. Assertions target behavior, rather than fixed natural-language answers or a target test count. Timing thresholds remain separate from ordinary functional assertions.

Acceptance: the three app workflows and recovery paths run from a clean checkout on CI, browser failures produce actionable diagnostics, and the existing recovery/security checks continue to pass. Previously manual release checks have a repeatable owner and procedure where automation is unsuitable.

## 4 Complete the developer and production host journey

Keep the minimal quickstart intact and add a compiled production integration recipe using trusted authenticated server claims. Demonstrate `IWorkspaceIdentity`, `IWorkspaceAccess`, an authorized C# service with attribute and explicit registration alternatives, a store, action authorization, and a stable logical workspace ID. Include host-owned per-user usage and concurrency controls at the AI request boundary, with no live inference enabled by default. The application supplies its login provider and business database; IntentBlazor does not gain a mandatory account system.

Document how to reopen a workspace, restore its conversation archive and bounded context, provide host-owned workspace naming/selection, and handle retention/backups. Clarify the existing extractive history index and the absence of cross-workspace semantic retrieval. Keep any workspace chooser outside the required two-component page.

Record the checkpoint/schema compatibility policy: restore supported formats with representative saved data, reject unsupported formats without silently discarding drafts, and document recovery/backup procedures before upgrades. A general database migration engine or vector-memory service is not required for this foundation.

Acceptance: a fresh host integration builds against packages, two authenticated users cannot access each other's workspaces/artifacts, concurrent sessions retain their own state, restored references are reauthorized, and reopening a known workspace restores its supported state/context. Setup and renderer tutorials also compile against packaged public APIs. The docs explain which responsibilities belong to the framework and which belong to the host.

## 5 Strengthen compatibility and release operations

Run .NET 11 compatibility checks in an isolated SDK installation without changing the working .NET 10 environment. Record SDK, target framework, runtime, operating system, build/test/sample outcomes and any limitations. Passing under a newer SDK is separate from targeting/running on the newer runtime. The stable package baseline remains .NET 10; [Microsoft's support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) is the reference for the release status at verification time.

Bind release evidence to the code and configuration it actually evaluates. The current source fingerprint covers library sources, fixture JSON and central package versions; extend or version it to cover relevant evaluator/protocol and app-service changes. Require package evidence for the intended candidate revision, prevent stale evidence from passing, and replace the loose public-host confirmation with a checked version/source/mode observation plus recorded circuit verification. These are changes to release tooling, not a proposal to weaken acceptance.

Reduce the IIS deployment footprint after verifying the site's process architecture and native dependencies. The current local bundle is approximately 516.9 MiB, including 274.7 MiB of native PDB files. Keep portable framework packages and their symbols intact; optimize the host-specific deployment separately. Preserve a prior bundle for rollback and only remove obsolete managed files through a validated deployment manifest.

Acceptance: current evidence passes only for the intended candidate, modified source or missing/failed evidence blocks release before NuGet login, the optimized bundle still supports its declared native features, and public HTTPS/health/circuit smoke checks and a rollback rehearsal pass. Review dependency vulnerabilities and licensing findings with their actual relevance; preserve locked restore and package provenance checks.

## 6 Publish and prove the developer preview

Tory creates/selects the NuGet profile or organization and chooses an explicit publishing method compatible with the local maintainer workflow. The former `publish.yml`/`release` OIDC setup is removed; do not recreate it as a prerequisite. Confirm the owner, credential handling and publishing checks before a run, as described in the [release guide](site/releasing.md). The existing account/policy readiness gate remains unsatisfied until this decision and verification are complete. No account terms are accepted and no package is published merely by approving a draft plan.

After the code/evidence is frozen and publication is explicitly requested, run complete local verification, identify the exact source/version and publish all eight packages with their symbols using the chosen verified method. Never replace a published version or move a tag to hide a failed attempt. Restore the quickstart and three examples from NuGet.org using a fresh isolated package cache with no local framework feed or framework project references, then exercise their representative workflows.

Update the hosted guides and release notes with the actual package version, source tag, implemented adapter capabilities, optional observed profiles, known limits and upgrade policy. Create the release discussion/announcement only for the audience and channel Tory selects. Keep ordinary public demos in deterministic mode.

Acceptance: another developer can install the public packages, run all three fixture apps, configure a model through documented connection primitives, add a C# capability/renderer through public contracts and follow the contribution process. Required framework/package/account/host readiness gates are green for the intended candidate; optional live reports remain clearly separate and may be absent.

## Expansion backlog after the foundation

Retain named follow-on work for WASM and MAUI/Blazor Hybrid hosting; distributed stores and multi-node recovery; richer workspace/folder navigation and semantic memory/RAG; image pixel/vision transport, OCR, audio/video and Office fidelity; generative asset tools and advanced 3D editing; and broader renderer/theme ecosystems. Each needs its own contract, authorization, compatibility and acceptance design. This milestone records their boundaries and extension points without making them release blockers or promising their implementation.

## Execution order and remaining decisions

Steps 1–3 are implemented. Continue with the production-host recipe without model accounts, inference or local services; NuGet ownership setup can be prepared alongside them. Complete compatibility/operations checks and freeze required evidence after behavior-changing fixes, then publish and repeat public-feed consumption tests.

The approved scope is an Interactive Server foundation on .NET 10, with separately recorded .NET 11 compatibility. Tory's November 4 decision replaces three-provider/native-model release criteria with optional compatibility reports while retaining offline framework checks. Remaining external decisions are NuGet ownership/policy and the eventual announcement audience. No step authorizes using Tory's local model installations or paid inference, and later publication/compatibility work remains incomplete.
