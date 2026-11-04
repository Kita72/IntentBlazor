# Provider workflow evaluation

Foundation Step 2 preserves default `<AiInput />` and `<AiOutput />`. Provider connections and application services are registered on the server; no new component parameters or page wrapper are required. [Decision 0011](architecture/0011-provider-workflow-evaluation.md) describes the generation fixes and evaluation boundary.

## Generation behavior

The runtime loads authorized current-request data before constraining presentation. When an existing workspace references datasets that have not been loaded, the HTTP model must choose registered read tools without a presentation JSON grammar. Once the datasets are available, the configured presentation format is restored. A fresh workspace may choose tools automatically. Host-preloaded data and model-initiated reads remain separate paths.

Host context precedes the final user request and omits irrelevant default node fields. Toolkit instructions describe property types and interactive behavior without a canned greeting to copy. A correction receives the accepted C# data again because the agent's temporary transcript is not the caller's message list. Output parsing, semantic validation, registered tool checks, protected edits, cancellation, timeout and budgets still apply.

Ollama and LM Studio request `{ "document": { ... } }`, so a valid outer response also contains syntactically valid presentation JSON. This avoids malformed JSON hidden inside an escaped `documentJson` string. Their envelope schema permits dynamic document keys; the strict C# parser and semantic validator still decide acceptance. OpenAI and Codex retain `{ "documentJson": "..." }` for their stricter schema integrations. Local connections also accept that earlier string envelope for compatible custom clients. `ModelProfile.UseDocumentObject` selects object delivery on a custom connection; it grants no data or action authority.

`ModelRunReport.Validation` contains bounded attempt numbers and error codes. It does not contain rejected output, parser messages, records or reasoning. HTTP evaluation measurements record initial/follow-up request character counts and the streaming flag; these are serialized message/tool sizes, not token estimates. Provider-reported usage remains nullable. Codex does not expose its internal HTTP request through this instrumentation.

## Six checks per profile

Each complete evaluation runs all three shared fixtures and the three real example-service workflows. A successful fixture requires accepted initial/follow-up documents, actual successful C# reads, selection, a protected edit and rendered default AiOutput. The real app workflows additionally require:

| Application | Required service/action results |
| --- | --- |
| Gaming News | FixtureNewsSource and private NewsLibrary data; source attribution, selected article and comparison draft; explicit bookmark/comparison actions and actual saved records. |
| Staff Business | EF Core jobs/staff/history; selected job and exact notes draft; a peer save, conflicting action that retains the draft, reader denial, then an explicit reviewed refresh/rebase and successful save. |
| Asset Workbench | Owned sample PNG metadata; selected asset and dimension draft; registered resize, actual 16×24 PNG bytes, derived-parent provenance, private download URL and byte-for-byte retained original. |

The evaluator uses fresh private temporary SQLite/artifact storage and synthetic identities. It deletes only its own validated temporary directory. Models receive read-only data tools; writes occur only through explicit evaluator action requests and existing server authorization/version checks. No external news feed, staff database or user asset is used.

Protocol `2` requires all six results for a complete optional profile report. A greeting, an incomplete action checklist, a failed follow-up or a partial diagnostic run cannot pass that report. [Decision 0012](architecture/0012-framework-verification-and-optional-model-reports.md) makes live reports optional developer diagnostics: no local, OpenAI or Codex inference is needed for framework publication. Readiness lists provider/native-tool observations separately from mandatory framework/package/account/host gates. Preload success cannot supply native-tool evidence; Codex remains experimental and preload-only.

## Local evaluation

**Maintainer deferral, November 1, 2026:** do not start Ollama, LM Studio or other local inference on Tory's current machine. Local testing was stopped following host instability. The commands below are procedures for a later approved run on a suitable host, not instructions to resume current-machine testing. Offline HTTP/process/model substitutes remain available for development.

Build with `dotnet build IntentBlazor.slnx -c Release`. Start a trusted loopback model server and load an already-installed local model. Record its exact digest/file SHA256, server/app version, CLI version, context and placement. Do not select cloud aliases through a local endpoint. Run from the repository root:

~~~powershell
dotnet tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll model-local . Ollama 'installed-model' 'recorded-digest' 'recorded-server-version' http://127.0.0.1:11434/v1/ artifacts/provider-evaluation/ollama.json --allow-local
dotnet tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll model-local . 'LM Studio' 'loaded-model-id' 'recorded-GGUF-SHA256' 'recorded-app-and-cli-version' http://127.0.0.1:1234/v1/ artifacts/provider-evaluation/lmstudio.json --allow-local
~~~

