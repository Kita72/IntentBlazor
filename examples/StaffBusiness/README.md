# Staff Business

Independently runnable Phase 5 Blazor Web App with synthetic roles, jobs waiting on materials, assignment drafts, shared operation history, and an example-owned EF Core SQLite database. Its page uses default AiInput/AiOutput; the framework checkpoint database remains separate.

~~~powershell
dotnet run --project examples/StaffBusiness --no-launch-profile -- --environment Development --urls http://localhost:5182
~~~

Open http://localhost:5182. Alex and Sam are synthetic Dispatchers; Riley is a Reader. Use independent browser sessions/profiles for two users. Select the same job in both, edit both drafts, save Alex's assignment, and attempt Sam's save. The conflict retains Sam's draft. Reconnect/refresh, review the saved values and history, explicitly choose Use current record version after review, then save. Riley's UI and direct-service save attempts are denied by the server.

Ignored artifacts/phase5-staff contains business.sqlite and workspaces.sqlite. Configure trusted Example:DataDirectory for another isolated directory. The [repository](StaffRepository.cs) uses IDbContextFactory, optimistic versions, and transactional operation/receipt writes. IStaffRepository is the replacement seam; choose another EF provider and migrations in the consuming host. EnsureCreated initializes this small synthetic example, so do not point it at an existing business database.

Ai:Provider/Ai:Model opt into existing model adapters; Fixture is the default. UserSecretsId is IntentBlazor.Examples.StaffBusiness. No inference occurs on initial load/restoration, and live-model verification is deferred. See the [Phase 5 guide](../../docs/phase-5-example-apps.md) for setup, permission/conflict semantics, private workspace controls, and the Development-only synthetic identity boundary.
