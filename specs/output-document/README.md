# Output document specification

Status: implemented experimental **schema 0.1**, toolkit **builtin 0.1**. The canonical contracts are [OutputDocument.cs](../../src/IntentBlazor.Core/OutputDocument.cs), [OutputJson.cs](../../src/IntentBlazor.Core/OutputJson.cs), and [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs). C# types plus the strict parser and semantic validator define this version; a separate JSON Schema is not supplied. Earlier JSON sketches in the implementation plan are conceptual.

`OutputContent` pairs an `OutputDocument` with host-provided `Data`. Only the document is a presentation proposal. Dataset schemas and rows are supplied by the host after its own authorization; a model must never be allowed to create or authorize its own datasets. Fixtures bundle the pair for deterministic playback.

| Document member | Meaning |
| --- | --- |
| schemaVersion | Exact supported schema, currently 0.1. |
| id / revision | Stable document identity and positive authored revision. |
| rootId / nodes | A single rooted tree with stable node IDs. |
| requires | Exact required renderer capabilities; builtin 0.1 is currently the only toolkit. |
| dataSets | IDs of datasets available from the host; no row data in the presentation document. |
| state | Named typed slots: text, number, boolean, or a record reference to one dataset. |
| interactions | Named local effects attached to a node, with a select or activate event. |

The initial node vocabulary is layout, card, text, list, table, rating, input, select, button, image, and form. Layout/card/form compose children; form groups editable fields without claiming an application save. `columns` requests a responsive grid. `whenState` conditions a subtree on boolean state or a non-null record selection. Images bind selected fields from host datasets and render only application-local root-relative sources. Host applications own media delivery and authorization.

Bindings have five explicit sources: literal scalar, named state, selected record field, selection event key, or selection event field. There is no expression language, arbitrary property traversal, HTML, Razor, JavaScript, or generated C# execution. Tables sort only displayed columns, using typed values and stable record-key tie breaking. Labels and scalar text are HTML encoded.

A local interaction either assigns up to 16 unique state targets atomically or clears current drafts. Assignments read the same pre-event snapshot. A selected key must exist in the declared host dataset. State types, numeric ranges, record references, event kind, visible source, and expected session revision are rechecked before any effect is applied. `action` is reserved and rejected in this phase.

An editable input changes a state slot and marks it dirty relative to its most recent local baseline. Selection effects cannot overwrite dirty targets; document replacement also refuses a dirty workspace by default. Clear-draft restores that baseline. Explicit host `Open(..., discardDraft: true)` can discard it. This is in-memory UI state, not a database save or durable session.

`AiSession.Revision` is a monotonically increasing interaction counter across document replacements. It is separate from the authored `OutputDocument.Revision`. Events carry the session counter they were rendered against. Async input responses must still match the captured counter; stale responses leave the current workspace intact.

JSON uses case-sensitive camelCase property names and string enums. Unknown members, duplicate properties, unknown/number enum values, malformed JSON, and excessive sizes fail closed. Parsing checks syntax; `DocumentValidator.Validate` checks semantics. `AiSession.Open` copies and validates both before replacing its working content. Errors include code, path, and a developer-readable message; invalid proposals preserve the current document, state, and revision.

| Limit | Value |
| --- | --- |
| Serialized document or complete content | 256 KiB UTF-8 |
| JSON nesting | 32 |
| Nodes / tree depth from root at 0 | 128 / 16 |
| State slots / interactions | 64 / 64 |
| Datasets / fields per dataset / rows per dataset | 16 / 32 / 250 |
| Identifier / row key / node title | 64 / 128 / 160 characters |
| Scalar text / input request | 4096 / 2048 characters |
| In-memory input outcome history | Latest 20 turns |

All three [authored fixtures](../fixtures/README.md) use this contract. See the [developer guide](../../docs/phase-1-developer-guide.md), [authority decision](../../docs/architecture/0003-output-and-authority.md), and [Phase 1 decision](../../docs/architecture/0004-deterministic-output-v01.md). Dataset paging, patch updates, application actions, durable memory, artifact handles, extensions, and provider capability negotiation require later versions and are not implied by schema 0.1.
