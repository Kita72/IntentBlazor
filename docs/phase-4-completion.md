# Phase 4 completion record

Recorded October 30, 2026. Phase 4 is implemented for the experimental Interactive Server target. Phase 5 has not started. This is implementation and deterministic validation evidence, not a public package release or live-model certification.

## Delivered

- Optional `IntentBlazor.Content`, `IntentBlazor.Packs.ImageEditor`, and `IntentBlazor.Blazor.AI` packages. The consumer interaction surface remains `AiInput` and `AiOutput`; uploads are discovered through optional DI services.
- Original-preserving UTF-8 text/Markdown, JSON/CSV, PNG/JPEG, and bounded text-PDF adapters; owner-scoped immutable file artifacts, provenance/parents, quotas, explicit parent-aware cleanup, and authenticated delivery. JSON/CSV exports and resized PNGs contain actual bytes.
- Schema 0.3 Markdown, gallery, bar/line chart, artifact, and extension nodes. Hosts register typed configurations, local events, renderer components, examples, and exact capability versions through public APIs. Required versions fail closed; missing optional renderers show encoded fallback text.
- An image pack with an actual nearest-neighbor resize action, local zoom, recoverable original, and checkpointed comparison choice. Ordinary document inputs and action buttons compose inside the pack.
- Host-approved model presets; scoped generation settings with current-policy checks; unsupported-option explanations; explicit connection/presentation probes; and a policy-controlled inspector showing validated state and bounded result metadata. Credentials/endpoints remain host-owned, outside output documents.
- A private deterministic content demo, [developer guide](phase-4-content-and-setup.md), [contract reference](../specs/output-document/v0.3-content-and-extensions.md), [architecture decision](architecture/0007-content-extensions-and-setup.md), and [dependency notices](../THIRD-PARTY-NOTICES.md).

## Repository and package evidence

`pwsh -File scripts/Verify.ps1` completed successfully against the final source changes:

- Locked dependency restore and Release build: zero warnings and zero errors.
- Formatting verification passed.
- **Passed: 224, Failed: 0, Skipped: 0.** This includes the existing three-domain fixtures and Phase 3 recovery/action suite. The [test evidence matrix](../tests/README.md) names the specific behavioral tests.
- Local Markdown links passed; all eight source libraries packed locally as `0.1.0-alpha.0`.

A separate non-incremental Release build also completed with zero warnings/errors before the final AI-policy hardening. The final policy changes then passed the full repository verification above.

A new console consumer was created outside the repository with a fresh isolated NuGet package cache, no project references, and only local IntentBlazor NuGet packages plus their public dependencies. It passed real PNG import/resize and pixel checks, original-parent provenance, CSV export, SQLite save/new-scope restore, default-parameter AiInput/AiOutput rendering, registered pack event compilation, policy-controlled AiSetup/AiInspector rendering, and scope-local effective settings. A throwing sentinel client ensured that this package check could not perform model inference. SDK request generation and next-generation setting behavior are independently covered by deterministic tests.

Local logs, the consumer verification script, browser screenshots, and downloaded files are under ignored `artifacts/phase4-proof`. Temporary consumer projects and databases are not source deliverables or published packages.

## Native browser evidence

The rebuilt Development-only `/content` demo was exercised in native Edge with its protected synthetic identity:

1. Created and selected an actual 64 by 64 PNG.
2. Edited target dimensions to 16 by 24, executed **Resize preview**, and saw the acknowledged derived artifact while retaining the original.
3. Switched **Show original** and **Show preview**, selected 4× local zoom, and confirmed the displayed artifact changed. Reload restored the meaningful comparison choice and artifact references; zoom intentionally remains local to the renderer.
4. Downloaded the displayed image. Its PNG signature and header identify a real 141-byte, 16 by 24 PNG.
5. Created and downloaded actual CSV and JSON metadata exports, inspected their previews, and parsed the downloaded files. The CSV included both original and derived image records.
6. Confirmed AI setup reports no host-approved connection in the credential-free default demo and disables Apply/Probe accordingly. Editable setting execution, provider-specific request bodies, and policy revocation are tested without a live provider.

The browser check found missing `Microsoft.AspNetCore.Components.Web` imports in the two new Razor libraries: the initial views rendered literal event directives. Those imports were fixed, rendering regressions now reject literal event attributes, and actual image comparison/zoom clicks were rechecked successfully in Edge.

File selection through the browser automation extension was blocked by Edge's disabled **Allow access to file URLs** permission. No browser permission was changed. This run therefore does not claim a completed native-browser file upload. The real streamed-import pipeline for every declared format, byte/count limits, cancellation, malformed files, and owner boundaries passed automated tests; the default AiInput attachment integration also passed actual Blazor HTML rendering and fresh-package checks. Browser file selection remains a manual UI verification item on a browser with that permission enabled.

## Boundaries and next phase

Markdown is a safe block subset. PDF support extracts bounded text, without OCR or faithful page rendering. Images contribute metadata to model context; this phase does not imply vision or image generation. The file store is a single-instance adapter; hosts own identity, retention, backups, and any distributed replacement. Parser limits are not a process sandbox.

Live Ollama, LM Studio, OpenAI, and Codex evaluation remains the separately deferred Phase 2 gate. Neither scripted SDK checks nor a successful connection probe certify tool behavior for every model.

Three phases remain: **5** complete the Gaming News, Staff Business, and Asset Workbench apps; **6** complete the docs/demo site; **7** verify and publish the developer preview. Public release also requires the deferred live-model gate. Maintainer review and merging follow [CONTRIBUTING.md](../CONTRIBUTING.md).
