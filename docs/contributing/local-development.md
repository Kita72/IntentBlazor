# Local development

Run the verification script from [CONTRIBUTING.md](../../CONTRIBUTING.md). It supports PowerShell 7 on Windows and Linux.

Use the no-launch-profile HTTP command in the README for a local smoke check. It explicitly selects Development so static web assets resolve when running from source, and avoids requiring development certificate installation. Production hosting keeps HTTPS redirection enabled.

No generated binary or package belongs in source control. Local output is under ignored bin, obj, and artifacts directories.

For deliberate dependency updates, review the change here in Codex, update Directory.Packages.props and run dotnet restore IntentBlazor.slnx without locked mode to refresh committed packages.lock.json files. Local verification uses locked restore. Dependabot and GitHub Actions are disabled; maintainer work goes directly to `main` after appropriate local checks. See [decision 0014](../architecture/0014-main-only-repository.md).

Live-model setup belongs in host-managed configuration introduced during Phase 2. Never paste credentials into issues or include them in a pull request.
