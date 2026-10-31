# Provider capability report template

Use this template to report one exact provider/model/configuration and the tasks actually exercised. A report can be partial or negative. Adapter registration, a reachable server and a model listing are separate observations from usable tool calls, accepted documents and interactive workflows. Existing results are in the [provider matrix](../provider-evaluation.md); the [fictional example](provider-capability-report-example.md) illustrates this format without claiming compatibility.

Copy the Report section into your contribution or issue, replace bracketed fields, and keep unexercised rows marked Not tested. Use a separate report for a different model revision, endpoint/backend, native-tools versus preload mode, structured-output setting or materially different budget. Record whether evidence came from a real model, a scripted substitute or static inspection. Only actual live observations can support a live capability claim.

## Safe reporting and outcomes

Reports and issue attachments are public. Do not include API keys, authorization headers, cookies, connection strings, private endpoints/query tokens, account/project identifiers, publish profiles, personal file paths, private prompts/documents, raw tool records or model reasoning. Use synthetic fixture IDs, row counts, validation codes and sanitized request shape. Share a public model identifier/digest where available; say Not exposed if a backend does not provide it. Do not invent a revision.

Use these outcomes consistently:

| Outcome | Meaning |
| --- | --- |
| Passed | The named behavior was exercised and its required outcome was observed for this configuration. State the scope. |
| Failed | The behavior was attempted and did not meet the required outcome, including a timeout or invalid proposal. Keep the failure. |
| Unsupported | A documented adapter boundary or explicit backend response excludes this capability for this configuration. Record that basis. Zero observed calls alone does not prove unsupported tools. |
| Not tested | No observation establishes the outcome. This includes claims based only on a model name, advertised feature or connection probe. |
| Deferred | Evaluation was deliberately postponed; record the reason and decision needed to resume. |

Unknown token usage is Not reported, not zero. Character counts and context limits are not token measurements. Mark any separate estimate explicitly and keep it out of reported usage totals. Cancellation of a client request does not prove the backend stopped generation or billing.

## Report

### Identity and reproducibility

