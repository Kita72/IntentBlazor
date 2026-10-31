# Developer preview release gates

Status: accepted for Phase 7 implementation. Public C# document/session contracts remain unchanged.

Prepare `0.1.0-preview.1` as a candidate, distinct from a published release. Keep package version, output schemas (0.1/0.2/0.3), toolkit version, provider profiles and evidence versions separate. Every library includes its own readme, MIT metadata, repository/source revision and portable symbol package. Verification inspects packed artifacts and builds/runs an independent consumer before publishing.

Registered read tools may refresh a dataset already preloaded or returned earlier in the same request. Validate the complete replacement with all other authorized datasets before atomically replacing that ID. Invalid replacements cannot poison the last authorized data. Duplicate IDs from independent preload sources remain an error. This repairs the documented combined AddTools/AddDataSource journey without relaxing document validation, tool budgets, owner checks or dirty-field reconciliation.

Use deterministic tests and repeatable local rendering/update workloads in ordinary CI. A separate opt-in provider evaluation records exact server/model identity, request budgets, observed C# data access, accepted documents, interactions and failures. A model listing, connectivity probe, scripted client or benchmark is not live capability certification. Synthetic data only; no prompts or output can grant authority. Live OpenAI verification remains deferred by Tory, so the original local/frontier release acceptance gate remains unresolved until explicitly satisfied or revised by the maintainer.

The publishing workflow is separate from PR CI, has no long-lived NuGet API key, and requests OIDC only in its protected `release` job. Require a clean exact source tag/version and completed verification/evidence checks before login. Bind the NuGet policy to Kita72/IntentBlazor, the exact workflow filename and release environment. An absent profile/policy, failed gate or missing environment must not be treated as a completed release. Never retag or replace a published package version to hide a failed attempt.

Deploy the existing Interactive Server docs as a server application, preserving WebSockets and trusted proxy handling. Ordinary deployed docs remain anonymous deterministic fixtures; synthetic staff/content identities remain Development-only and live model activation remains prohibited in Production. Produce portable deployment artifacts before configuring the maintainer's identified host. Do not invent a hosting endpoint, incur new hosting costs or imply a local server is publicly deployed.

Release readiness is observable evidence, not permission supplied by generated content. Report candidate build, local provider results, public publication and deployment independently. Tory's standing direct-commit/push workflow continues; external contribution and release-account setup requirements remain explicit.
