# State, bindings, and events

These are the supported public rules from [Core](../../src/IntentBlazor.Core/OutputDocument.cs) and [AiSession](../../src/IntentBlazor.Runtime/AiSession.cs). There is no expression language, property traversal, generated code, or implicit business write.

## Typed state

| kind | Initial value and constraints |
| --- | --- |
| text | String up to 4,096 characters. |
| number | JSON number fitting C# decimal, with optional inclusive `minimum` and `maximum`. |
| boolean | JSON true or false. |
| recordReference | Existing text key in the declared `dataSetId`. |

All state is non-null by default. Set `nullable:true` to permit null, including an initially unselected record. Initial values are validated against the same rules as later effects. A stable state key preserves its meaning across follow-ups; do not reuse one key for a different record or field.

Text and number inputs mark state dirty relative to the local baseline. A select input edits a record reference. Selection effects update explicit targets atomically from the same pre-event snapshot; they cannot overwrite dirty targets. `clearDrafts:true` restores dirty values to the current baseline. It does not save anything.

## Every binding source

| source | JSON members | Valid use |
| --- | --- | --- |
| literal | `literal` | Scalar string/number/boolean/null. Arrays and objects are unsupported. C# helper: `ValueBinding.Constant(value)`. |
| state | `stateKey` | Existing named state. Helper: `FromState(key)`. |
| selectedField | `stateKey`, `dataSetId`, `field` | The state must be a record reference to the same dataset. Reads the selected authorized row's declared field; no selection gives null. Helper: `Selected(key,set,field)`. |
| eventValue | None beyond source | Selected row key, only inside list/table/gallery select assignments. Helper: `EventValue()`. |
| eventField | `dataSetId`, `field` | A declared field of the selected row from that event source's dataset, only in select assignments. Helper: `EventField(set,field)`. |

Event bindings cannot drive ordinary node values, activate assignments, or action arguments. Assignment types must match their targets; the runtime also verifies actual record references, nullability, text bounds, and numeric ranges.

## Every event kind

| UiEventKind | Runtime behavior |
| --- | --- |
| select | A list, table, or gallery selects a host row and applies the named interaction's assignments. Rows must exist in the declared dataset. |
| change | Input/select editing through `Session.Change(nodeId, rawValue, expectedRevision)` or equivalent session dispatch. No document-defined change interaction. |
| activate | A visible button applies local assignments, clears drafts, or prepares and executes its registered action on explicit user activation. |
| sort | A table sorts one of its displayed fields using `SessionEvent.SortField` and `Descending`. No document-defined sort interaction. |

`OutputInteraction` declares only select or activate. It has an `id`, matching `event`, and either assignments, clear-draft, or a registered action. Assignment targets are unique and bounded to 16. Every declared interaction is attached to a matching node. A button must name one.

`SessionEvent` carries `NodeId`, `Kind`, `ExpectedRevision`, optional scalar `Value`, and sorting members. Runtime dispatch rechecks current visibility, event/node agreement, field references, values, and revision. A forged event cannot bypass the handler's authorization.

## Local selection example

~~~json
{
  "id": "choose",
  "event": "select",
  "assignments": [
    { "stateKey": "selected", "value": { "source": "eventValue" } },
    { "stateKey": "notes", "value": { "source": "eventField", "dataSetId": "jobs", "field": "notes" } }
  ]
}
~~~

This fragment assumes declared `selected` record-reference state, `notes` text state, authorized `jobs` data, and a list/table/gallery node with `interactionId:"choose"`. The complete [staff fixture](../../specs/fixtures/staff.json) demonstrates the surrounding document.

## Revisions, follow-ups, and drafts

`OutputDocument.Revision` is authored document metadata. `AiSession.Revision` is a monotonically increasing workspace counter used by events, async input, extensions, and actions. They are distinct from each other and from an application's business-record version.

`Open` validates and replaces a workspace, refusing dirty output by default. Explicit host `discardDraft:true` opts into discard. `ApplyUpdate` checks an expected session revision and reconciles compatible state while retaining visible dirty fields. Invalid, incompatible, or stale proposals keep the last valid output and draft. An asynchronous response prepared before another local change cannot overwrite that newer workspace.

Input and extension failures expose errors through AiOutput. A correct business action acknowledges only its successful edited fields; failed or conflicting saves do not acknowledge them. See [actions](../../specs/output-document/v0.2-actions.md) and [workspace recovery](../phase-3-workspaces.md).

## Extension events

`DispatchExtension<TEvent>(nodeId, expectedRevision, message)` uses the installed typed contract. Payloads are bounded to 4,096 characters; effects are bounded to 16 unique existing declared editable states. `IsDraft:true` marks a local user edit. Non-draft effects cannot overwrite dirty state. No extension event calls a business write handler. Follow the [compiled renderer tutorial](extensions.md).
