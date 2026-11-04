# Keyboard and focus guide

This guide describes the current Interactive Server renderer and the checks contributors should perform when changing it. AiInput and AiOutput use native HTML controls; authors do not need to define keyboard handlers in an output document. State, bindings and supported event names remain those in the [binding reference](../site/bindings.md) and [output specification](../../specs/output-document/README.md).

## Control keys and semantics

Use Tab and Shift+Tab to enter, move between and leave enabled controls in document order. Disabled or conditionally hidden controls are unavailable to keyboard navigation. Keep the focus indicator visible in the consuming application's styles. Visual columns must preserve a meaningful reading and tab order; avoid positive tabindex values. See [WCAG focus order guidance](https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html).

| Control | Current rendering and keys | Effect |
| --- | --- | --- |
| AiInput request | Labelled textarea inside an EditForm. Tab leaves the field; Enter inserts a newline. Tab to Submit and use Enter or Space. | Submits a bounded request through the registered processor. There is no built-in Ctrl+Enter shortcut. Controls become available after interactive handlers are ready. |
| Selectable list | Each selectable record is a native button inside a list item. Tab visits buttons; Enter or Space activates the focused record. | Dispatches select with the record key. aria-pressed exposes the selected state. A list without an interaction displays text without record buttons. |
| Table | A named, focusable scroll region contains a captioned HTML table. Tab reaches the region, sort buttons and optional Select buttons. Activate a sort or Select button with Enter or Space. | Sorting updates local order and aria-sort. Selecting dispatches the record key. This is a table with buttons, not an ARIA grid with arrow-key cell navigation. |
| Text or numeric input | Labelled native input, with numeric min/max where declared. Use normal editing keys; Tab or another normal browser change/blur action commits a changed value. | Updates the declared state through change and marks a local draft. Binding uses the native change event, not every keystroke. Numeric step is any; an integer-only editor needs its own validated contract. |
| Select field | Labelled native select with Choose and authorized record options. Use the browser/OS selection keys and Tab to leave. | Updates a record-reference state through change. Arrow/type-ahead and popup behavior vary by browser and OS; there is no custom listbox keyboard implementation. |
| Form or card | Labelled fieldset and legend around children. The group adds no extra tab stop; its enabled controls determine the sequence. | A form node groups fields. It is not an EditForm, an automatic submit action, or an application save. |
| Local button | Native button with type button. Enter or Space activates it. | Dispatches activate for declared assignments or an explicit clear-draft interaction. It does not implicitly submit a surrounding form. |
| Application action button | Native button with the host's registered label; unavailable when its action cannot be prepared or the session is busy. | Explicit activation enters the registered action path, which rechecks authorization, workspace/record versions and the issued request. Keyboard activation grants no additional authority. |
| Rating | Labelled meter with a visible numeric value and maximum, or an unavailable-value message. | Read-only information. It has no rating-selection handler and is not an editing tab stop. |
| Gallery, chart and artifact | Gallery selection uses buttons; downloads use authorized links. Chart data opens through a native details/summary disclosure and contains a labelled focusable table region. | Test the actual control exposed by the renderer, including disclosure and download behavior. An image or SVG alone does not provide an editing operation. |

