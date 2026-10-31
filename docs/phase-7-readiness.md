# Developer preview candidate

`0.1.0-preview.1` is an **unreleased candidate**, not an available NuGet release. The framework keeps the default `<AiInput />` and `<AiOutput />` developer experience. Phase 7 adds independent package-consumer execution, package/symbol auditing, repeatable renderer workloads, production host checks and an explicit release-readiness report.

## Reproduce the candidate checks

Run `pwsh -File scripts/Verify.ps1` with the SDK in `global.json`. It restores locked dependencies, builds, checks formatting, runs the behavior suite, checks documentation links, packs all eight libraries, audits their metadata and portable symbols, and restores/builds/runs the quickstart and three examples in temporary directories outside this repository. Those consumers use framework packages instead of framework project references.

`artifacts/phase7/evidence/` contains machine-readable package, runtime and readiness reports. Ordinary CI reports unmet live/account gates without failing deterministic checks. `pwsh -File scripts/Verify-Readiness.ps1 -RequireReady` fails unless every release gate passes. Never use a scripted model, a successful model listing or a server connection as evidence of live model capability.

## Live model evidence

See the [provider matrix](provider-evaluation.md). The bounded opt-in evaluator supports loopback Ollama and LM Studio only, with synthetic news, staff and asset tasks. It records the model identity/revision, server version, budgets, C# reads, document acceptance, selection, protected drafts and failures. Native read tools and preloaded C# data are separate profiles. No OpenAI request or Codex inference has been performed for this candidate; Tory explicitly deferred live OpenAI verification.

The local runs did not establish a fully successful three-app provider profile. Live local/frontier acceptance remains open. A current source fingerprint must match successful evidence before the publication gate passes. Credentials, raw prompts, model text, reasoning and generated documents do not belong in committed evidence.

## Rendering workload budgets

The verification tool warms each workload three times and records 30 samples, median and p95. Initial p95 budgets are 250 ms for opening/rendering each fixture and a protected staff follow-up; 100 ms for selecting/rendering each fixture; and 500 ms for a 250-row table or rejecting 251 rows while retaining the previous document. These are server-side HTML renderer regression budgets, not browser, network or model latency claims. Hardware/runtime metadata is recorded in the report. Timing tests use real documents and the default AiOutput; ordinary unit tests check report validity without asserting wall-clock durations.

## Publication and hosting gates

No public packages or release tag have been created. A NuGet profile and confirmed trusted-publishing policy are required. The [release guide](site/releasing.md) contains exact account, workflow and hosting steps. The separate publishing workflow checks tag/version, deterministic verification and current evidence before requesting a short-lived OIDC publishing credential. It does not publish on a push to main.

The docs app deploys as a framework-dependent .NET 10 Interactive Server application on IIS. Its public routes remain deterministic fixtures. Synthetic identities, private example workflows and anonymous live inference stay Development-only. `/health/ready` reports version, source revision and mode without exposing private configuration. Public hosting is proven separately by HTTPS requests and interactive browser checks; generating a publish folder alone does not prove a deployment.

The remaining release actions are a confirmed NuGet owner/policy, successful current local/frontier evidence, package publication and a tagged release. A public preview announcement follows those gates. [Decision 0010](architecture/0010-preview-release-gates.md) records the boundaries.

Public docs hosting has been deployed and checked on MonsterASP: [docs](https://intentblazor.runasp.net/docs/quickstart), [explorer](https://intentblazor.runasp.net/explorer), and [hosting evidence](phase-7-hosting.md). HTTPS, the reported version/source, a WebSocket circuit, all three fixtures, protected edits, keyboard operation and small-screen chart alternatives were observed. Confirm the release workflow's host-verification variable against the current deployment before a future package release; ordinary CI does not access hosting credentials.
