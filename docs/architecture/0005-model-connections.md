# Model connections and host-owned data

Status: implemented adapters; live verification deferred by maintainer direction.

## Decision

Add an optional IntentBlazor.AI package, keeping Core, Runtime, and the two components provider independent. Register one IChatClient connection and a scoped ModelInputProcessor behind the existing input seam. Use Microsoft.Extensions.AI and Microsoft Agent Framework for the HTTP model/function path. Ollama, LM Studio, and OpenAI share the official OpenAI client with explicit endpoints.

Opt-in attribute and explicit registration produce the same read-only dataset tools, parameter/result schemas, scoped service resolution, and validation. An optional host data source can preload current-user data. Model output contains only a presentation document; the runtime attaches authorized host data rather than accepting model-supplied rows.

Constrain final output with a small documentJson envelope. The contained document uses schema 0.1 and the existing semantic validator. Bounded correction, model/data call counts, context size, output size, cancellation, timeout, reported usage, dirty-state guards, and final revision checks protect generation. Streaming reports safe progress without rendering partial JSON.

Support the documented Codex noninteractive CLI as an experimental workstation adapter. It receives preloaded datasets, uses a fresh temporary workspace and independent ephemeral CLI request, and does not resume or access the current desktop conversation. Its narrower capabilities are explicit in ModelProfile. Native app-server tools and shared production authentication are separate future work.

## Consequences and evidence

Consuming pages retain AiInput and AiOutput with no provider parameters. Server configuration and application services carry integration details. Anonymous sample hosts allow model mode only in Development. Fixture demos remain separate and credential-free.

Deterministic tests exercise real SDK HTTP requests against scripted handlers, Agent Framework service invocation, Codex event parsing, all error/limit paths, and the existing renderer. Live provider/model access was explicitly deferred; Phase 2's live acceptance gate remains open. No published package or production-hosting compatibility is claimed.
