# Validation projects

Run `pwsh -File scripts/Verify.ps1` from the repository root. It restores locked dependencies, builds the solution in Release, verifies formatting, runs the test project without rebuilding, checks documentation links, and packs the four source libraries locally.

`IntentBlazor.Tests` uses xUnit with conventional VSTest. No live model, credentials, database, or browser installation is required by the automated suite.

| Area | Evidence |
| --- | --- |
| Contract compatibility | All three fixtures round trip with separate host data; strict JSON and semantic validation reject unsupported content. |
| Boundaries | Exact UTF-8 size, tree depth, node/state/row counts, and numeric input limits test accepted and rejected boundaries. |
| Local interactions | Shared selection contract, numeric sorting, atomic assignments, record validation, hidden ancestors, draft protection, and explicit clearing. |
| Input orchestration | Configured processor, scoped sessions, busy guard, cancellation, safe failure messages, and stale response rejection. |
| Blazor output | Actual `HtmlRenderer` renders all three domains, responds to state changes, retains output on failure, encodes text, and blocks nonlocal images. |
| Drop and go | Default registration and accessible input controls; explicit session override; busy/cancel states retain the workspace. |

The playground is also checked manually in a native browser for selection, sorting, form editing, draft rejection and clearing, fixture input, invalid proposals, and responsive layout. These checks are distinct from automated HTML rendering tests. Live providers, durable storage, application authorization, and multi-user business workflows are not exercised because they are not implemented in Phase 1.

Use conventional VSTest-mode SDK projects initially. scripts/Verify.ps1 discovers projects explicitly declaring IsTestProject=true and runs each with dotnet test using no-build and no-restore. Changes to runner conventions require corresponding script and CI changes.

Provider tests requiring real credentials must be optional and separated from deterministic contributor checks.
