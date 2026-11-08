# Foundation Step 6: first developer preview

Tory has authorized NuGet setup, current MonsterASP hosting verification and publication of `0.1.0-preview.1`. The selected owner is [Wilson72](https://www.nuget.org/profiles/Wilson72). The release procedure is prepared; publication and hosting outcomes below are recorded only after their actual checks. Model evaluation remains offline and the supported framework baseline remains .NET 10 Interactive Server, with default `<AiInput />` and `<AiOutput />`.

## Release procedure

Use the [local publishing guide](site/releasing.md). GitHub remains a main-only source/history backup. No Actions, Dependabot, automated release schedule, project-hosted model service or required new component is introduced. Keep Microsoft authentication and NuGet key creation in the maintainer's browser. The one-day push-only `IntentBlazor.*` key is encrypted for the local Windows user outside source, with no credential in process arguments/logs or user NuGet settings.

Run full offline verification from the clean candidate, preserve the exact artifacts and prepare rollback for the prior deployed source. Update the Production fixture site over validated TLS and verify exact HTTPS health/source/fingerprint/runtime/architecture plus actual selection, edit and draft-preserving follow-up. All six release gates must pass before key decryption or package pushes. Public-host checks and package registry operations are authorized Step 6 operations; model calls are not.

The local publisher prevents immutable-version conflicts, retains partial acceptance on failure and pushes all eight packages with their symbols. Public verification checks the actual signed package payloads against the originals, owner/source provenance and symbol availability. Public-only consumers use fresh package caches, clear local/fallback sources, replace framework project references, pin the intended version and execute the quickstart, three examples and production-host recipe without inference.

## Source and artifact evidence

The candidate/report archive under `artifacts/releases/<version>/<revision>/` records the exact package source and original hashes. NuGet may add a repository signature; unchanged ZIP payloads must still match. `PublicPackageComparison_AllowsRepositorySigningAndZipOrderWithoutChangingPayload` and `PublicPackageComparison_RejectsChangedExtraOrMissingPayload` check this distinction offline. `PublicPackageComparison_RejectsUnsafeAmbiguousOrSignatureOnlyArchives` rejects malformed payloads. `PublicConsumerConfiguration_ExcludesLocalSourcesAndFallbackFolders` checks the isolated source configuration; real public-feed execution follows publication.

Publication confirmation, exact immutable source/tag, package links and deployed source will be added after those operations succeed. A docs-only follow-up commit can update the public status without replacing the published package source. The [candidate readiness record](phase-7-readiness.md) distinguishes current-source checks from historical release evidence; optional live model observations remain separate.
