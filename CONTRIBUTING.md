# Contributing to IntentBlazor

Thank you for helping build IntentBlazor. Read the [README](README.md) for current capabilities and [ROADMAP](ROADMAP.md) for planned work. Claim an issue before substantial work so contributors do not duplicate effort.

## Local setup

Install Git, PowerShell 7, and a .NET 10.0.4xx SDK satisfying [global.json](global.json). Clone the repository and run:

~~~powershell
pwsh -File scripts/Verify.ps1
dotnet run --project apps/IntentBlazor.Docs --no-launch-profile -- --environment Development --urls http://localhost:5180
~~~

Ordinary builds and fixture-based work must not require a model account, paid credentials, or private data. Record live-model evaluations separately with provider, model identifier, configuration, scenario, and observed outcome.

## Work and review

1. Find an issue labelled good first issue or help wanted, or open a proposal.
2. Branch from main using a descriptive name.
3. Keep changes focused. Public contract changes require an architecture proposal under docs/architecture.
4. Add behaviour-focused regression coverage when introducing or changing executable behaviour.
5. Run scripts/Verify.ps1 and describe checks actually completed in the pull request.
6. Update affected documentation and CHANGELOG.md.
7. Request review. The maintainer merges changes after checks pass.

Do not publish packages or deploy the docs site from pull requests. Maintainer release workflows will be introduced before the developer preview.

## Conventions

Follow .editorconfig, nullable C#, dependency injection, cancellation, and small cohesive components. Keep provider SDKs out of application services and the renderer. Expose application operations explicitly; generated content does not confer authorization.

The nearest Directory.Build.props is imported early by MSBuild. Project files retain their target framework and project-specific settings; shared defaults live at the repository root. Package versions belong in Directory.Packages.props.

New SDK-style test projects should initially use the conventional VSTest command mode. If a different runner is proposed, update the verification script, global configuration, CI, and contributor instructions together. Do not add empty tests just to produce a green result.

Use synthetic data and original or redistribution-compatible assets. Include attribution and dependency license information. Never commit credentials, private documents, model transcripts containing personal data, or database files. Keep setup secrets in host-managed secret storage.

## Contribution terms

Unless you explicitly state otherwise in a proposal before submission, contributions submitted for inclusion are licensed under this repository's MIT license. You must have the right to contribute the material. No separate contributor license agreement is required at this stage.

AI-assisted changes receive the same review and validation as other changes. Authors remain responsible for understanding and verifying their submissions.

Read [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), [GOVERNANCE.md](GOVERNANCE.md), and [SECURITY.md](SECURITY.md).
