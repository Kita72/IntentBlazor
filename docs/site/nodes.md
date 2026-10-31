# Output node reference

The canonical types are [OutputDocument.cs](../../src/IntentBlazor.Core/OutputDocument.cs), [OutputJson.cs](../../src/IntentBlazor.Core/OutputJson.cs), and [DocumentValidator](../../src/IntentBlazor.Core/DocumentValidator.cs). JSON is case-sensitive camelCase with string enums. Unknown/duplicate properties, unknown or numeric enums, oversized content, and invalid graphs are rejected. The [explorer](../../apps/IntentBlazor.Docs/Components/Pages/Playground.razor) renders proposals against fixed host data.

## Document and host data

`OutputContent` contains `Document` plus a separate `Data` array. The host authorizes data before supplying it. A model returns presentation JSON only; dataset rows inside that proposal are not accepted.

| JSON member | Contract |
| --- | --- |
| schemaVersion | Exact `0.1` for local nodes, `0.2` for actions, or `0.3` for content/extensions. |
| id / revision | Identifier and positive authored revision. Distinct from the session event revision. |
| rootId / nodes | One rooted tree; all nodes reachable exactly once. No shared children or cycles. |
| requires | Exact capabilities, normally `builtin:0.1` plus required installed packs. Missing required versions reject the proposal. |
| dataSets | Up to 16 unique IDs, exactly matching host-supplied datasets. |
| state | Up to 64 named typed state definitions. See [bindings](bindings.md). |
| interactions | Up to 64 named select/activate effects or registered action proposals. Every interaction must be used. |

Host datasets have an `id`, text `keyField`, 1–32 declared scalar `fields`, and up to 250 rows with unique keys. Every row matches its schema; fields may be text, number, or boolean with explicit nullability. Keys are at most 128 characters. Use host data sources for filtering; arbitrary expressions, query URLs, paging, and model-created datasets are not supported.

## All node kinds

All nodes have a stable `id` and a `kind`. `title` is an encoded label up to 160 characters; interactive, media, and schema 0.3 content nodes require a nonempty title. Optional `whenState` names boolean or record-reference state, showing the node when true or non-null. Hidden ancestors also prevent dispatch from their descendants.

| kind | Required configuration | Behavior and restrictions |
| --- | --- | --- |
| layout | `children` | Responsive composition; `columns:true` requests columns. |
| card | `children` | Labelled container. May conditionally show a selected record's details. |
| form | `children` | Groups fields; does not imply a business save. |
| text | `value` | Encoded scalar output. Label may be empty. |
| list | `title`, `dataSetId`, text `labelField` | Host records. Optional `selectionState` and select `interactionId`. |
| table | `title`, `dataSetId`, `fields:[{field,label}]` | 1–16 unique declared columns. Typed local sort with stable record-key tie breaking. Optional record selection. |
| rating | `title`, numeric `value` | Read-only numeric display; `ratingMaximum` from 1–10, default 5. |
| input | `title`, `stateKey` | Edits text or numeric state, respecting type/range; marks a local draft. |
| select | `title`, `stateKey`, `dataSetId`, text `labelField` | Edits record-reference state for that dataset. Not an arbitrary option array. |
| button | `title`, activate `interactionId` | Local assignments, clear-draft, or a schema 0.2/0.3 registered action. The host owns action labels and checks. |
| image | `title`, `value` from `selectedField` | Uses a host-provided text image path. Only application-local root-relative URLs are allowed; no remote/data/script image sources. |
| markdown | `title`, text-compatible `value` | Schema 0.3. Safe built-in headings, paragraphs, and bullet blocks with encoded HTML; not a full HTML/Markdown execution engine. |
| chart | `title`, `dataSetId`, `chart:{kind,labelField,valueField}` | Schema 0.3. `bar` or `line`, up to 50 labelled numeric rows, values -1e9 to 1e9; textual data table alternative. |
| gallery | `title`, `dataSetId`, text `labelField`, text `imageField` | Schema 0.3. Host-local images with optional select interaction and selection state. |
| artifact | `title`, `value` naming an attached artifact ID | Schema 0.3. Reauthorized metadata; authenticated delivery rechecks identity. Value may be text or record-reference state. |
| extension | `title`, `extension:{pack,version,renderer,configuration,fallback}` | Schema 0.3. Installed typed contract/component only. May compose children when installed; unavailable optional nodes have text fallback and no children. |

`value` is a [ValueBinding](bindings.md), not a C# expression. Most scalar node values may also be null where their contract permits. Images specifically require selected-field binding. Lists and tables do not automatically assign a selection: declare a select interaction with explicit assignments. A select input updates its own `stateKey` through the normal change event.

## Composition example

This minimal presentation needs no datasets:

~~~json
{
  "schemaVersion": "0.1",
  "id": "welcome",
  "rootId": "card",
  "nodes": [
    { "id": "card", "kind": "card", "title": "Welcome", "children": ["message"] },
    { "id": "message", "kind": "text", "value": { "source": "literal", "literal": "What would you like to do?" } }
  ]
}
~~~

The default `requires` value is `builtin:0.1`; omitted collections are empty. For meaningful interactive compositions use the [three authored fixture documents](../../specs/fixtures/README.md) or the compiled [quickstart document](../../examples/Quickstart/CatalogInputProcessor.cs). The model can invent a new arrangement of these primitives, but a new primitive requires a [registered renderer](extensions.md).

## Bounds and failure behavior

Serialized document or complete content: 256 KiB UTF-8. JSON nesting: 32. Nodes: 1–128. Tree depth: 16 from root depth 0. State slots and interactions: 64 each. Assignments: 16 per interaction. Identifiers: 64 characters beginning with an ASCII letter, followed by letters, digits, underscore, or hyphen. Scalar text: 4,096 characters. Input request: 2,048 characters.

Extension configuration: 8,192 characters. Local extension event: 4,096 characters. Fallback: 1–500 characters. Required pack versions are exact; migrations never silently replace renderer versions.

Invalid output reports a [code, path, and message](errors.md), retaining the current document, state, and revision. Output documents are presentation proposals, never authorization. See [schema 0.2 actions](../../specs/output-document/v0.2-actions.md) and [schema 0.3](../../specs/output-document/v0.3-content-and-extensions.md) for their additional rules.