Native buttons support Enter and Space activation; keep the semantics when extending a renderer. The [WAI button pattern](https://www.w3.org/WAI/ARIA/apg/patterns/button/) explains the expected interaction. On a focused scroll region, use the browser's scrolling keys to reach overflowed columns; Tab must still leave the region. Do not intercept those keys to select records or trap focus.

## Read-only and editable ratings

The builtin rating node represents a measured value, using the HTML [meter element](https://html.spec.whatwg.org/multipage/form-elements.html#the-meter-element). The news fixture's Community rating is a display of host data. Clicking a star, pressing an arrow, or submitting a request does not edit that aggregate.

For an editable personal score, declare bounded numeric state and use an input node, or register an accessible typed renderer. The compiled [quickstart ScoreView](../../examples/Quickstart/Components/ScoreView.razor) demonstrates Decrease score and Increase score buttons with a politely announced output. Its [contract](../../examples/Quickstart/ScoreContract.cs) validates the range and effects. It is a custom score editor, not a builtin interactive rating widget. Editing local state still does not save a business record.

## Focus during document updates

Keep document IDs stable for the same workspace, node IDs stable for the same control, and row keys stable for the same record. AiOutput keys its output tree by document ID, child renderers key siblings by node ID, and lists/tables key records by their data key. Field labels target IDs derived from the session and node. These relationships help Blazor retain the matching elements while values or row order change. See [Microsoft's keyed-rendering guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/element-component-model-relationships?view=aspnetcore-10.0).

An unchanged keyed subtree can retain focus; keys do not implement a general focus-restoration service. Replacing the document ID, removing/reparenting a control, changing its kind, hiding it through whenState, or disabling the focused control can change browser focus. Selection does not automatically move focus into a newly shown detail form. A rejected proposal should retain the working tree, values and revision; retaining state is distinct from proving browser focus.

For a compatible follow-up, ApplyUpdate preserves visible dirty fields and requires the expected session revision. A late model response cannot replace a newer local edit. Open replaces a workspace and normally refuses dirty output; explicit draft clearing restores the accepted baseline. Keep IDs and compatible state definitions stable rather than rebuilding the entire tree for a label change.

If an application needs focus movement after a deliberate navigation or removal, the host or typed renderer owns that policy. Move focus only after the destination exists, choose a meaningful reachable destination, and verify it with a real browser. The default framework does not automatically focus the first new heading, the first invalid field, or a remembered field after circuit loss. Reconnecting a surviving circuit and restoring a durable workspace in a new circuit are different cases; [workspace recovery](../phase-3-workspaces.md) does not imply restoring a DOM element's focus.

## Errors and submission

AiInput validates its required request and length through EditForm and a ValidationMessage. Its textarea's Enter key remains text entry; use Submit for the request. Output input/select fields instead call the session's typed change path. Invalid type/range, stale revision, hidden source or unauthorized reference leaves accepted state intact and exposes errors in AiOutput's alert region. Application action failures/conflicts also appear there; successful action feedback and dirty/progress messages use status regions.

Output fields currently have labels and native bounds, but they do not each receive a dedicated inline error description or automatic invalid-field focus. The output form node has no automatic form validation summary. Check that the alert is discoverable and understandable with the target screen reader; markup assertions alone do not establish that an announcement occurred. Hosts adding inline errors must associate them with the correct control and preserve the framework's server validation.

Field editors are disabled during an application action; direct edits may still occur during a model request, which makes the final revision check important. Test the resulting focus and busy messages rather than assuming that every output control is disabled. A conflict or cancelled request must leave the user's accepted output and protected draft available. Do not automatically replay an uncertain write on reconnect.

## Synthetic fixture walkthroughs

Use the [document explorer](../site/quickstart.md) at /explorer in fixture mode, or run the [three standalone apps](../phase-5-example-apps.md). These checks need no model and use synthetic records. The explorer has no application write service; use Staff Business for the separate save/conflict scenario.

| Fixture | Keyboard check | Expected outcome |
| --- | --- | --- |
| Gaming News | Open news fixture. Tab to Example RPG patch 1.2, activate it, then move to Skyward Tactics beginner tips and activate it. | Correct details/source/rating appear; the selected record button exposes its state. Community rating is readable but not editable. Focus remains usable in the list rather than jumping into the detail card. |
| Staff Business | Open staff fixture. Focus the table's named scroll region, reach Sort by Priority, activate it, and choose Select Cinderworks. Tab through Follow-up owner and Follow-up notes; change a value and Tab out. | Order/selection update correctly. A native edit creates a local draft and the preview reflects accepted state. The Select control remains associated with its record after sorting. |
| Staff follow-up and errors | With the note dirty, submit staff through AiInput. Attempt a fixture replacement, then clear only the disposable draft. Use Try invalid schema after clearing. | A compatible follow-up retains the selected record and direct edit. Replacing dirty output is refused. The invalid schema keeps the working output and exposes a readable error; JSON can be restored explicitly. |
| Asset Workbench | Open assets fixture. Activate Crate sprite, Tab to Working label, edit it and Tab out. Attempt a conflicting selection/replacement before explicitly clearing the disposable draft. | The selected sprite/dimensions appear, local label changes remain protected, and the original host-provided image is unaffected. The authored gallery fixture is a list; it does not imply image generation or a canvas keyboard editor. |

For narrow screens, repeat at 320 and 390 CSS pixels: check page reflow, visible focus, table-region scrolling and the expanded Chart data alternative. Do not create a keyboard trap or require a pointer to recover from an error. Follow the [explorer source](../../apps/IntentBlazor.Docs/Documentation/DocumentExplorer.cs) for its separate JSON-draft protections.

## Automated checks and manual review

| Check | Existing evidence or required review |
| --- | --- |
| Encoded labels, default pair, input controls, named table scroll region, and local re-render/error retention | Existing RendererTests, including Input_RendersAccessibleControlsWithoutExtraPageWiring, Table_ExposesAFocusableNamedScrollRegion and Output_RerendersSelectionAndRetainsOutputOnFailure. These are HTML-renderer checks, not browser key presses. |
| Interactive readiness, JSON-draft protection and fixture switching | Phase6ExplorerTests exercises guards and retained state. It does not prove browser tab order. |
| Custom score bounds/events and chart data markup | Phase6QuickstartTests and ExtensionRendererTests validate public contracts and renderer output. |
| Real keyboard/change events, retained focus, disclosure and horizontal scrolling | The [Step 3 browser suite](../foundation-step3-browser-regressions.md) automates representative native Tab/Enter/change paths, late-response focus retention, explicit pressed/sort state and the narrow chart/table alternative. Repeat the walkthroughs above for Shift+Tab/Space, platform-native select popup behavior, zoom and broader browser coverage. The [hosting record](../phase-7-hosting.md) is a separate limited Edge observation. |
| Screen-reader labels, selected/sort state, meter values, errors/status announcements, meaningful reading order and focus visibility with host styling/zoom | Manual assistive-technology review. Record browser, OS, screen reader/version and outcomes; no complete screen-reader or WCAG conformance certification is claimed. |

For each browser review, record source revision, fixture, viewport, browser/OS, actions performed, expected/actual focus target and any failed recovery. Use only synthetic text in screenshots. Native event tests must edit controls and commit a change; calling Session.Change or assigning a DOM value alone cannot prove the keyboard event path.

## Renderer contribution checklist

Use semantic controls and explicit labels, keep DOM order meaningful, preserve stable identities where the task stays the same, and expose state without relying on color. Avoid nested interactive elements and positive tabindex. Check empty/hidden/disabled states, changing record order, invalid events and a focused control disappearing. Document any additional keyboard pattern an extension implements; it must not silently change the builtin select/activate/change contract. Keep business writes behind registered actions.

Implementation references: [AiInput](../../src/IntentBlazor.Blazor/AiInput.razor), [AiOutput](../../src/IntentBlazor.Blazor/AiOutput.razor), [node rendering](../../src/IntentBlazor.Blazor/Internal/OutputNodeView.razor), [list](../../src/IntentBlazor.Blazor/Internal/ListView.razor), [table](../../src/IntentBlazor.Blazor/Internal/TableView.razor), [fields](../../src/IntentBlazor.Blazor/Internal/FieldView.razor), and [chart](../../src/IntentBlazor.Blazor/Internal/ChartView.razor).
