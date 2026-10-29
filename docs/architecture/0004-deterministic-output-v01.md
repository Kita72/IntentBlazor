# Deterministic output schema 0.1

Status: implemented experimental contract, Phase 1.

## Context

The two public interaction components need one bounded presentation model that can express a news reader, a staff follow-up form, and an asset gallery. Establishing this without a model makes renderer behavior and application authority independently testable.

## Decision

Use immutable C# records, a strict JSON codec, and semantic validation. Separate proposed presentation from host-owned typed datasets. A scoped `AiSession` owns selections, sorting, drafts, errors, input outcomes, and a monotonically increasing session revision. Both components resolve the same scoped session by default and accept an explicit session for multiple independent workspaces.

Implement a small fixed vocabulary that can be composed into new views. Ordinary interactions are deterministic local state transitions. Unknown toolkit requirements and application actions are rejected. Host services will eventually expose application capabilities, but a presentation document cannot authorize a write.

An `IInputProcessor` seam accepts text and a workspace snapshot and returns proposed content. The example implementation selects authored fixtures only. A later model adapter must retain host ownership of dataset authorization and pass all proposals through the same validation path.

## Consequences

The components remain drop and go inside a supported interactive boundary. The first implementation targets Blazor Web Apps with Interactive Server. Session state is circuit-scoped, ephemeral, and must be used on the Blazor dispatcher; this version is not a thread-safe shared application store. Host integrations must marshal external updates onto their session's dispatcher.

Dirty values block overwriting transitions rather than being silently lost. Full document replacement resets clean selection state. Patch reconciliation, durable session ownership, authorization refresh, and multi-user concurrency remain Phase 3. Rich files, extensions, and AI setup controls remain Phase 4. Independently runnable example apps remain Phase 5.

## Evidence

All three authored fixtures pass the same validator and renderer. Contract tests cover parsing, graph integrity, bounds, bindings, capabilities, and image policy. Session tests cover local events, typed sorting, atomic failure, dirty-state protection, scoped isolation, cancellation, and stale async responses. HTML renderer tests exercise real `AiInput` and `AiOutput`, including updates, failure preservation, encoded text, image blocking, busy states, and explicit session override. The docs playground provides the same public API with a document explorer.
