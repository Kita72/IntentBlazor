# Hosting, ownership, and migration

The implemented hosting target is a .NET 10 Blazor Web App with **Interactive Server**. The docs are static SSR by default; only the explorer and interactive demos create circuits. Keep `AddRazorComponents().AddInteractiveServerComponents()`, static asset mapping, antiforgery, and `AddInteractiveServerRenderMode()` in your host.

## Isolate each user's workspace

`AddIntentBlazor()` registers a scoped session. The default AiInput/AiOutput pair shares it within a circuit. Do not put `AiSession`, model setup state, or authenticated domain services in a singleton. For several independent pairs in one circuit, create explicitly owned sessions and pass each pair its own session.

An in-memory session lasts only for the circuit. Reconnecting a surviving circuit and restoring after circuit loss are different operations. For durable workspaces, implement `IWorkspaceIdentity` from trusted authentication, implement `IWorkspaceAccess` to reload currently authorized records/artifacts, register a store, and connect a stable logical workspace ID. Default anonymous identity/access do not grant durable access.

The [workspace guide](../phase-3-workspaces.md) covers checkpoint sequences, reauthorization, archived context, summaries, forks, and recovery. Private checkpoints are not shared business records. Keep application schemas and transactions separate from framework storage; Staff Business demonstrates this with EF Core and a separate business database.

## Concurrency and saves

UI events require the current session revision. Checkpoint writes use their own expected sequence. Business actions require a current record version, current authorization, a connected workspace, and an issued request; the service owns transactional idempotency. A conflict retains private drafts. Do not automatically replay a pending or uncertain write on reconnect.

Use the Blazor dispatcher for session changes and external notifications. This version does not support simultaneous arbitrary background-thread mutation or collaborative editing of one shared session. Business apps can share records while keeping each person's session private.

## Files and private delivery

`IntentBlazor.Content` stores bytes outside public static folders. Artifact metadata and download requests reauthorize the trusted owner. Do not treat a generated URL or artifact ID as permission. Default content limits and format boundaries are in the [content guide](../phase-4-content-and-setup.md). Images are metadata context, not a guarantee of provider vision support. PDFs are text extraction; OCR, video/audio processing, and arbitrary filesystem access are unsupported.

Configure storage locations, retention, backups, access control, and identity for your deployment. The examples' Development-only protected synthetic cookies are learning aids, not a production account system.

## Model connections and live demos

Credentials and endpoints are server-controlled. Server `localhost` refers to the host machine; it does not discover visitors' Ollama or LM Studio installations. HTTP is accepted only on loopback provider endpoints; use HTTPS for remote hosts. Codex is an experimental installed CLI adapter for a controlled developer machine, not a bridge into this Codex desktop chat.

The docs live model demo is disabled by default and **refuses to start outside Development when enabled**. It makes no call on page load. Its session is circuit-scoped; data is synthetic; each request has explicit model/tool/output/context/time limits. The `/models` page displays those limits. Deployed docs remain credential-free fixture mode. AiSetup uses host-approved presets and policy; it does not let visitors configure arbitrary endpoints or secrets.

A public application enabling live inference must add authentication, rate limits, quotas, cancellation, operational monitoring, retention, and appropriate data policies. Per-request token/call limits do not establish a cross-user cost cap. The library does not provide a hosted inference service or production identity system.

## Supported formats and migration

Output schemas `0.1`, `0.2`, and `0.3` retain explicit version checks; builtin toolkit stays `0.1`. Schema 0.2 introduces registered actions; 0.3 introduces content and extensions. Older authored fixtures still render under their declared version. Unsupported versions, required packs, or invalid restored references fail closed without silently upgrading or discarding the current draft.

Persist framework checkpoints through `IWorkspaceStore` rather than inventing serialization of a live circuit. Checkpoints carry their own format version; the SQLite adapter initializes its framework schema. No general migration engine exists for arbitrary future checkpoint versions or your application's database. Treat package upgrades as experimental: back up durable storage, pin pack versions, test restoration with representative data and identities, and implement app-owned database migrations separately.

WebAssembly, MAUI/Hybrid, distributed stores, scale-out circuit restoration, and production multi-node hosting have not been verified. Do not infer compatibility from .NET targeting alone. See [preview boundaries](limitations.md) and [provider evidence](../provider-evaluation.md).
