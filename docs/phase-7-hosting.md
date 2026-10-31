# Public docs hosting verification

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

Use the [release/deployment guide](site/releasing.md). Keep the profile in its private local location; it is neither committed nor copied into the publish bundle. The helper invokes MSDeploy directly, uses its native option quoting, preserves destination files and suppresses credential-bearing native output. Check the public health source revision and a fresh interactive session after each deployment; a successful upload alone is insufficient.

Docs hosting is now verified independently of NuGet publication. The remaining public package release gates are documented in the [candidate readiness record](phase-7-readiness.md). A NuGet owner/policy and current successful local/frontier evidence are still required; OpenAI remains deferred.
