# Release and deployment

The first developer preview, `0.1.0-preview.1`, is published under Wilson72. The [publication record](../foundation-step6-developer-preview.md) identifies its immutable source and completed public checks. Follow the [readiness record](../phase-7-readiness.md) for future candidates. Maintainers develop and verify locally in Codex, then commit and push directly to `main`. GitHub Actions, Dependabot and the former publishing workflow are disabled/removed under [decision 0014](../architecture/0014-main-only-repository.md). Pushing code or creating a tag does not publish packages or deploy the site.

## Package publication remains separate

Tory explicitly authorized the first developer preview's publication in foundation Step 6. The selected NuGet owner is [Wilson72](https://www.nuget.org/profiles/Wilson72). Publishing runs locally with a one-day API key restricted to pushing `IntentBlazor.*` packages/versions, without unlist permission. GitHub Actions/trusted-publishing workflows remain disabled. Ordinary development still does not authorize another publication. Never paste Microsoft passwords or API keys into chat, issues or source.

Local `scripts/Verify.ps1` packs and audits all eight `.nupkg`/`.snupkg` pairs and runs independent package consumers. Retain those checks and current runtime/package/hosting evidence before publication. The publisher acknowledges the chosen owner/method only after checking private setup metadata and its expiry; it then requires every exact-candidate gate. Do not recreate the old `publish.yml`/`release` OIDC setup without Tory's explicit instruction. Never overwrite a release tag or replace a published package version to hide a failed attempt. Live model reports remain optional under [decision 0012](../architecture/0012-framework-verification-and-optional-model-reports.md).

## Local first-preview publishing procedure

The maintainer creates the scoped key in their signed-in NuGet account and runs `pwsh -File scripts/Set-NuGetCredential.ps1`. Its hidden prompt accepts only the API key. The helper uses Windows current-user encryption and restricts the private directory ACL; no secret is written in plaintext, placed in the repository or added to user-level `NuGet.Config`. No Microsoft password is needed. A future maintainer on another platform must select an appropriate credential store rather than copy the encrypted file.

Freeze and commit the intended source, then run full local verification from that clean commit. Update/check the matching Production fixture site with `Record-HostEvidence.ps1 -AllowPublicHost` only under explicit hosting authorization. The local publisher acknowledges the selected owner/method from private metadata, requires all six candidate gates before decrypting the key, and checks the public registry for immutable-version conflicts.

~~~powershell
pwsh -File scripts/Publish-Preview.ps1 -WhatIf
pwsh -File scripts/Publish-Preview.ps1
pwsh -File scripts/Verify-PublicFeed.ps1
~~~

The publisher retains the exact candidate, original packages/symbols and reports under ignored `artifacts/releases/<version>/<revision>/`. NuGet 7.6+ receives the short-lived key only in the child process environment; command arguments and logs contain no credential. The eight package/symbol pushes run through the official CLI without silently skipping duplicate versions. It requires a successful exit and an acknowledgement for each expected artifact; NuGet 7.9 uses the generic `Your package was pushed.` message for both uploads. A failure preserves partial acceptance results for inspection before any retry. A successful push is followed by registry indexing/owner/payload/symbol checks and public-only consumers; acceptance is not inferred from the CLI alone.

NuGet repository signing changes the container bytes. `package-payload` compares every original ZIP payload entry while permitting only the added root repository signature; changed, missing, extra or ambiguous entries fail. Keep package source/tag/artifact provenance immutable. A later docs-only status commit is distinct from the published package source and must not move the release tag or replace the same package version. The [Step 6 record](../foundation-step6-developer-preview.md) records actual publication/hosting outcomes when complete.

## Prepare the IIS docs bundle

~~~powershell
pwsh -File scripts/Verify.ps1
pwsh -File scripts/Publish-Docs.ps1
~~~

The second command creates a versioned publish folder and ZIP under ignored `artifacts/deploy/`. The folder contains the app, static assets, Production `web.config` and a source manifest. The ZIP is an IIS folder bundle, not an MSDeploy package. Keep publish settings, local settings, databases and private keys out of source and bundles. The server must have the .NET 10 ASP.NET Core hosting runtime and permit Interactive Server circuits/WebSockets.

## Deploy to MonsterASP

Enable Web Deploy in the hosting control panel and download its publish settings to a local private directory. In the site's **Scripting** settings, select the modern .NET runtime, Integrated pipeline, **64-bit (x64)** app pool and **InProcess** hosting. The sealed `win-x64` bundle requires a matching process architecture; MonsterASP's default x86 setting must be changed for this bundle. Each setting is saved separately and can recycle the app pool. See the host's [Scripting guide](https://help.monsterasp.net/websites/configuration/scripting). Use the generated folder path from `artifacts/phase7-docs-path.txt`:

~~~powershell
$bundle = Get-Content artifacts/phase7-docs-path.txt
pwsh -File scripts/Deploy-Docs.ps1 -PublishSettingsPath 'C:\private\site.publishSettings' -PublishDirectory $bundle -ExpectedSiteHost intentblazor.runasp.net -WhatIf
~~~

Review the expected site and repeat without `-WhatIf` to deploy. The helper reads the profile in memory, uses its matching MonsterASP publishing endpoint over HTTPS with certificate validation, preserves destination files, takes the app offline during replacement and suppresses raw native output. It never prints native credentials or copies the profile into the repository. Follow the host's [Web Deploy instructions](https://help.monsterasp.net/websites/deploy/command-line) for server-side setup; the helper keeps TLS validation enabled.

After an explicitly requested deployment, request HTTPS `/health/ready` and verify the expected source revision, `mode=fixture` and version. Open `/docs/quickstart`, `/docs/profiles`, `/demos` and `/explorer` in a real browser. Submit a fixture, select a record and edit a field to prove the Interactive Server connection beyond an HTTP 200 response. Check missing routes and Development-only controls. Keep a prior bundle for rollback and never commit the publish settings. Record host verification for the exact candidate only after public HTTPS and circuit checks pass; the former GitHub workflow variables are not an active deployment or release mechanism.
