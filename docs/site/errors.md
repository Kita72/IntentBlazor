# Errors and recovery

Invalid proposals and failed operations keep the last valid workspace available. `OperationResult` exposes `Success` plus `Errors`, each a `DocumentError(Code, Path, Message)`. AiOutput displays current session errors. Hosts calling session APIs directly must check the result rather than assuming a write succeeded.

The complete [framework error code index](error-codes.md) is generated from validator/session/setup failure call sites and checked during verification. Application-defined renderer contracts and business services can return additional errors. Messages and paths add detail; do not parse English message text as an API.

## Parse and validation failures

`invalid-json` covers malformed JSON, duplicates, unknown members/enums, excessive nesting or serialization bounds at the session boundary. `schema-version`, `toolkit`, tree/data/state/binding/event codes, and schema 0.3 extension/chart codes describe semantic failures. Fix the proposed document using its declared host data and installed capabilities. Do not retry an invalid proposal by disabling validation.

`OutputJson.ReadDocument` and `ReadContent` throw `JsonException` for parse failures. `DocumentValidator.Validate` returns semantic errors. `AiSession.OpenJson`, `Open`, and `ApplyUpdate` translate failures into operation results and retain the current document/state/revision.

The explorer shows structured codes and paths next to the editable JSON. It validates against the current fixture's read-only host datasets. Its additional `editor-dirty` notice blocks fixture replacement while JSON edits are pending; `fixture` rejects unknown fixture names. Restoring JSON is an explicit editor reset, not an output draft discard or a business save.

## State and concurrency failures

`dirty-state` means an operation would discard, hide, invalidate, or overwrite direct edits. Let the user keep editing, clear the draft explicitly, or submit a compatible follow-up. `stale-revision` means the event/response belongs to an older session revision; rerender current state before requesting another operation. Do not silently replay a stale write.

Input, selection, visibility, sort, record, value, and effect errors reject that operation atomically. Extension event errors reject malformed/oversized messages, unavailable sources, undeclared targets, or incompatible effects.

## Persistence failures

Owner/access failures require trusted identity and current resource authorization. `workspace-unavailable` does not authorize falling back to another user's checkpoint. `invalid-checkpoint` or `incompatible-workspace` requires host investigation and an explicit supported migration; the framework does not silently rewrite saved content. A checkpoint conflict retains the local draft for review, reconnect, or a private fork.

Inspect `AiSession.PersistenceError`, store results, and the [workspace guide](../phase-3-workspaces.md). Keep checkpoint sequence, UI revision, and business-record version separate.

## Every action outcome

Actions return `ActionResult(Status, Message)` rather than a `DocumentError` for every domain outcome:

| ActionStatus | Meaning and response |
| --- | --- |
| Succeeded | The registered service reported success. Only actual accepted fields/data are acknowledged. |
| Conflict | The business version changed. Retain the private draft, reload/review current data, and submit a new explicit action if appropriate. |
| Rejected | Invalid, stale, replayed, hidden, unauthorized, unavailable, or otherwise prohibited operation. No permission is implied by the document. |
| Failed | A known service failure. Keep the draft and inspect the host's safe diagnostics. |
| Indeterminate | The result cannot be safely established. Inspect the stored receipt/domain record before a new action; do not automatically retry. |

`PrepareAction` can return null when a valid issued request cannot be created. `ExecuteActionAsync` rechecks ownership, visibility, authorization, expected versions, idempotency, and actual results. See [the full action contract](../../specs/output-document/v0.2-actions.md).

## Model, content, cancellation, and setup

The input processor normalizes provider, tool, invalid-output, timeout, output/context/call/token-budget, and content failures into rejected/failed input outcomes without rendering partial JSON. `InputRejectedException` supplies a safe caller message; arbitrary exception details are not a user-facing transcript. Cancellation retains current output. `InputProgress` and model reports contain safe stage/usage/tool observations, not credentials or hidden reasoning.

Content adapters throw `ContentRejectedException` for unsupported or invalid files. File-size, count, pixel, PDF-page, structured-data, identity, and version limits remain enforced even without a model. Delivery reauthorizes each requested artifact; a missing resource is not permission to expose a path.

`setup-busy`, `setup-denied`, `setup-budget`, `setup-unsupported`, and `setup-invalid` reject a setup change outside the current policy, capability profile, revision, or bounds. A presentation probe reports connectivity, presentation validity, and unverified tool-use status separately. [AiSetup](../phase-4-content-and-setup.md) never certifies a model from one probe.
