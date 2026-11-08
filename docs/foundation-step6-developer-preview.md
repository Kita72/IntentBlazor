# Foundation Step 6: published developer preview

Completed November 7, 2026. **IntentBlazor `0.1.0-preview.1` is available on NuGet.org under [Wilson72](https://www.nuget.org/profiles/Wilson72).** All six foundation steps are complete. The supported baseline is .NET 10 Interactive Server, with the same default `<AiInput />` and `<AiOutput />` components. Model tests stayed offline.

The immutable package source is [9c204d0f712e4cf355948f6405219db717e2fe73](https://github.com/Kita72/IntentBlazor/commit/9c204d0f712e4cf355948f6405219db717e2fe73), fingerprint `F34607D371D0D539CD4318616D6ACD41AEE07076123A546708999D0E885C2C41`. Repository metadata and SourceLink pin that exact commit. Later documentation and maintainer-tool changes on `main` have their own identity and do not replace the published packages.

## Public packages

| Package | Purpose |
| --- | --- |
| [IntentBlazor.Blazor](https://www.nuget.org/packages/IntentBlazor.Blazor/0.1.0-preview.1) | AiInput, AiOutput and deterministic rendering; includes Runtime/Core transitively. |
| [IntentBlazor.Core](https://www.nuget.org/packages/IntentBlazor.Core/0.1.0-preview.1) | Versioned document/data contracts, strict parsing and validation. |
| [IntentBlazor.Runtime](https://www.nuget.org/packages/IntentBlazor.Runtime/0.1.0-preview.1) | Scoped sessions, protected state, actions and persistence contracts. |
| [IntentBlazor.AI](https://www.nuget.org/packages/IntentBlazor.AI/0.1.0-preview.1) | Ollama, LM Studio, OpenAI, custom IChatClient and experimental Codex adapters. |
| [IntentBlazor.Storage.Sqlite](https://www.nuget.org/packages/IntentBlazor.Storage.Sqlite/0.1.0-preview.1) | Private durable workspaces and action receipts. |
| [IntentBlazor.Content](https://www.nuget.org/packages/IntentBlazor.Content/0.1.0-preview.1) | Bounded content adapters, owned artifacts and exports. |
| [IntentBlazor.Packs.ImageEditor](https://www.nuget.org/packages/IntentBlazor.Packs.ImageEditor/0.1.0-preview.1) | Typed image comparison/zoom and real PNG resize actions. |
| [IntentBlazor.Blazor.AI](https://www.nuget.org/packages/IntentBlazor.Blazor.AI/0.1.0-preview.1) | Optional embeddable AiSetup and AiInspector. |

In a fresh Interactive Server Blazor app, start with:

~~~powershell
dotnet add package IntentBlazor.Blazor --version 0.1.0-preview.1
~~~

Follow the [installation guide](site/installation.md) and [compiled quickstart](site/quickstart.md) for service registrations and the two-component page. Add the AI package when connecting a model. The public [docs](https://intentblazor.runasp.net/docs/quickstart), [explorer](https://intentblazor.runasp.net/explorer) and [demo hub](https://intentblazor.runasp.net/demos) use deterministic fixtures.

## Release evidence

Before publication, full `Verify.ps1` passed for the clean frozen source: **484 unit/HTTP/rendering cases, 16 serial browser cases, eight package/symbol audits, five independent local-package apps and nine renderer workloads**. Formatting, locked restore, documentation links, real published PNG/SQLite operations and the Production bundle browser upgrade/rollback checks passed. All six mandatory release gates passed before credential decryption and uploads; optional live model reports were absent.

After publication:

- All eight package/symbol uploads were accepted and the exact version became available from the public package feed. Each public package page showed Wilson72 as owner and a working symbol-download link.
- All eight downloaded `.nupkg` files passed `dotnet nuget verify --all`. NuGet's added repository signatures were allowed; every original ZIP payload entry still matched the frozen original.
- All eight downloaded `.snupkg` payloads matched the originals. Public metadata, MIT license, exact repository commit, dependencies and portable-PDB SourceLink checks passed.
- `Verify-PublicFeed.ps1` restored, built and ran Quickstart, Gaming News, Staff Business, Asset Workbench and ProductionHost from fresh isolated caches. Configurations cleared local/fallback feeds, replaced framework project references and pinned `0.1.0-preview.1`; asset metadata confirmed NuGet.org as the framework source. Their representative fixture/service/draft/action/image workflows passed without inference.
- The public MonsterASP candidate passed matching HTTPS health identity and actual request, selection and draft-preserving circuit interactions. Expected public guides returned 200; missing/Development-only routes returned 404, and the synthetic sign-in route issued no cookie.

The first publisher stopped after Core because NuGet 7.9 uses `Your package was pushed.` for both package and symbol uploads. The account showed both artifacts pending validation. Remaining uploads used the original archived files, and public downloads independently verified all eight pairs. The follow-up fixes acknowledgement handling and adds nine offline regression cases; **493 unit/HTTP/rendering cases passed**. No published version was rebuilt or replaced to address that tooling issue.

| Requirement | Evidence |
| --- | --- |
| Repository signing preserves original payload | `PublicPackageComparison_AllowsRepositorySigningAndZipOrderWithoutChangingPayload`; all eight public package signature and payload checks passed. |
| Altered or malformed payloads fail | `PublicPackageComparison_RejectsChangedExtraOrMissingPayload`, `PublicPackageComparison_RejectsUnsafeAmbiguousOrSignatureOnlyArchives`; all sixteen downloaded artifact payloads matched. |
| Public consumers cannot resolve local framework packages | `PublicConsumerConfiguration_ExcludesLocalSourcesAndFallbackFolders`; five public-only apps passed and their package-source metadata was checked. |
| Both upload acknowledgements and a successful process exit are required | `NuGetAcknowledgement_RequiresSuccessfulPackageAndSymbolUploads` covers generic/named symbol messages, failures, missing/wrong/reversed uploads and duplicate acknowledgements. |
| Published IIS app handles native interaction and rollback | `IisBundle_HealthCircuitAndRollbackPreserveUnmanagedData` plus separately recorded public HTTPS/source/selection/request/draft observations. |

The local archive is `artifacts/releases/0.1.0-preview.1/9c204d0f712e4cf355948f6405219db717e2fe73/`. It retains original packages/symbols, clean-source readiness/receipts, public artifact/owner/consumer evidence, a complete Git source bundle and the final publication-verification result. Logs, downloaded public artifacts and screenshots are under ignored `artifacts/step6/`. These local reports contain no publishing key or Web Deploy profile.

## Hosting and maintenance

The initial verified release bundle contains 99 files and 34,736,354 bytes. MonsterASP's site app pool was changed from x86 to x64 to match its `win-x64` native dependencies. The verified runtime is `.NET 10.0.14`; hosting is Interactive Server in Production fixture mode. TLS validation remained enabled and Web Deploy did not delete destination files. The prior deployment was retained and restored after the initial architecture mismatch; the corrected deployment then passed. See the [hosting record](phase-7-hosting.md).

The one-day publishing key is protected for the local Windows user outside the repository and was passed to the official CLI only through its child environment. The [release guide](site/releasing.md) documents future publication, partial-failure inspection and public-feed verification. Every later package release needs a new version and freshly matched evidence. Ordinary source pushes do not authorize publication or deployment.

GitHub remains a main-only source/history backup with no Actions, Dependabot or new repository automation. The immutable source commit identifies this preview; no separate GitHub Release, discussion or announcement was published. The hosted guides can advance through independently verified docs deployments while the package-source archive remains unchanged.

## Preview boundaries and expansion

The API is experimental. Local and paid model evaluation remains optional and unverified; no Ollama/LM Studio service or paid OpenAI/Codex inference was started. SDK 11/net10 checks passed in Step 5; native net11 targeting remains unverified after the recorded analyzer failure. WASM/MAUI Hybrid, multi-node stores, broader media fidelity, semantic memory/RAG and full assistive-technology review remain separate expansion work. See the [foundation plan](foundation-completion-plan.md), [provider matrix](provider-evaluation.md) and [preview limits](site/limitations.md).
