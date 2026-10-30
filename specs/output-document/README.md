# Output document specification

Status: implemented experimental **schema 0.1**, [schema 0.2 actions](v0.2-actions.md), and [schema 0.3 content/extensions](v0.3-content-and-extensions.md), toolkit **builtin 0.1**. This page describes the local 0.1 subset. The canonical contracts are [OutputDocument.cs](../../src/IntentBlazor.Core/OutputDocument.cs), [OutputJson.cs](../../src/IntentBlazor.Core/OutputJson.cs), and [DocumentValidator.cs](../../src/IntentBlazor.Core/DocumentValidator.cs). C# types plus the strict parser and semantic validator define these versions; a separate JSON Schema is not supplied. Earlier JSON sketches in the implementation plan are conceptual.

`OutputContent` pairs an `OutputDocument` with host-provided `Data`. Only the document is a presentation proposal. Dataset schemas and rows are supplied by the host after its own authorization; a model must never be allowed to create or authorize its own datasets. Fixtures bundle the pair for deterministic playback.

| Document member | Meaning |
| --- | --- |
| schemaVersion | Exact supported schema: 0.1 for local interactions, 0.2 for registered actions, 0.3 for content and extensions. |
| id / revision | Stable document identity and positive authored revision. |
| rootId / nodes | A single rooted tree with stable node IDs. |
| requires | Exact required renderer capabilities; builtin 0.1 and host-registered extension packs. |
| dataSets | IDs of datasets available from the host; no row data in the presentation document. |
| state | Named typed slots: text, number, boolean, or a record reference to one dataset. |
| interactions | Named local effects attached to a node, with a select or activate event. |

The initial node vocabulary is layout, card, text, list, table, rating, input, select, button, image, and form. Layout/card/form compose children; form groups editable fields without claiming an application save. `columns` requests a responsive grid. `whenState` conditions a subtree on boolean state or a non-null record selection. Images bind selected fields from host datasets and render only application-local root-relative sources. Host applications own media delivery and authorization.

Bindings have five explicit sources: literal scalar, named state, selected record field, selection event key, or selection event field. There is no expression language, arbitrary property traversal, HTML, Razor, JavaScript, or generated C# execution. Tables sort only displayed columns, using typed values and stable record-key tie breaking. Labels and scalar text are HTML encoded.

A local interaction either assigns up to 16 unique state targets atomically or clears current drafts. Assignments read the same pre-event snapshot. A selected key must exist in the declared host dataset. State types, numeric ranges, record references, event kind, visible source, and expected session revision are rechecked before any effect is applied. `action` is reserved and rejected in this phase.

An editable input changes a state slot and marks it dirty relative to its most recent local baseline. Selection effects cannot overwrite dirty targets; `Open` replacement also refuses a dirty workspace by default. Clear-draft restores that baseline. Explicit host `Open(..., discardDraft: true)` can discard it. Phase 3's `ApplyUpdate` reconciles compatible values/baselines while preserving visible dirty fields. Optional [durable checkpoints](../../docs/phase-3-workspaces.md) preserve this state; only registered application actions can acknowledge a business save.

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

All three [authored fixtures](../fixtures/README.md) retain schema 0.1. See the [developer guide](../../docs/phase-1-developer-guide.md), [authority decision](../../docs/architecture/0003-output-and-authority.md), and [Phase 1 decision](../../docs/architecture/0004-deterministic-output-v01.md). Phase 3 adds [registered actions](v0.2-actions.md), durable checkpoints, conversation context, and versioned artifact metadata references. Phase 4 adds [content and extensions](v0.3-content-and-extensions.md), optional file storage/adapters, and explicit presentation probes. Dataset paging, JSON Patch, and comprehensive provider capability certification remain future work.
