# Release and deployment

The current `0.1.0-preview.1` is an unpublished candidate. Follow the [readiness record](../phase-7-readiness.md). Maintainers develop and verify locally in Codex, then commit and push directly to `main`. GitHub Actions, Dependabot and the former publishing workflow are disabled/removed under [decision 0014](../architecture/0014-main-only-repository.md). Pushing code or creating a tag does not publish packages or deploy the site.

## Package publication remains separate

No NuGet publication is authorized by ordinary development or this repository cleanup. Before a future release, Tory must choose the package owner and an explicit publishing method that fits the local workflow. A [NuGet.org account](https://www.nuget.org/users/account/LogOn) is not yet configured for this project. Do not accept account terms, create credentials or configure trusted publishing as part of routine contributions. Never paste credentials or API keys into chat, issues or source.

Local `scripts/Verify.ps1` packs and audits all eight `.nupkg`/`.snupkg` pairs and runs independent package consumers. Retain those checks and current runtime/package/hosting evidence before any later publication. The existing account/policy readiness gate remains unsatisfied; disabling Actions does not establish a tested alternative publisher. Do not recreate the old `publish.yml`/`release` OIDC setup without Tory's explicit instruction. Once a publishing method is chosen, document and verify its credential handling and exact source/version checks before publishing. Never overwrite a release tag or replace a published package version to hide a failed attempt. Live model reports remain optional under [decision 0012](../architecture/0012-framework-verification-and-optional-model-reports.md).

## Prepare the IIS docs bundle

~~~powershell
pwsh -File scripts/Verify.ps1
pwsh -File scripts/Publish-Docs.ps1
~~~

The second command creates a versioned publish folder and ZIP under ignored `artifacts/deploy/`. The folder contains the app, static assets, Production `web.config` and a source manifest. The ZIP is an IIS folder bundle, not an MSDeploy package. Keep publish settings, local settings, databases and private keys out of source and bundles. The server must have the .NET 10 ASP.NET Core hosting runtime and permit Interactive Server circuits/WebSockets.

## Deploy to MonsterASP

Enable Web Deploy in the hosting control panel and download its publish settings to a local private directory. Use the generated folder path from `artifacts/phase7-docs-path.txt`:

~~~powershell
$bundle = Get-Content artifacts/phase7-docs-path.txt
pwsh -File scripts/Deploy-Docs.ps1 -PublishSettingsPath 'C:\private\site.publishSettings' -PublishDirectory $bundle -ExpectedSiteHost intentblazor.runasp.net -WhatIf
~~~

Review the expected site and repeat without `-WhatIf` to deploy. The helper reads the profile in memory, uses its matching MonsterASP publishing endpoint over HTTPS with certificate validation, preserves destination files, takes the app offline during replacement and suppresses raw native output. It never prints native credentials or copies the profile into the repository. Follow the host's [Web Deploy instructions](https://help.monsterasp.net/websites/deploy/command-line) for server-side setup; the helper keeps TLS validation enabled.

After an explicitly requested deployment, request HTTPS `/health/ready` and verify the expected source revision, `mode=fixture` and version. Open `/docs/quickstart`, `/docs/profiles`, `/demos` and `/explorer` in a real browser. Submit a fixture, select a record and edit a field to prove the Interactive Server connection beyond an HTTP 200 response. Check missing routes and Development-only controls. Keep a prior bundle for rollback and never commit the publish settings. Record host verification for the exact candidate only after public HTTPS and circuit checks pass; the former GitHub workflow variables are not an active deployment or release mechanism.
