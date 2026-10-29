# Staff Business example

The independently runnable app is planned for Phase 5. Phase 1 already provides the [authored staff fixture](../../specs/fixtures/staff.json) in the docs playground: sortable jobs, selected job, owner lookup, notes, and protected local drafts use the common output contract. Authentication, multi-user storage, and business saves are not implemented yet.

Demonstrate authenticated synthetic staff, shared job records, private AI sessions, assignment forms, permission checks, and optimistic concurrency. Keep domain storage separate from framework session persistence.

Acceptance: two staff users edit the same job; the second receives a recoverable conflict, and a restricted user cannot invoke a prohibited operation.