Append `preloaded` for an independent host-data profile. Append `plain` to request JSON-object mode instead of a JSON-schema envelope; validation remains strict. Append `scenario=staff` (or `news`/`assets`) for just that fixture and its app-service workflow. A partial run writes observations but exits `1` because it cannot meet the complete profile-report criteria. Unknown options/scenarios are rejected.

Without `--allow-local`, the CLI rejects the command before contacting a model server. This switch does not override the maintainer's current-machine deferral; a future live run still needs an approved suitable host.

Defaults remain 120 seconds per submission, six model calls, eight read calls, one correction, 6,000 output tokens, 48,000 reported tokens, 60,000 context characters and local temperature 0.1. Timing measures a complete request, not first streamed tokens; `timeToAcceptedOutputMilliseconds` is null for rejected output. Final JSON is rendered only after acceptance. Do not raise budgets to conceal invalid generation.

Use a new report path for each profile/run. Wait for evaluation completion before rebuilding the same output directory, or run a separate copied build. `partial-evaluation.json` preserves completed observations after each stage; missing stages are not passes. Exit `0` means every required workflow passed. Exit `1` means incomplete/failed evaluation or an input/connection error, with no release authorization.

## OpenAI: implemented, live calls deferred

Tory has deferred paid inference. Ordinary CI uses scripted handlers against the actual .NET SDK; it needs no account, key or network model. A future live run needs Tory's approved model/configuration/budget and a host-managed `OPENAI_API_KEY`. Never paste a key into chat, command arguments or reports.

After that separate approval, the explicit command is:

~~~powershell
dotnet tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll model-openai . 'approved-exact-model-id' 'recorded-model-revision' 'recorded-API-version-or-observation-label' artifacts/provider-evaluation/openai.json --allow-paid
~~~

The endpoint is fixed to `https://api.openai.com/v1/`; the evaluator offers no endpoint override that could receive the key. Without `--allow-paid`, it rejects the request before reading a credential or starting inference. Temperature is omitted for OpenAI so the evaluator does not assume the selected model accepts custom sampling. All framework output/call/time limits remain in effect; choose a model compatible with the registered adapter's declared capabilities. `preloaded` and `scenario=...` are diagnostic alternatives with the same reporting rules.

## Codex: implemented, live calls deferred

This is the supported independent noninteractive CLI integration, not a connection to the current desktop chat. It receives preloaded synthetic C# data, creates an ephemeral read-only temporary workspace and requests a schema-constrained final response. It does not resume this chat or import its memory. See the official [noninteractive CLI documentation](https://learn.chatgpt.com/docs/non-interactive-mode) and [configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference).

After separate live approval, use the installed native executable, not a PowerShell/npm shell shim:

~~~powershell
dotnet tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll model-codex . 'approved-model-id' 'recorded-model-revision' 'recorded-CLI-version' 'C:/trusted-installation/codex.exe' artifacts/provider-evaluation/codex.json --allow-paid
~~~

Without `--allow-paid`, no process is started. Shell tools, apps, memories, plugins, host skill discovery, web search and AGENTS loading are disabled for the independent request. Existing global configuration is not edited. CLI help/features are inspected separately from paid inference; the recorded baseline is 0.154.0. Future CLI changes can require adapter updates.

Native C# function calling, incremental document text, custom sampling and a provider-side output-token limit are unsupported by this adapter. The runtime still bounds elapsed time, model calls, context/event/output sizes and usage when reported. Cancelling the local process is not proof that remote generation or billing stopped. Codex does not supply native-tool evidence. It is offered as an experimental adapter without live certification; its live evaluation is optional.

## Evidence and remaining work

Use the [capability template](contributing/provider-capability-report.md) with the generated report and exact profile settings. Keep raw prompts, rejected model text, reasoning, credentials and generated documents out of committed evidence. [Provider observations](provider-evaluation.md) distinguish historical runs, current attempts, deterministic tests and deferred paid checks. The [foundation plan](foundation-completion-plan.md) retains browser, production-host, compatibility and publication work as separate steps.

Step 2's framework implementation and offline verification are complete. Live model evaluation is optional compatibility work for developers and contributors, not an outstanding publication task. Do not start local services on Tory's machine or run paid inference. Browser automation, production-host integration, compatibility and publication work remain in the foundation plan.
