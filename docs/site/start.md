# Build around two components

IntentBlazor turns user intent into a validated interactive workspace. Your app supplies the model connection, authorized data, business services, and identity. The framework coordinates input, a versioned output document, local state, and registered actions.

**Experimental, unreleased API.** The initial target is a .NET 10 Blazor Web App with Interactive Server. Packages have not been published. Live provider verification remains deferred. See [preview boundaries](limitations.md) before choosing a deployment target.

## The developer journey

1. [Install from source or local packages](installation.md), then run the [two-component quickstart](quickstart.md) without credentials.
2. Place `<AiInput />` and `<AiOutput />` in one interactive circuit. They resolve the same scoped `AiSession`; you do not connect them with page events.
3. [Connect a model and expose a C# service](services.md). Attributes are the easy path; explicit registration supports services you cannot annotate. Keep model SDK calls outside domain services.
4. Supply authorized datasets. The model proposes a document referencing their IDs and fields; it does not supply authoritative business rows.
5. Let the deterministic renderer handle selection, sorting, and local edits. It validates every replacement and preserves compatible drafts during follow-ups.
6. Add [owned persistence and registered actions](../phase-3-workspaces.md) when you need business saves, restoration, or multiple staff members.
7. Add [files and AI setup tools](../phase-4-content-and-setup.md), or a [typed renderer](extensions.md), when your domain needs more than the built-in vocabulary.

## What the model builds

The model composes a tree of [supported nodes](nodes.md): a card around a table, a selected record's details beside a form, or an image editor beside a gallery. It can discover host-registered renderer descriptions and configuration examples. It cannot compile new Razor, run generated JavaScript, install a component type, grant permissions, or claim that a save succeeded.

New combinations of primitives need only a new document. A new primitive needs a developer-supplied typed contract and renderer. That keeps a dynamic interface compatible with the app's normal C# authorization and validation boundaries.

## Input, state, and continuity

AiInput receives text and optional bounded attachments. The input processor receives a `WorkspaceSnapshot` including the current document, selection, values, sorting, dirty fields, authorized artifact references, and bounded conversation context. AiOutput renders the accepted document and reports validation or operation errors without replacing the last working output.

Session revision and business-record version solve different problems: a stale UI event cannot change a newer workspace; a conflicting business save cannot overwrite a newer shared record. Private checkpoints preserve each user's draft. Your app owns shared business transactions and identity.

Choose **Explorer** in the site navigation, or open `/explorer` on your docs host, to inspect authored news, staff, and asset fixtures. The [three standalone apps](../phase-5-example-apps.md) add real synthetic workflows beyond those read-only fixtures.

## What you still own

Your app owns authentication, resource permissions, database schemas, host secrets, retention, quotas, model costs, and deployment. Optional `AiSetup`, `AiInspector`, and workspace controls support developers without changing the two primary interaction components. No project-operated cloud service is required.
