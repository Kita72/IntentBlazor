# Create a typed interactive renderer

Compose the [built-in nodes](nodes.md) for new arrangements. When you need a new primitive, register a typed configuration/event contract and a Razor component. No generated executable UI code is loaded. The compiled [Quickstart](../../examples/Quickstart/README.md) provides a complete small example.

## Define configuration and local effects

[ScoreContract.cs](../../examples/Quickstart/ScoreContract.cs) describes an exact pack/version/renderer key, a model-visible description and example configuration, valid state requirements, and local event reduction.

{{sample: ScoreContract.cs}}

`Validate` checks configuration against the whole proposed `OutputContent`. `EditableStates` declares which existing state slots this renderer may change; it also participates in preserving visible dirty editors during follow-ups. `Reduce` returns up to 16 unique `ExtensionEffect` targets. Each effect is validated against its declared state type, range, and current workspace. `IsDraft:true` marks a direct user edit; returning to its baseline clears that dirty mark. A non-draft effect cannot overwrite a dirty target.

The sample accepts steps of exactly -1 or +1, clamps its numeric state to 0–10, and rejects other messages. A malformed message, stale revision, hidden source, wrong target, or missing extension leaves current state available. Local extension effects cannot invoke business writes.

## Render accessible controls

[ScoreView.razor](../../examples/Quickstart/Components/ScoreView.razor) derives from `IntentExtensionComponent<TConfiguration>`. The runtime supplies `Session` and `Node`, and the base component reads typed configuration. Use labels, ordinary controls, and the current session revision:

{{sample: Components/ScoreView.razor}}

`DispatchExtension(nodeId, expectedRevision, message)` revalidates source visibility, installed contract, payload, and effects. The sample widget uses the framework session's event notification to rerender the containing output and its rating.

## Register the pack

Call `services.AddIntentRenderer<ScoreContract, ScoreView>()` during host setup. The full registration appears in the [service guide](services.md). It registers both the contract and its component type; duplicate exact keys fail at startup. The model sees registered descriptors and configuration examples automatically.

Represent this primitive in a schema 0.3 node:

~~~json
{
  "id": "scoreEditor",
  "kind": "extension",
  "title": "Local score",
  "extension": {
    "pack": "sample", "version": "1.0", "renderer": "score",
    "configuration": { "stateKey": "score" },
    "fallback": "Score editor unavailable."
  }
}
~~~

The surrounding document must define non-null numeric `score` state, minimum 0 and maximum 10. Declare `"sample":"1.0"` in `requires` when this renderer is essential. The [compiled processor](../../examples/Quickstart/CatalogInputProcessor.cs) supplies the complete valid document, including a draft reset button.

## Required and optional capabilities

A missing/incompatible required pack rejects the whole proposal. An unavailable optional extension can display only its encoded text fallback and must have no interactive children. Installed extensions may compose ordinary children while preserving the single rooted tree. Versions are exact; the framework does not silently substitute a newer pack.

Configuration objects are bounded to 8,192 characters, event JSON to 4,096 characters, and fallback text to 500 characters. A descriptor example is documentation for the model, not an authorization rule. Use registered [application actions](../../specs/output-document/v0.2-actions.md) for durable writes. The [image editor pack](../phase-4-content-and-setup.md) demonstrates this separation with a real PNG resize and immutable artifact versions.