| Field | Observation |
| --- | --- |
| Report title and kind | [Profile/task; real-model observation, scripted test or static inspection] |
| Date, time and timezone | [When the run occurred, including timezone] |
| Framework version and source revision | [Package version and full commit; note local modifications] |
| Source fingerprint and evaluator protocol | [Actual generated values if using the harness; otherwise Not recorded] |
| Output schema and required toolkit/packs | [Exact versions used] |
| Provider/backend and endpoint type | [Ollama, LM Studio, OpenAI, Codex or other; local loopback HTTP, HTTPS Chat Completions, CLI, etc.; sanitized route shape only] |
| Backend/server/app and CLI versions | [Record app and CLI separately where applicable] |
| Client/runtime versions | [SDK/client package versions, .NET SDK/runtime, OS/process architecture] |
| Model identifier and revision | [Exact requested model ID, resolved model/file name, digest/revision if exposed] |
| Local execution configuration | [Quantization, configured model context, CPU/GPU details relevant to reproduction; omit machine/account identifiers] |
| Application/data mode | [Authored fixture or named app; synthetic source; native-tools, preloaded C# data, or both] |
| Evaluation command or procedure | [Reproducible command without secrets, or explicit public steps] |

### Generation and request budgets

| Setting | Configured value and observed limitation |
| --- | --- |
| Temperature, top-p, penalties, seed/reasoning option | [Only settings sent or supported; distinguish requested from effective/ignored] |
| Structured response format | [Requested JSON/schema or plain text; backend acceptance/response; presentation envelope version] |
| Maximum output tokens | [Requested limit and backend behavior if observed] |
| Context window and framework context limit | [Model/server token window separately from framework character/turn limits] |
| Model/tool calls and correction budget | [Configured maxima, plus actual observed counts] |
| Timeout and cancellation policy | [Deadline, cancellation mechanism and applicable CLI/process restrictions] |
| Reported-token/request limits | [Configured limits, missing-usage behavior and whether enforcement was exercised] |
| Input/content scope | [Fixture IDs, bounded row counts, attachment types and metadata versus image pixels; no private content] |

### Capability observations

For each attempted row, record the exact task, an observable result and sanitized evidence. Keep initial request and follow-up outcomes separate. HTTP/CLI transport streaming, framework progress metadata and final document acceptance are different capabilities; partial text is not automatically a renderable document.

| Capability | Outcome | Observation and evidence |
| --- | --- | --- |
| Endpoint/process connectivity | Not tested | [Actual response/exit outcome; a model listing alone is only discovery] |
| Incremental transport events/chunks | Not tested | [Observed stream/event type and completion; do not infer from a request flag] |
| Framework progress and completion | Not tested | [Stages, bounded metadata, final status] |
| Native model read-tool request | Not tested | [Public tool name, request count and scoped C# invocation observed] |
| Read-tool result validation | Not tested | [Authorized dataset IDs/counts, accepted/refused result and code] |
| Host-preloaded C# data | Not tested | [Source calls/authorized scope; this does not establish native tool calling] |
| Structured presentation proposal | Not tested | [Requested envelope versus actual parse and semantic validation] |
| Accepted interactive document | Not tested | [Supported schema/toolkit, data binding and successful AiOutput render] |
| Local selection/sort/edit | Not tested | [Actual interaction and updated state, without another model request] |
| Follow-up draft and selection continuity | Not tested | [Stable identities, retained direct edit/selection and final revision] |
| Malformed/partial output recovery | Not tested | [Injected or observed failure, bounded correction, error code, retained output/draft] |
| Cancel and subsequent request | Not tested | [When cancelled, client/process outcome, retained workspace, next request] |
| Timeout and subsequent request | Not tested | [Deadline observed, final status, retained workspace, next request] |
| Call/context/output/token budgets | Not tested | [Boundary actually exercised and outcome; configured values alone are not an enforcement test] |
| Host scope and registered actions | Not tested | [Authorized/unauthorized synthetic case, explicit user activation, version/conflict outcome; generated UI cannot authorize a write] |
| Image/content capability, if claimed | Not tested | [Actual payload modality and result; imported image metadata is not a vision test] |

### Application scenarios

| Scenario | Initial/follow-up outcome | C# reads and interaction | Retention/recovery result |
| --- | --- | --- | --- |
| News | Not tested / Not tested | [Native versus preload calls; source-attributed output and selection] | [Draft/selection and failure preservation, if exercised] |
| Staff | Not tested / Not tested | [Authorized job/staff data; table/editor; confirmed action if exercised] | [Protected edit, conflicting save and no duplicate replay, if exercised] |
| Assets | Not tested / Not tested | [Authorized metadata; inspection/edit; registered resize/export if exercised] | [Working draft and original/derived provenance, if exercised] |

Do not mark all three passed because one fixture produced a valid card. An authored-fixture run and a real example-app service workflow must be identified separately. For the existing harness, its scenario result also checks accepted initial/follow-up documents, actual C# reads, local interaction, retained draft/selection and rendered output. A working preload profile does not certify an untested native-tools profile.

### Usage and timing

| Measurement | Initial request | Follow-up | Source/meaning |
| --- | --- | --- | --- |
| Input tokens | Not reported | Not reported | [Provider/event metadata only] |
| Output tokens | Not reported | Not reported | [Provider/event metadata only] |
| Total tokens | Not reported | Not reported | [Provider total if supplied; label a separately calculated sum] |
| First useful output / total submission elapsed | Not measured | Not measured | [Define when measurement starts and whether useful means accepted rendered output] |
| Local interaction time | Not measured | Not measured | [Browser, server HTML renderer or other boundary; sample count] |
| Context/serialized character count | Not measured | Not measured | [Name the measured representation; not an inferred token count] |

Record failed/cancelled attempts and whether their usage was available. A one-run time is an observation, not a latency guarantee or percentile. Do not infer monetary cost from a subscription, unknown usage or unverified prices.

### Conclusion and retained evidence

- **Observed working scope:** [Exactly which profile/tasks/capabilities passed.]
- **Failures and limits:** [Codes, timeout/correction outcomes, unsupported settings and effect on the user's workspace.]
- **Not tested or deferred:** [Explicit remaining tasks and any needed maintainer configuration/budget decision.]
- **Evidence:** [Commit/run links and sanitized reports/screenshots; synthetic data only.]
- **Overall result:** [Passing tested scope, partial, failed or not run. State whether required release criteria are still unmet.]

## Relationship to release verification

This Markdown report is a contributor-readable record, not an instruction to alter the automated verdict. Keep actual generated JSON reports separate and retain their original source fingerprint, identity, settings, timestamps and failures. Never manually set a passed flag or relabel an old report as a current run. Fictional examples and scripted clients cannot satisfy live acceptance.

The current opt-in evaluator runs local Ollama/LM Studio profiles; its [documented command and limits](../provider-evaluation.md) are distinct from planned OpenAI/Codex live evaluation. Follow the [readiness record](../phase-7-readiness.md) for the actual release gates and the [foundation plan](../foundation-completion-plan.md) for remaining work. Completing this template does not resume deferred inference or certify a provider.
