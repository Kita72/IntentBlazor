# Documentation and independently consumable samples

Status: accepted and implemented in Phase 6. No framework public contract changes.

The Blazor docs host serves a whitelist of authored repository Markdown as embedded resources through static SSR routes. Guides, specifications, errors, contribution policy, and release notes stay in source control. Markdig handles ordinary Markdown with raw HTML disabled; local links map to catalog routes or repository source and unsafe schemes are inert. Model output never enters this documentation renderer; the framework's bounded Markdown node retains its own safe subset.

Quickstart is an independent, non-packable Blazor application using only public framework contracts. Documentation embeds its actual compiled source files, avoiding a second uncompiled copy of executable examples. Verification builds another copy outside the repository against local packed libraries and isolated NuGet sources. Neither application depends on a project-operated service.

The explorer owns a private non-durable session and authorized synthetic fixture data. Presentation edits cannot introduce datasets, registered actions, or installed renderers. Invalid proposals retain the working output. Pending JSON drafts and output drafts block destructive replacements; the user explicitly restores JSON or clears output edits. Static docs use no circuit; only demonstrations use page-level Interactive Server.

The public documentation host remains in credential-free fixture mode. Optional live demonstration is Development-only, requires explicit host configuration, and shows per-request budgets and provider evidence. Local provider URLs refer to the server. Production authentication/quotas, live-model evaluation, deployment, package publication, and the final preview gate remain separate Phase 7 work.

Acceptance evidence: [Phase 6 completion](../phase-6-completion.md), catalog/explorer/quickstart tests, packed-consumer build, and recorded keyboard/small-screen checks. Maintainer-directed work may be committed directly after checks under Tory's standing authorization; external contributors retain the documented PR process.
