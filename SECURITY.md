# Security Policy

IntentBlazor is pre-preview software. No production-supported release exists yet.

Report suspected vulnerabilities through [GitHub private vulnerability reporting](https://github.com/Kita72/IntentBlazor/security/advisories/new). If that route is unavailable, contact Kita72 through the [GitHub profile](https://github.com/Kita72) to arrange a private channel before sharing technical details. Do not disclose secrets or exploit details in a public issue.

Include affected commit or version, reproduction steps using synthetic data, expected boundaries, and the observed impact. Never include real credentials or private customer information.

The initial maintainer coordinates triage and disclosure. Response times and fix timelines depend on available capacity; this project does not currently offer a response-time guarantee.

Important boundaries include session and tenant ownership, server-side tool authorization, untrusted uploaded/model content, generated UI event validation, persistence, and credential handling.
