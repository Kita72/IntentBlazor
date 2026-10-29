# Validation projects

Phase 0 validates restore, compilation, formatting, documentation links, and package creation. There are no automated behaviour test projects yet.

Phase 1 introduces contract and renderer tests alongside the behaviours they verify. Additional runtime, browser, provider, and package-consumer validation follows the implementation plan.

Use conventional VSTest-mode SDK projects initially. scripts/Verify.ps1 discovers projects explicitly declaring IsTestProject=true and runs each with dotnet test using no-build and no-restore. Changes to runner conventions require corresponding script and CI changes.

Provider tests requiring real credentials must be optional and separated from deterministic contributor checks.
