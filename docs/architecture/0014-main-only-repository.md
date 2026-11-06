# Main-only source repository and local verification

Status: accepted by Tory on November 6, 2026. Repository administration only; no framework, document or AiInput/AiOutput contract changes.

## Context and decision

Tory is a solo developer working with AIMIE in Codex. Old phase branches and scheduled Dependabot pull requests made GitHub an unwanted maintenance queue. Keep the existing public MIT repository and its commit history, with `main` as its only upstream and local development branch. Use GitHub for source hosting and history backup.

Work and verify locally, then commit and push directly to `main`. Do not create maintainer PRs or development branches. Requested external contributions may use their own forks, with maintainer review and local verification before integration. An issue or Discussion is not a prerequisite for Tory-directed work in Codex.

Remove `.github/dependabot.yml` and the CI/publishing workflow files. Disable this repository's Dependabot alerts, automatic security-update PRs and GitHub Actions. Close the five outstanding bot PRs without merging their proposed dependency changes. Account-wide settings and other repositories are outside this decision. Do not disable secret scanning/push protection or alter repository access.

Remove PR/status-check/conversation requirements from the existing `main` rule so ordinary verified direct pushes work without Actions. Keep force pushes and deletion of `main` prohibited. Preserve a verified local Git bundle containing every branch head before removing old branch refs; inspect branch-only work first. Phase branches are already incorporated, the old Phase 0/documentation fixes have equivalent patches on `main`, and the local bootstrap differs from the incorporated foundation only by UTF-8 byte-order marks. Unmerged bot updates remain in the backup, not in the dependency set.

## Consequences

Local `scripts/Verify.ps1` and its offline framework, browser, package, documentation and renderer checks remain available. Dependency updates are deliberate maintenance work in Codex using committed versions and lock files. Run checks appropriate to the change and report actual outcomes. No model service or paid inference is needed or authorized.

This supersedes the GitHub CI and OIDC publishing setup described in earlier phase records and decisions 0010/0013. Their prior results remain historical evidence, not a promise of ongoing CI. Package publication and deployment remain separate, explicitly requested tasks. There is no active package-publishing workflow; choose and verify a publishing method with Tory before a future release. Existing readiness gates are not automatically satisfied by this cleanup.

Do not reintroduce bots, workflows, branch proliferation or account-level changes without Tory's explicit instruction. No repository recreation, package publication or site deployment is part of this cleanup.

## Verification

Check that GitHub exposes only `main` and no open bot PRs; verify repository-scoped Dependabot controls and Actions are off. Verify local `main` matches the pushed commit, the old branch heads are recoverable from the bundle, removed automation files are absent, and documentation links and whitespace checks pass. Full framework tests remain available locally; a documentation/administration-only change does not imply a new runtime or live-provider result.
