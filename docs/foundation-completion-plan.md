# IntentBlazor foundation completion plan

Status: **proposed for Tory's review**. Recorded October 30, 2026, America/Edmonton. This completes the remaining developer-preview work before adding major capabilities. AiInput and AiOutput remain the two default interaction components; setup, storage, content and specialist renderers stay optional.

The outcome is a framework another developer can install, connect to a verified model, expose an authorized C# service, and use to build an interactive application with recoverable state. A usable foundation needs reliable model behavior and a reproducible developer journey alongside the existing deterministic renderer.

## Current position

The Phase 7 baseline at `b88fc592dcb0e770f6aa0b5142f1edc353301a65` recorded 316 passing tests, independent package-only execution of the quickstart and three examples, eight package/symbol audits, and nine renderer workloads within their budgets. [Windows and Ubuntu CI passed](https://github.com/Kita72/IntentBlazor/actions/runs/41172546182). The [public MonsterASP site](https://intentblazor.runasp.net/) has verified HTTPS and interactive fixture behavior. These are completed foundations to preserve.

Repository maintenance has addressed the two open Dependabot pull requests: [#1 setup-dotnet 6.0.0](https://github.com/Kita72/IntentBlazor/pull/1) and [#2 checkout 7.0.1](https://github.com/Kita72/IntentBlazor/pull/2). Both passed Windows/Ubuntu PR checks and were merged sequentially. [Main CI also passed with the combined versions](https://github.com/Kita72/IntentBlazor/actions/runs/41174077728). Their changes retain full commit pins, read-only ordinary CI permissions and disabled checkout credential persistence.

The largest remaining gaps are live provider reliability, automated browser workflows, publication setup, and a complete host integration guide. The local evaluations did not produce a successful three-app provider profile. OpenAI and Codex live evaluation remain deferred. The current evaluator exposes a loopback-only `model-local` command; adding a key alone will not create an OpenAI evaluation path.

## Scope decisions

| Item | Treatment in this milestone |
| --- | --- |
| Ollama, LM Studio and OpenAI | Complete the existing release gates with exact tested provider/model profiles and all three workflows. |
| Codex | Verify the supported integration and run a bounded live preload workflow; resolve failures or bring an explicit experimental-status decision to Tory. It does not connect to this desktop chat. |
| Browser regression tests | Automate the critical fixture workflows and keep a documented manual accessibility review. |
| Open issues #4 and #5 | Complete their acceptance criteria, link the results, then close them. |
| Identity, ownership and durable state | Complete a compiled host integration recipe and verify isolation/recovery; the host continues to own accounts and business rules. |
| Memory and workspace organization | Prove durable conversation/context within a workspace and document its limits. Workspace naming/listing belongs to host application chrome; semantic recall and folder hierarchies remain expansion items. |
| .NET 11 | Perform isolated compatibility verification and record the exact SDK/runtime result. Keep .NET 10 as the published baseline unless a separate migration is approved. |
| NuGet and public preview | Finish owner/policy setup, verify the release workflow, publish exact tagged artifacts, and test consumption from the public feed. |
| MonsterASP | Preserve the working fixture deployment; improve bundle size, repeatable smoke checks and rollback. |
| WASM/MAUI Hybrid, distributed storage and advanced media | Keep explicit expansion work, outside this first supported Interactive Server foundation. |

## 1 Close the existing backlog and reconcile status

Complete [issue #4](https://github.com/Kita72/IntentBlazor/issues/4) with a dedicated keyboard/focus guide linked from the output specification. Cover lists, tables, inputs, selects, buttons, read-only versus editable ratings, errors, document updates, and the news/staff/asset examples. Distinguish automated checks from human keyboard and screen-reader review.

Complete [issue #5](https://github.com/Kita72/IntentBlazor/issues/5) with a reusable provider capability report template and a clearly fictional example. Include exact client/server/model versions, model revision, settings, context limits, streaming, tools, schema output, cancellation, recovery and reported usage. A successful probe must not imply successful tools or application workflows.

Reconcile the roadmap, current guides, test inventory and original unchecked preview checklist against their evidence. Preserve dated completion records as history. Give every outstanding item a disposition: finish in this milestone, obtain an external decision, or retain in the expansion backlog. Preserve the existing deterministic suite and target the unverified boundaries and missing automated workflows.

Acceptance: both documentation issues meet their original criteria, current status pages agree, and main CI passes with both action upgrades. No release or feature is marked complete solely to empty a checklist.

## 2 Make the real model workflows reliable

Investigate the existing failed local runs before changing models or increasing budgets. Separate backend/protocol failures, unsupported combinations of structured output and tools, context/output truncation, slow inference, and invalid presentation documents. Retain strict validation and protected drafts. Fix a demonstrated adapter/prompt/runtime defect with a focused regression test; do not relax the checks to obtain a pass.

Extend the shared evaluation harness to support explicit opt-in OpenAI and Codex runs alongside Ollama and LM Studio. Keep provider SDK calls in the AI boundary. Reuse the same scenario outcomes and sanitized reporting, while recording the different capabilities of each connection. Preloaded C# data and model-initiated native read tools remain separate profiles. Codex must be tested through its supported integration, using an isolated fixture workspace and restricted capabilities.

Evaluate exact available local model revisions and settings. OpenAI and Codex calls begin only after Tory approves the live configuration and resource budget; credentials stay in supported local secret storage, outside chat and reports. No paid inference enters ordinary PR CI. Model downloads or hosting changes require their own decision when needed.

In addition to the shared fixture evaluator, exercise the real example-app service boundaries:

| App | Required live workflow |
| --- | --- |
| Gaming News | Read authorized news data, compose a source-attributed interactive view, select an item, retain a bookmark/comparison draft through a follow-up. |
| Staff Business | Read authorized jobs/staff, compose a useful table/editor, preserve direct edits, and route an explicitly confirmed synthetic save through the existing role/version checks. A conflicting save must retain the draft. |
| Asset Workbench | Read authorized asset metadata, compose an inspection/editing workspace, retain a label/dimension draft, and invoke the existing registered resize/export workflow only through its authorized action boundary. Retain the original image. |

Acceptance: current successful news/staff/assets reports satisfy the existing Ollama, LM Studio and OpenAI release gates. Required supported profiles prove actual C# reads, accepted initial/follow-up documents, selection and draft continuity. The documented service/tool developer journey also needs native-tool coverage for OpenAI and at least one local profile; a preload pass cannot substitute for that claim. Reports record failures, settings, first useful output/follow-up timing, context growth and usage only where observed. Codex has either a verified documented profile or a specific limitation and release-scope decision approved by Tory. Existing release gates stay intact until an explicit decision changes them.

## 3 Automate the important regressions

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

Tory creates/selects the NuGet profile or organization and confirms the trusted publishing policy. Use repository owner `Kita72`, repository `IntentBlazor`, exact workflow filename `publish.yml` and environment `release`, as detailed in the [release guide](site/releasing.md). Confirm the environment protections and profile/policy before enabling a publishing run. No account terms are accepted and no package is published merely by approving a draft plan.

After the code/evidence is frozen, run complete verification, create the exact source/version tag and publish all eight packages with their symbols through the OIDC workflow. Never replace a published version or move a tag to hide a failed attempt. Restore the quickstart and three examples from NuGet.org using a fresh isolated package cache with no local framework feed or framework project references, then exercise their representative workflows.

Update the hosted guides and release notes with the actual package version, source tag, verified provider profiles, known limits and upgrade policy. Create the release discussion/announcement only for the audience and channel Tory selects. Keep ordinary public demos in deterministic mode.

Acceptance: another developer can install the public packages, run all three fixture apps, complete the documented verified AI path, add a C# capability/renderer through public contracts and follow the contribution process. The release readiness report is fully green for the intended candidate, with no required item left labelled deferred.

## Expansion backlog after the foundation

Retain named follow-on work for WASM and MAUI/Blazor Hybrid hosting; distributed stores and multi-node recovery; richer workspace/folder navigation and semantic memory/RAG; image pixel/vision transport, OCR, audio/video and Office fidelity; generative asset tools and advanced 3D editing; and broader renderer/theme ecosystems. Each needs its own contract, authorization, compatibility and acceptance design. This milestone records their boundaries and extension points without making them release blockers or promising their implementation.

## Execution order and decisions for review

Start with backlog/documentation closure and local model diagnosis. Browser automation and the host recipe can proceed without paid-model accounts; NuGet ownership setup can be prepared alongside them. Complete compatibility/operations checks and freeze evidence after behavior-changing fixes, then publish and repeat public-feed consumption tests.

The recommended scope is an Interactive Server foundation on .NET 10, with separately recorded .NET 11 compatibility. Preserve the existing three-provider release criteria. Tory's remaining external decisions are the OpenAI/Codex live configuration and budget, NuGet ownership/policy, and the eventual announcement audience. This plan proposes implementation work for review; it does not resume deferred paid inference, change repository access, or publish packages.
