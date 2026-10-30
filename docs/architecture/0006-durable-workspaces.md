# Durable workspaces and registered actions

Status: implemented, experimental public contracts. Builds on decisions [0003](0003-output-and-authority.md), [0004](0004-deterministic-output-v01.md), and [0005](0005-model-connections.md).

## Context

A Blazor circuit is a presentation lifetime. A user needs to retain exact edits across a new circuit while the application's shared records can change independently. Model output and restored content cannot establish authority to read records or execute writes.

## Decision

Keep `AiInput` and `AiOutput` unchanged. Connect an `AiSession` to an explicit workspace key. Obtain the owner exclusively through host `IWorkspaceIdentity`; the Blazor adapter reads the authenticated name-identifier claim through `AuthenticationStateProvider`. Multi-tenant hosts should supply a tenant-qualified stable owner. A changed identity invalidates and clears the old private in-memory session; start a fresh session for the new identity.

Expose `IWorkspaceStore` with compare-and-swap checkpoint versions and action request claims/receipts. The default is a singleton in-memory store; the optional SQLite implementation stores immutable serialized checkpoints under `(owner, key)`. Neither store establishes identity itself. Hosts own retention, backup, encryption, database placement, and access to their server filesystem. Do not expose the store as an unauthenticated API.

Checkpoint format `1` contains the current output, exact values, original edit baselines, dirty markers, sorting, artifact identifiers/versions, and original input/output conversation turns. Restore reauthorizes every referenced dataset and artifact through `IWorkspaceAccess`, replaces persisted rows with fresh authorized host data, then validates the complete candidate before installation. Unknown formats and unsupported output schemas fail with an incompatibility result. No automatic format migration is claimed.

The default context policy selects up to six recent turns within an 8,000-character input/excerpt budget, plus a bounded extractive index of older user requests. It is deterministic and is not a model-written semantic summary. Original conversation turns remain stored; bounded context does not prune the archive. Stop accepting turns when the archive quota is reached.

`ApplyUpdate` reconciles a complete validated proposal against the current session revision. Compatible values and their baselines survive; a proposal that removes, invalidates, or hides a dirty field is rejected. This is full-document reconciliation, not JSON Patch or collaborative merging.

Retain schema 0.1 for local interactions. Schema 0.2 additionally permits explicitly registered action descriptors. `AddIntentAction<T>` associates a trusted label and typed scalar parameters with an `IWorkspaceActionHandler`. The model may propose that action's button and bindings; only an explicit user activation enters the write path. Read-only AI data tools remain separate.

The runtime validates the issued nonce, revision, visible source, bindings, resource access, argument types, and current identity, then calls the handler's authorization and execution methods. Claim the request durably before entering the handler. Pass that request ID as the host's idempotency key. Persist the actual structured outcome before acknowledging fields as saved. A conflict or rejection leaves the draft; a submitted but uncertain operation is not automatically replayed.

Application services must enforce their own permissions, expected record versions, and transactionally durable idempotency. There is no distributed transaction between a workspace checkpoint and the host's business database. A crash after the business commit can leave a pending framework receipt: check the application's receipt before deliberately starting another operation. Exactly-once effects for arbitrary external services are not promised.

## Consequences and acceptance evidence

The SQLite adapter uses dedicated `intent_` tables, metadata schema version `1`, parameterized SQL, WAL, atomic conditional writes, and independent connections. It does not reuse an application's schema or database migration version. It targets single-instance examples; distributed coordination and real-time shared editing need another adapter/design. SQLite calls are synchronous because Microsoft.Data.Sqlite's async APIs execute synchronously.

Automated tests exercise exact recovery in new scopes, concurrent checkpoint writes, separate staff identities, actual shared SQLite record conflicts, revoked access, malformed formats, late edits/identity changes, durable receipts, and uncertain submissions. The docs host demonstrates reconnect, reload, and conflict recovery using the public session/action contracts. Live models and other interactive hosting models remain unverified. See the [guide](../phase-3-workspaces.md) and [test evidence](../../tests/README.md).
