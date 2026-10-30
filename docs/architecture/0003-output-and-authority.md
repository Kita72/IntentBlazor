# Output documents and authority

Status: accepted direction. The Phase 1 subset is implemented as [experimental schema 0.1](0004-deterministic-output-v01.md). [Phase 3](0006-durable-workspaces.md) adds registered actions in schema 0.2 and owner-scoped durable restoration.

AiInput and AiOutput share an explicitly scoped logical session. A document describes data references, exact state, a view, interactions, stable node identities, and an expected revision.

The runtime validates typed bindings, events, available actions, and extension requirements. Application services enforce permissions and business rules. A model cannot authorize its own operation, invent data access, or declare a successful write before a real result exists.

Local sorting, selection, and edits need no model call. Meaningful state is included in the next model request. Revision checks and dirty-field rules must prevent silent loss of user work.

Persistence records logical session ownership separately from the Blazor circuit. Restored references are reauthorized. News, business, and asset examples must share these mechanisms, with domain behaviour in services or registered extensions.
