# Fictional provider capability report

**FICTIONAL EXAMPLE — NOT LIVE EVIDENCE.** Every provider, version, timestamp, count and result below is invented to show how to fill the [report template](provider-capability-report.md). No model was contacted. This example must not be placed in verification/providers, converted into a passing JSON report, or cited as IntentBlazor compatibility.

## Identity and configuration

| Field | Fictional value |
| --- | --- |
| Report kind and overall outcome | Fictional illustration; partial workflow, not release-ready |
| Date/time | 2026-10-01 12:00 UTC, invented |
| Framework/source | 0.0.0-fictional; FICTIONAL-NOT-A-COMMIT |
| Fingerprint/protocol | FICTIONAL-NOT-A-FINGERPRINT; illustration-only |
| Schema/toolkit | Illustrative schema 0.1, builtin 0.1 |
| Backend/endpoint type | FictionalLocalServer 0.0.0-example; loopback HTTP Chat Completions route /v1/; no real endpoint |
| Client/runtime | Imaginary.Client 0.0.0-example; .NET/OS not measured |
| Model/revision | imaginary-chat-4b; revision not exposed by this invented backend |
| Local configuration | Fictional Q4; configured model context 16,384 tokens; no measured hardware |
| Task/data | Preloaded C# data; synthetic news fixture with an added evaluation note |
| Procedure | Invented observation only; no executable evaluation command |
| Generation options | Temperature 0.1; plain-text envelope; other sampling options not tested |
| Budgets | 120 seconds/submission; six model calls; eight tool calls; one correction; 6,000 output tokens; 60,000 framework context characters |

## Capability observations

All observations in this table are fictional, including rows labelled Passed.

| Capability | Illustrative outcome | Fictional observation |
| --- | --- | --- |
| Connectivity | Passed | The invented server returned a successful transport response. This establishes nothing about tools. |
| Streaming | Passed | The invented client observed five content chunks and a completion event; it did not render incomplete JSON. |
| Native read tools | Unsupported | The invented adapter's capability declaration excludes native tools for this profile; no native request was attempted. Zero service invocations alone would not justify this label. |
| C# preload | Passed | The initial/follow-up pair called its authorized synthetic source once per submission, two reads for that pair. These are not native model tool calls; separate cancellation/retry cases are outside this pair's counts. |
| Initial presentation | Passed | A complete documentJson envelope passed parsing/validation and rendered a news list and note editor. |
| Local selection/edit | Passed | Selecting one synthetic article and editing evaluationDraft changed local state without a model request. |
| Follow-up presentation | Failed | The invented follow-up and its one allowed correction both produced malformed envelopes. The run ended with invalid-output. |
| Failure preservation | Passed | The previous accepted document, selected article and direct note edit remained; failed output did not replace them. |
| Cancellation | Passed | The fictional client cancelled a separate request and accepted a subsequent valid request. Whether backend generation/billing stopped was not observed. |
| Timeout | Not tested | No deadline-exceeding request was illustrated. |
| Context/token enforcement | Not tested | Limits were configured but no boundary case was illustrated. |
| Business writes and vision | Not tested | The fictional task used synthetic read data and text; no registered write or image pixel payload was exercised. |

## Scenarios and usage

| Scenario | Initial | Follow-up | Workflow result |
| --- | --- | --- | --- |
| News preload | Passed | Failed | Failed; selection and draft retained after rejection |
| Staff | Not tested | Not tested | No capability claim |
| Assets | Not tested | Not tested | No capability claim |

The fictional initial response reported 980 input tokens, 250 output tokens and a provider total of 1,230. Follow-up and cancelled-request usage were Not reported; their token usage is not zero. The invented first accepted/rendered output took 2.4 seconds from submission. Local interaction timing and context character counts were not measured. These invented single-run values are neither a benchmark nor cost evidence.

## Conclusion

The illustration shows how connectivity, streaming, a valid initial document and a protected draft can coexist with an unusable follow-up and unsupported native tool mode. Only the stated preload news scope is illustrated, and its complete workflow failed. A maintainer would retain the failures, test unexercised cases and obtain actual current-source evidence before making a compatibility or release claim. See the [real provider matrix](../provider-evaluation.md) for observed results.
