# Provider evaluation matrix

Last implementation check: 2026-10-29. The maintainer explicitly deferred live API verification. No inference request was made, no key created, and no model downloaded.

| Connection | Implemented path | Deterministic evidence | Real model result |
| --- | --- | --- | --- |
| Ollama | OpenAI-compatible Chat Completions over IChatClient | Streaming request, model ID, base path, envelope, validated output | Deferred; no model/digest recorded |
| LM Studio | OpenAI-compatible Chat Completions over IChatClient | Streaming request, model ID, base path, envelope, validated output | Deferred; no model/version recorded |
| OpenAI | Official .NET client over IChatClient | Streaming request, credential destination, structured envelope, 401/429/500 failure preservation | Deferred; no account/model access verified |
| Codex CLI | Noninteractive exec with schema output and isolated workspace | Scripted JSON events, completion/failure checks, usage, registration, preload, process argument isolation | Deferred; CLI 0.154.0 help/version inspected only |

These tests prove adapter behavior with controlled substitutes, not model capability or successful real tool use. Shared tests cover data-tool invocation, explicit/attribute registration, document composition, correction, limits, scoped data, and output preservation. Codex supports preloaded data only, and reports completed text rather than incremental document text. Its output-token/temperature controls differ from the HTTP adapters.

Before declaring a profile verified, record the provider/server version, exact model identifier and revision/digest where available, configuration, date, task, service calls actually observed, accepted document, local interaction result, reported usage, and failures. Run the same synthetic news scenario first, then the staff and asset fixtures with host-owned data. A successful model listing or connection probe is insufficient.

The Phase 2 live gate requires at least one real local run and one real frontier run with an interactive list/table and a real C# data-service result. Verify Ollama and LM Studio independently. Key setup and resource use require the maintainer's separate authorization; ordinary CI stays deterministic and credential-free.
