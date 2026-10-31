# Provider evaluation matrix

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
dotnet tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll model-local . Ollama 'already-installed-model' 'recorded-digest' 'recorded-server-version' http://127.0.0.1:11434/v1/ artifacts/phase7/evidence/ollama.json
~~~

Use provider `"LM Studio"` and its loopback endpoint for that server. Append `preloaded` to evaluate C# preload instead of native read tools; use a separate report path. Optional `plain` disables the requested structured response format without changing document validation. Explicit loopback endpoints cannot contain credentials/query strings. Do not use this local runner for cloud models or mislabel placeholder identities as evaluations.

## Remaining live gate

Scripted tests cover streaming, model IDs, base paths, envelopes, explicit/attribute data tools, bounded corrections, cancellation, failures, unavailable providers and preservation of accepted output. They establish adapter contracts, not model capability. Codex reports completed text rather than incremental document text, supports preloaded data only and has different sampling/output controls.

Before publication, record a current successful local and frontier interactive workflow using actual C# service data, and evaluate Ollama and LM Studio independently. Release readiness requires successful current news/staff/assets evidence for each declared HTTP provider profile; no profile currently meets that gate. Keep live OpenAI deferred until Tory chooses to resume it or explicitly revises the release scope. A connection probe or model listing is insufficient. See the [candidate readiness record](phase-7-readiness.md).
