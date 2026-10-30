# Staff Business example

The independently runnable app is planned for Phase 5. The [authored staff fixture](../../specs/fixtures/staff.json) remains available in the docs playground. Phase 3 adds a Development-only [recovery demo](../../docs/phase-3-workspaces.md) with synthetic protected-cookie identities, real private SQLite checkpoints, shared job saves, restricted-action checks, and record-version conflicts. Its [host-owned services](../IntentBlazor.Examples/StaffWorkspaces.cs) use the public workspace/action contracts; production account integration remains the consuming app's responsibility.

Demonstrate authenticated synthetic staff, shared job records, private AI sessions, assignment forms, permission checks, and optimistic concurrency. Keep domain storage separate from framework session persistence.

Acceptance: two staff users edit the same job; the second receives a recoverable conflict, and a restricted user cannot invoke a prohibited operation.
