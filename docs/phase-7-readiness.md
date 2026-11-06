# Developer preview candidate

`0.1.0-preview.1` is an **unreleased candidate**, not an available NuGet release. The framework keeps the default `<AiInput />` and `<AiOutput />` developer experience. Phase 7 adds independent package-consumer execution, package/symbol auditing, repeatable renderer workloads, production host checks and an explicit release-readiness report.

## Reproduce the candidate checks

Run `pwsh -File scripts/Verify.ps1` with the SDK in `global.json`. It restores locked dependencies, builds, checks formatting, runs the behavior suite, checks documentation links, packs all eight libraries, audits their metadata and portable symbols, and restores/builds/runs the quickstart and three examples in temporary directories outside this repository. Those consumers use framework packages instead of framework project references.

`artifacts/phase7/evidence/` contains machine-readable package, runtime and readiness reports. Local verification reports unmet account/host gates without failing deterministic checks. `pwsh -File scripts/Verify-Readiness.ps1 -RequireReady` fails unless all four mandatory gates pass: runtime budgets, package metadata, NuGet policy and public hosting. Full deterministic framework verification runs before this release check. Live-provider/native-tool results appear separately under `providerObservations` and never determine `ready`. Missing, old, failed or unreadable model reports remain unverified. [Decision 0012](architecture/0012-framework-verification-and-optional-model-reports.md) records Tory's November 4 scope revision. [Decision 0014](architecture/0014-main-only-repository.md) removes ongoing GitHub CI and the publishing workflow; local checks remain available.

## Live model evidence

See the [provider matrix](provider-evaluation.md) and [workflow evaluation guide](foundation-step2-provider-workflows.md). The optional evaluator supports loopback Ollama/LM Studio and explicitly opted-in OpenAI/Codex paths. A complete protocol-2 report requires three shared fixtures plus three real example-service workflows, including registered actions, conflict retention and actual image bytes. Native tools and preload remain separate observations. These criteria qualify a report, not a framework release. No OpenAI request or Codex inference has been performed. Do not run paid inference or start Ollama, LM Studio or their workers on Tory's machine; those installations are reserved for Tory's own testing.

The local runs did not establish a fully successful provider profile. Developers choose and evaluate their own model, hardware, costs and application workflows. Offline tests verify our SDK requests, tool routing, validation, failures and state handling without claiming live model quality. Optional successful reports must retain matching source/protocol identity. Credentials, raw prompts, model text, reasoning and generated documents do not belong in committed evidence.

## Rendering workload budgets

The verification tool warms each workload three times and records 30 samples, median and p95. Initial p95 budgets are 250 ms for opening/rendering each fixture and a protected staff follow-up; 100 ms for selecting/rendering each fixture; and 500 ms for a 250-row table or rejecting 251 rows while retaining the previous document. These are server-side HTML renderer regression budgets, not browser, network or model latency claims. Hardware/runtime metadata is recorded in the report. Timing tests use real documents and the default AiOutput; ordinary unit tests check report validity without asserting wall-clock durations.

## Publication and hosting gates

No public packages or release tag have been created. The previous GitHub publishing workflow has been removed. Tory must choose a NuGet owner and publishing method before this work resumes; the existing account/policy gate remains unsatisfied and no alternative publisher is claimed as verified. The [release guide](site/releasing.md) records this boundary and the retained local package/hosting checks. Neither a push to `main` nor a tag publishes anything.

The docs app deploys as a framework-dependent .NET 10 Interactive Server application on IIS. Its public routes remain deterministic fixtures. Synthetic identities, private example workflows and anonymous live inference stay Development-only. `/health/ready` reports version, source revision and mode without exposing private configuration. Public hosting is proven separately by HTTPS requests and interactive browser checks; generating a publish folder alone does not prove a deployment.

The remaining release actions are completed framework/browser/host verification, a confirmed NuGet owner/policy, package publication and a tagged release. A public preview announcement follows those gates. Live model evaluation is not required. The [foundation plan](foundation-completion-plan.md) tracks the outstanding work.

Public docs hosting has been deployed and checked on MonsterASP: [docs](https://intentblazor.runasp.net/docs/quickstart), [explorer](https://intentblazor.runasp.net/explorer), and [hosting evidence](phase-7-hosting.md). HTTPS, the reported version/source, a WebSocket circuit, all three fixtures, protected edits, keyboard operation and small-screen chart alternatives were observed. Recheck the exact deployed candidate before a future package release; historical hosting evidence is not refreshed by a source push. Hosting credentials remain outside source and verification output.
