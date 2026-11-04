# Contributing to IntentBlazor

Thank you for helping build IntentBlazor. Read the [README](README.md) for current capabilities and [ROADMAP](ROADMAP.md) for planned work. Claim an issue before substantial work so contributors do not duplicate effort.

## Local setup

Install Git, PowerShell 7, and a .NET 10.0.4xx SDK satisfying [global.json](global.json). Clone the repository and run:

~~~powershell
pwsh -File scripts/Verify.ps1
dotnet run --project apps/IntentBlazor.Docs --no-launch-profile -- --environment Development --urls http://localhost:5180
~~~

Framework build, test and release verification must not require a model account, paid inference, or private data. Use offline substitutes to verify adapter contracts and failure handling. Live-model evaluations are optional developer/contributor observations, recorded separately with provider, model identifier, configuration, scenario, and observed outcome; see [decision 0012](docs/architecture/0012-framework-verification-and-optional-model-reports.md). Do not start Ollama, LM Studio or their workers on Tory's machine, including status commands that activate services.

## Work and review

1. Find an issue labelled good first issue or help wanted, or open a proposal.
2. Branch from main using a descriptive name.
3. Keep changes focused. Public contract changes require an architecture proposal under docs/architecture.
4. Add behaviour-focused regression coverage when introducing or changing executable behaviour.
5. Run scripts/Verify.ps1 and describe checks actually completed in the pull request.
6. Update affected documentation and CHANGELOG.md.
7. Request review. The maintainer merges changes after checks pass.

The pull-request process above applies to external contributions. For maintainer-directed implementation, Tory has authorized AIMIE to commit and push completed work directly to `main` after verification, without a separate PR review. This does not authorize changing repository protections, publishing packages, or deploying the site. CI still validates pushes to `main`.

Do not publish packages or deploy the docs site from pull requests. The separate maintainer publishing workflow requires verified release evidence and confirmed NuGet ownership/trusted-publishing policy; see the [release guide](docs/site/releasing.md).

For renderer contributions, follow the [keyboard and focus guide](docs/contributing/keyboard-and-focus.md). Provider reports should use the [capability report template](docs/contributing/provider-capability-report.md); its [fictional example](docs/contributing/provider-capability-report-example.md) illustrates partial and failed results without claiming model support. Report actual observations and leave unexercised capabilities marked Not tested.

## Conventions

Follow .editorconfig, nullable C#, dependency injection, cancellation, and small cohesive components. Keep provider SDKs out of application services and the renderer. Expose application operations explicitly; generated content does not confer authorization.

The nearest Directory.Build.props is imported early by MSBuild. Project files retain their target framework and project-specific settings; shared defaults live at the repository root. Package versions belong in Directory.Packages.props.

New SDK-style test projects should initially use the conventional VSTest command mode. If a different runner is proposed, update the verification script, global configuration, CI, and contributor instructions together. Do not add empty tests just to produce a green result.

Use synthetic data and original or redistribution-compatible assets. Include attribution and dependency license information. Never commit credentials, private documents, model transcripts containing personal data, or database files. Keep setup secrets in host-managed secret storage.

## Contribution terms

Unless you explicitly state otherwise in a proposal before submission, contributions submitted for inclusion are licensed under this repository's MIT license. You must have the right to contribute the material. No separate contributor license agreement is required at this stage.

AI-assisted changes receive the same review and validation as other changes. Authors remain responsible for understanding and verifying their submissions.

Read [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), [GOVERNANCE.md](GOVERNANCE.md), and [SECURITY.md](SECURITY.md).
