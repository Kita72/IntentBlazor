# Platform and project boundaries

Status: accepted for Phase 0, October 29, 2026.

Target net10.0 and pin SDK 10.0.403 with latestPatch roll-forward. This SDK is available on the implementation machine. Reassess .NET 11 compatibility at kickoff of the model integration work; do not claim it verified now.

The documentation app uses static SSR with per-page Interactive Server support for future demonstrations. Anonymous access is appropriate for public docs. Authentication and domain data for the staff example arrive with that application.

Core contains provider-independent contracts. Runtime references Core. Blazor references Core and Runtime. The optional SQLite adapter references Core. Docs references the public library projects. Initially the libraries have no runtime implementation or third-party package dependencies.

Shared C# defaults and package metadata live in Directory.Build.props; each project owns its target framework and description. Directory.Packages.props enables central package versions when dependencies are added. Lock files are committed. Application projects are not packable.

Microsoft.Extensions.AI and the orchestration dependency are evaluated in Phase 2. Do not add provider packages before their public integration boundary is designed.
