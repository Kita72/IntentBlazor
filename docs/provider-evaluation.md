# Provider evaluation matrix

Contribute observations with the [provider capability report template](contributing/provider-capability-report.md). The [fictional example](contributing/provider-capability-report-example.md) demonstrates the format only; it is not live evidence. Separate transport, native tools, preload, presentation acceptance and recovery outcomes for each exact profile.

**Framework scope, November 4, 2026:** live reports are optional developer/contributor compatibility observations, not framework release gates. Framework verification uses offline SDK/HTTP/process/model substitutes. Do not start Ollama, LM Studio or their workers on Tory's machine or run paid inference. [Decision 0012](architecture/0012-framework-verification-and-optional-model-reports.md) supersedes the earlier live acceptance requirement while preserving historical failures and the strict criteria for an observed profile.

**Foundation Step 2 update, November 1, 2026:** [generation fixes and protocol-2 evaluation](foundation-step2-provider-workflows.md) add local document-object delivery, native data-loading/presentation sequencing, retained tool data during correction, and opt-in OpenAI/Codex commands. Protocol 2 also exercises the real three-app services/actions. Offline verification is separate from live acceptance. Local inference was stopped after host instability and must not be restarted on Tory's current machine. OpenAI/Codex paid calls remain deferred. Earlier partial attempts do not certify the final source or the six-workflow gate.

The following table is the preserved October 30 historical observation, not a current protocol-2 result.

Recorded 2026-10-30 (America/Edmonton). Ordinary CI uses deterministic HTTP/process/model substitutes and never invokes a paid model. Tory explicitly deferred live OpenAI verification. No API key was created and no model was downloaded; local attempts used already-installed models.

| Connection | Implemented path | Observed live result |
| --- | --- | --- |
| Ollama | OpenAI-compatible Chat Completions over IChatClient | Local 0.30.10, llama3.2:latest: native-tool profile made no successful C# reads; preloaded profile read C# data but produced rejected documents. No three-app pass. |
| LM Studio | OpenAI-compatible Chat Completions over IChatClient | App 0.4.23.0, CLI commit 07b7252, google/gemma-4-e4b@q4_k_m loaded as intentblazor-evaluation with 16,384-token context: native profile made no successful C# reads. Preloaded news passed its full workflow; staff timed out and assets failed its follow-up. No three-app pass. |
| OpenAI | Official .NET client over IChatClient | Live verification deferred by maintainer; no account/model access or inference verified. |
| Codex CLI | Noninteractive exec, schema output, isolated workspace, preloaded data | CLI 0.154.0 help/version inspected only; no live inference. It does not call this desktop chat or share its memory. |

Ollama model digest: `a80c4f17acd55265feec403c7aef86be0c25983ab279d83f3bcd3abbcb5b8b72`. LM Studio main GGUF SHA256: `3F72A20A06F626C78E6C475AE07A64C88B2663149C0F6197B56BF7CF1F37585C` (`gemma-4-E4B-it-Q4_K_M.gguf`); no vision requests were made. The original JSON reports record the CLI version string; the running app file version was checked separately. These identities describe these attempts, not all models offered by either server.

## What the evaluation proves

The [sanitized reports](../verification/providers/README.md) preserve the original source fingerprint, timestamp, budgets, service calls, usage and failures. These attempts predate the final candidate tree and must not be relabelled as current-source certification. The evaluator separates `native-tools` from `preloaded` data access. Preloading proves the C# service path, not native model tool invocation.

Each profile uses synthetic news, staff and asset fixtures; 120 seconds per submission, at most six model calls, eight read calls, one correction, 6,000 output tokens, 48,000 reported tokens and 60,000 context characters, temperature 0.1 and a structured envelope. A passing scenario requires accepted initial and follow-up documents, at least two observed C# reads, a usable selection, a protected direct edit and selection retained through follow-up, and a rendered AiOutput. Renderer and local interaction timing is separate from model latency.

After building the solution, the opt-in loopback evaluator runs as follows:

~~~powershell
dotnet tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll model-local . Ollama 'already-installed-model' 'recorded-digest' 'recorded-server-version' http://127.0.0.1:11434/v1/ artifacts/phase7/evidence/ollama.json --allow-local
~~~

Use provider `"LM Studio"` and its loopback endpoint for that server. Append `preloaded` to evaluate C# preload instead of native read tools; use a separate report path. Optional `plain` selects JSON-object mode instead of a JSON-schema envelope without changing document validation. Explicit loopback endpoints cannot contain credentials/query strings. Do not use this local runner for cloud models or mislabel placeholder identities as evaluations. Do not run these commands on Tory's current machine.

## Optional compatibility reports

Scripted tests cover streaming, model IDs, base paths, envelopes, explicit/attribute data tools, bounded corrections, cancellation, failures, unavailable providers and preservation of accepted output. They establish adapter contracts, not model capability. Codex reports completed text rather than incremental document text, supports preloaded data only and has different sampling/output controls.

Application developers choose their model, hardware, provider options and application acceptance checks. Contributors may supply exact-profile reports voluntarily; no local, OpenAI or Codex inference is needed for framework publication. A connection probe or model listing cannot establish tool/workflow compatibility. The [candidate readiness record](phase-7-readiness.md) separates optional observations from mandatory framework/package/account/host checks.
