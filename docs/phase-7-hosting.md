# Public docs hosting verification

## November 7 developer preview

Foundation Step 6 separately authorized live hosting operations while keeping model tests offline. The frozen package-source deployment, [9c204d0f712e4cf355948f6405219db717e2fe73](https://github.com/Kita72/IntentBlazor/commit/9c204d0f712e4cf355948f6405219db717e2fe73), passed public HTTPS health and native browser circuit checks on MonsterASP. Health reported `0.1.0-preview.1`, fingerprint `F34607D371D0D539CD4318616D6ACD41AEE07076123A546708999D0E885C2C41`, `.NET 10.0.14`, `X64`, `fixture` and `InteractiveServer`.

The control panel initially showed a 32-bit app pool. The first x64 deployment failed its health gate and the retained prior bundle was restored. The site was then configured for 64-bit execution, the same frozen bundle was redeployed, and matching source/fingerprint plus real request, selection and draft retention passed. Web Deploy preserved destination files and kept TLS validation enabled. The prior payload and a local no-delete overlay/rollback rehearsal were retained.

Public `/`, `/docs/quickstart`, `/docs/developer-preview`, `/docs/publishing`, `/demos` and `/models` returned 200. An unknown guide and `/workspaces/demo-signin` returned 404; the Development-only sign-in route issued no cookie. No model credential or inference was enabled. The [publication record](foundation-step6-developer-preview.md) records the independently verified public packages and five package-only apps.

Release artifacts/evidence remain under `artifacts/releases/0.1.0-preview.1/9c204d0f712e4cf355948f6405219db717e2fe73/`. Documentation and maintainer-tool follow-ups on `main` have separate deployment identities; `/health/ready` always reports the running docs source. Check each later deployment with fresh health/circuit evidence rather than relabelling the package-source observation.

## Initial October deployment

On 2026-10-30 (America/Edmonton), the candidate docs app was deployed to [intentblazor.runasp.net](https://intentblazor.runasp.net/) using the maintainer-provided MonsterASP Web Deploy profile. The initial verified application source was commit `32223315bef41b507e64ac2ae90aa86c5e467f78`; Windows and Ubuntu [CI both passed](https://github.com/Kita72/IntentBlazor/actions/runs/41171809321). `/health/ready` reports the currently deployed source revision, version, fixture mode and Interactive Server hosting.

## Observed public behavior

- HTTPS certificate validation succeeded for the public site and the publishing endpoint. TLS checks were not bypassed.
- `/`, `/docs/quickstart`, `/docs/readiness`, `/docs/publishing`, `/docs/profiles`, `/demos` and `/models` returned 200. An unknown guide and the Development-only `/workspaces/demo-signin` returned 404; that sign-in route issued no cookie.
- `/models` showed model mode disabled. The deployment manifest and IIS configuration selected Production and fixture mode. No key or live inference was configured on the public site.
- In Edge, the public explorer established a WebSocket connection with no observed console errors. News selection showed its article, source and rating. Staff keyboard selection opened the editor; sorting retained button focus. A direct note edit and selected record survived a subsequent `staff` submission through AiInput.
- Invalid JSON/schema attempts retained the accepted output; replacing a dirty workspace was refused. After clearing the disposable draft, schema 99 produced the supported-schema error and kept the working document.
- A schema 0.3 chart was composed in the public JSON editor from the existing authorized staff dataset. Its SVG and expandable data table rendered through AiOutput. At 320 pixels, Tab reached the labelled data region; the table had a caption and column scopes. The wider job table scrolled inside its focusable region. Page content stayed within the viewport at 320 and 390 pixels.
- The asset fixture opened and selected the crate sprite, showing dimensions and an editable working label. These are deterministic examples, not live image generation or production business writes.

HTTP checks and screenshots are local ignored evidence under `artifacts/phase7/evidence/` and `artifacts/phase7-proof/`. Browser checks exercise actual deployed controls; they are separate from the deterministic HTML-renderer tests. They do not certify all assistive technologies, hosting plans, browsers or model profiles.

## Repeating deployment

November 7 foundation Step 5 adds a sealed win-x64 bundle, real native dependency checks, an offline Production-mode browser upgrade/rollback rehearsal, and a separately invoked current-candidate host recorder. See the [release operations record](foundation-step5-release-operations.md). No public requests, Web Deploy execution or new deployment were performed for that offline task. The historical observations above cannot satisfy the new candidate's host gate.

Use the [release/deployment guide](site/releasing.md). Keep the profile in its private local location; it is neither committed nor copied into the publish bundle. The helper invokes MSDeploy directly, uses its native option quoting, preserves destination files and suppresses credential-bearing native output. Check the public health source revision and a fresh interactive session after each deployment; a successful upload alone is insufficient.

Docs hosting is verified independently of NuGet publication. Future package releases retain the gates documented in the [readiness record](phase-7-readiness.md): framework/package/runtime verification, a confirmed NuGet owner/policy and candidate hosting evidence. The November 7 release outcomes are recorded above. Live local/frontier reports are optional under [decision 0012](architecture/0012-framework-verification-and-optional-model-reports.md).
