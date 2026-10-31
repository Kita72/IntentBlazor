# IntentBlazor.Content

Optional bounded files and private artifacts for IntentBlazor's two-component framework.

**Developer preview candidate.** Package/API version `0.1.0-preview.1`; target `net10.0`. The initial hosting target is a Blazor Web App with Interactive Server. Public publication and live-provider results are tracked separately in the [release status](https://github.com/Kita72/IntentBlazor/blob/main/docs/phase-7-readiness.md).

Add content services for UTF-8 text/Markdown, bounded JSON/CSV, PNG/JPEG and text-based PDF. Store bytes outside static folders and map authenticated artifact delivery. This does not imply OCR, arbitrary document editing, vision-model compatibility or a generative image service.

Start with the [independent quickstart](https://github.com/Kita72/IntentBlazor/tree/main/examples/Quickstart), [installation guide](https://github.com/Kita72/IntentBlazor/blob/main/docs/site/installation.md), and [contract reference](https://github.com/Kita72/IntentBlazor/blob/main/docs/site/nodes.md). The framework needs no project-operated service. Fixture mode requires no model account; configure model credentials only on the trusted host when opting into inference.

A model proposes presentation; application services own authorization, writes, identity and persistence. Generated content is never permission. See [provider evidence](https://github.com/Kita72/IntentBlazor/blob/main/docs/provider-evaluation.md), [hosting limitations](https://github.com/Kita72/IntentBlazor/blob/main/docs/site/hosting.md), [contribution guide](https://github.com/Kita72/IntentBlazor/blob/main/CONTRIBUTING.md), and [security policy](https://github.com/Kita72/IntentBlazor/blob/main/SECURITY.md).

MIT licensed. Dependencies retain their own licenses; see [third-party notices](https://github.com/Kita72/IntentBlazor/blob/main/THIRD-PARTY-NOTICES.md).
