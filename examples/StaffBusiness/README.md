# Staff Business example

Planned for Phase 5. No application is implemented in this directory yet.

Demonstrate authenticated synthetic staff, shared job records, private AI sessions, assignment forms, permission checks, and optimistic concurrency. Keep domain storage separate from framework session persistence.

Acceptance: two staff users edit the same job; the second receives a recoverable conflict, and a restricted user cannot invoke a prohibited operation.
