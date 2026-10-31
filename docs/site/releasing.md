# Release and deployment

The current `0.1.0-preview.1` is a candidate. Follow the [readiness record](../phase-7-readiness.md); the public release remains gated. Maintainers commit verified development work to main. Package publication is a separate tag-triggered workflow, not a consequence of merging or pushing code.

## First NuGet profile and trusted policy

Create a free [NuGet.org account](https://www.nuget.org/users/account/LogOn), choose a public username and confirm its email. Do not paste credentials or API keys into chat, issues or source. Tell the maintainer the public profile name and whether an organization should own the packages before the first publication; package IDs and ownership must be deliberate.

Create a [trusted publishing policy](https://www.nuget.org/account/trustedpublishing) with these exact values:

| Field | Value |
| --- | --- |
| Repository owner | `Kita72` |
| Repository | `IntentBlazor` |
| Workflow file | `publish.yml` (filename only, not its display name or path) |
| Environment | `release` |

Configure the GitHub `release` environment and set its `NUGET_USER` secret to the NuGet profile username, not an email. Configure allowed release tags and any maintainer protections before use. Confirm the policy exists before enabling publication. Set repository variable `INTENT_NUGET_POLICY_CONFIRMED=true` only after that confirmation. Do not create a long-lived NuGet API key. The publishing job uses `NuGet/login@v1` with `id-token: write` to obtain a short-lived credential.

Before the first publication, pack locally and inspect the `.nupkg` and `.snupkg`; `scripts/Verify.ps1` performs that pack, audits all eight packages and executes independent package consumers. Do not overwrite tags or replace published versions after a failed attempt. The workflow requires exact `v<Version>` tags on main, successful deterministic checks and current live evidence. An unresolved live gate stops publication before NuGet login. The maintainer must confirm both the profile policy and all readiness gates before creating the release tag.

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

After deployment, request HTTPS `/health/ready` and verify the expected source revision, `mode=fixture` and version. Open `/docs/quickstart`, `/docs/profiles`, `/demos` and `/explorer` in a real browser. Submit a fixture, select a record and edit a field to prove the Interactive Server connection beyond an HTTP 200 response. Check missing routes and Development-only controls. Keep a prior bundle for rollback and never commit the publish settings. Mark `INTENT_PUBLIC_HOST_VERIFIED=true` only after public HTTPS and circuit checks pass for the candidate being released.
