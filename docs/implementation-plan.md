# IntentBlazor Developer Preview Implementation Plan

Prepared for Tory on October 29, 2026.

Build an open-source Blazor framework whose two core interaction components accept user intent and produce a dynamic, interactive workspace. Deliver it as reusable NuGet packages, accompanied by a Blazor documentation and demonstration website and three independently runnable example applications.

The first public milestone is a developer preview. Its purpose is to prove that developers can install the packages, connect a supported model, expose C# application capabilities, and obtain useful interactive output without writing each screen. Production readiness, universal model compatibility, and comprehensive component coverage are later milestones.

## Product commitments

- Provide two core interaction components: AiInput and AiOutput. Optional setup, diagnostics, and workspace-management UI are supporting developer tools.
- Support attributes as the easy tool-registration option and explicit registration for existing services and advanced scenarios.
- Let developers own their application, identity, business rules, data, hosting, and surrounding UI.
- Compose familiar controls, combine their state and behaviour, and extend output through specialist renderers and tools.
- Support selected frontier models and local models through LM Studio and Ollama, with a published matrix of combinations actually verified.
- Connect input and output through a shared logical session that includes conversation, workspace state, data references, artifacts, and operation results.
- Make natural language and direct manipulation operate on the same state.
- Keep application actions connected to real service execution. A generated interface cannot create authority or declare an operation successful before execution succeeds.
- Develop in public with clear contribution routes, documented contracts, a public roadmap, and release notes.
- The project name and namespace prefix are IntentBlazor. The repository is Kita72/IntentBlazor, and the license is MIT, selected during Phase 0.

## Developer preview scope

Include a default presentation toolkit with responsive layouts, text and Markdown, cards, lists, tables, ratings, forms, buttons, simple charts, image galleries, and file or artifact references. Include one image-editor extension sufficient for the asset example.

Accept text, Markdown, JSON, CSV, PNG, and JPEG in the initial content path. Add bounded PDF text extraction during the content phase. Support downloadable JSON, CSV, and image artifacts for the initial examples. Publish an explicit capability table distinguishing upload, preview, extraction, editing, generation, and export. These operations are separate capabilities.

Provide an in-memory session option for experiments and a SQLite persistence adapter for resumable single-instance examples. The framework must expose storage contracts so applications can supply PostgreSQL, distributed stores, or other implementations.

Begin with Interactive Server as the supported interactive hosting model. Use ordinary static rendering for documentation where appropriate. Design the boundary between browser UI and the server runtime so future WebAssembly and MAUI Hybrid support can be added without changing the output document. Do not advertise those hosting models as supported until their own acceptance work is complete.

Defer arbitrary generated executable scripts, runtime Razor compilation, full real-time collaborative editing, autonomous cross-application agents, advanced 3D editing, complete Office document fidelity, and parity with mature component suites. Preserve extension points for these directions.

## Architecture and package boundaries

Use Microsoft.Extensions.AI abstractions for model access and existing provider implementations where suitable. Put multi-step tool execution behind a runtime abstraction, using Microsoft Agent Framework where its selected release meets requirements. Record the selected package versions and any prerelease dependencies in an architecture decision.

Do not put model-provider SDK calls in business services or the renderer. Keep provider configuration, persistence, and tool execution replaceable.

Proposed responsibilities:

| Area | Responsibility |
| --- | --- |
| Core contracts | Output documents, node descriptors, bindings, state definitions, events, tool metadata, errors, version rules, and extension registration contracts. |
| Runtime | Sessions, context construction, capability discovery, model integration, bounded tool execution, authorized dataset and artifact registration, validation, and document updates. |
| Blazor library | AiInput, AiOutput, internal toolkit renderers, local interactions, browser adapters, accessible defaults, and optional setup or inspection UI. |
| Storage adapter | SQLite persistence, migrations, retention hooks, and restoration through the storage contracts. |
| Example applications | Domain services, domain permissions, content sources, and the workflows that demonstrate public framework APIs. |
| Documentation application | Quickstarts, reference documentation, document explorer, model setup, contribution guidance, and demonstrations. |

Start with a small number of projects. Split provider-specific or content-specific packages when dependencies and maintenance requirements justify it. Internal toolkit components do not need to become separate public packages.

Proposed repository layout:

- src/Framework.Core
- src/Framework.Runtime
- src/Framework.Blazor
- src/Framework.Storage.Sqlite
- apps/Framework.Docs
- examples/GamingNews
- examples/StaffBusiness
- examples/AssetWorkbench
- tests/Contracts
- tests/Runtime
- tests/Blazor
- tests/Browser
- tests/PackageConsumer
- specs/output-document
- specs/fixtures
- docs/architecture
- docs/contributing
- .github

These are provisional folder names. The final naming decision applies consistently across namespaces, packages, repository metadata, documentation, and release assets.

Razor class libraries support sharing Razor components and their static assets through NuGet. The documentation site and examples must consume the same public APIs that external developers receive. [Microsoft Razor class library guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0).

## Output document contract

Use typed C# contracts with a canonical JSON representation. YAML in earlier discussions is explanatory notation.

The logical document includes:

| Part | Required behaviour |
| --- | --- |
| Schema and toolkit versions | Identify the supported format and required renderer capabilities. Reject unsupported requirements with a useful error. |
| Document identity and revision | Identify the workspace and detect outdated updates. |
| Data references | Refer to runtime-owned datasets and artifacts, including record keys, versions, field metadata, and source attribution when available. |
| State | Hold selections, filters, draft values, viewport settings, active tools, and artifact references with declared types. |
| View | Define stable node IDs, layouts, supported properties, and typed bindings. |
| Interactions | Connect validated events to local effects or registered application actions. |
| Extension requirements | Declare specialist renderers and their contract versions. |
| Results and status | Represent loading, empty, failed, cancelled, conflicted, and completed operations from actual runtime outcomes. |

The model proposes structure and behaviour. The runtime establishes which data, artifacts, and actions are accessible to the current session. Persisted references do not grant access after restoration.

Bindings are restricted typed paths and supported operations. They cannot evaluate arbitrary C#, JavaScript, URLs, SQL, or shell commands. Layout and event descriptions must have explicit limits on size, depth, and work.

Apply updates against an expected document revision. Validate complete update units before committing them. Keep the last working interface available on a failed proposal. Preserve dirty fields or return an explicit conflict when an update affects them.

Define serialization, bounded JSON values, decimal and date handling, missing and null values, record identity, extension payloads, error envelopes, and schema evolution before widening the renderer set. Model-friendly representations must map consistently to the canonical contracts.

## Session and memory model

A logical session is more durable than a Blazor circuit. A scoped AiSession can provide the convenient default for an Interactive Server page, but persistent identity, ownership, and restoration must be explicit runtime concepts.

Store these separately:

- Conversation messages, tool requests, actual results, and relevant user interaction events.
- Workspace document, current revision, selections, filters, and exact unfinished values.
- Dataset descriptors and references to retrievable results.
- Artifact metadata, versions, ownership, and recoverable working copies.
- Compact session summaries and unresolved task information.
- Optional lasting user or workspace memory with an explicit scope.

Construct model context from the current request, authoritative workspace state, recent history, selected tool descriptions, and relevant retrieved material. Do not resend every stored message, file, or toolkit example.

Begin with recent history and a bounded summary policy. Add source-aware retrieval only when the example workloads require it. Keep original records available because summaries can omit details.

A persisted session must recheck the current user or tenant before loading data or resuming an action. Prevent duplicate execution of committed writes after cancellation, retry, reconnect, or process restart. Let the host provide idempotency semantics for its business operations.

Keep cancellation, disposal, multiple sessions per user, and multiple input/output pairs explicit. Never use a global mutable session shared by unrelated users.

## Phase overview

| Phase | Outcome | Dependency |
| --- | --- | --- |
| 0 | Open-source foundation and recorded architecture decisions | This plan |
| 1 | Versioned contracts and deterministic document rendering | Phase 0 |
| 2 | One complete interaction through a real model | Phase 1 |
| 3 | Stateful interactions and resumable workspaces | Phase 2 |
| 4 | Content, extension tools, and embeddable AI setup | Phases 2 and 3 |
| 5 | Three independently runnable example applications | Phases 3 and 4 |
| 6 | Complete documentation and demo website | Website begins in Phase 0; final content depends on Phase 5 |
| 7 | Contributor validation and developer preview release | All previous gates |

These are milestone phases, not calendar estimates. Update effort estimates after the contract and first live model interaction expose the real integration work. Documentation and contributor-friendly issues grow throughout the phases.

## Phase 0 Open-source foundation and architecture decisions

Establish the repository as a project other developers can understand and contribute to before the preview is published.

Deliverables:

- Select the public name, GitHub owner or organisation, repository, package prefix, copyright holder, and maintainer contact.
- Adopt a license before publishing source. Recommend MIT as the initial proposal; confirm that choice and the treatment of sample assets before creating the final LICENSE file. MIT permits commercial use, modification, and redistribution subject to its notice conditions. [MIT license reference](https://choosealicense.com/licenses/mit/).
- Create README, CONTRIBUTING, CODE_OF_CONDUCT, SECURITY, GOVERNANCE, CHANGELOG, and a public roadmap.
- Describe Tory's initial role as lead architect and maintainer; define review, release, decision, and future maintainer responsibilities.
- State how contributor submissions are licensed. Keep any additional contributor agreement decision explicit.
- Add issue forms for defects, feature proposals, provider compatibility, and documentation; add a pull-request template.
- Use Discussions for questions and early ideas, issues for accepted work, and short requests for comments for public contract or architectural changes.
- Define labels such as good first issue, help wanted, documentation, provider, renderer, accessibility, and breaking change. Create milestones corresponding to the phases.
- Configure review and branch rules, maintainer ownership, dependency update handling, secret protection where available, and private vulnerability reporting.
- Pin the SDK, centralize dependency versions, add formatting and analyzer settings, and establish reproducible restore, build, test, and package scripts.
- Bootstrap a documentation Blazor application with project purpose, roadmap, contributor setup, and a clear implementation-status page.
- Establish CI that builds the solution and executes deterministic checks without model credentials. Run it on fork pull requests without privileged secrets.

Platform recommendation: use .NET 10 as the initial stable package baseline and add .NET 11 compatibility checks. At implementation kickoff, verify the available SDKs and .NET 11 release status; record whether the docs and examples should move to .NET 11 once generally available. Avoid requiring prerelease APIs without a demonstrated need. As of this plan, Microsoft's support page lists .NET 10 as LTS and .NET 11 as release candidates. [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

Record decisions for the first schema, hosting support, orchestration dependency, package boundaries, settings visibility, storage ownership, and licensing. The decisions can evolve through documented proposals.

GitHub's community profile checks files such as README, LICENSE, CONTRIBUTING, and CODE_OF_CONDUCT. Use that profile as a discoverability check alongside the project's own contributor acceptance work. [GitHub community profile guidance](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories).

Exit gate: a new contributor can understand the scope, build the starter solution, run deterministic checks, find the roadmap, and identify how to propose a change. Repository visibility, naming, and license decisions are recorded before public publication.

## Phase 1 Contracts and deterministic renderer

Prove the output language and interactions without depending on a model.

Deliverables:

- Implement output contracts, schema validation, renderer descriptors, typed bindings, node identities, and structured error reporting.
- Implement AiInput and AiOutput with one explicitly shared session; include loading, empty, error, and cancellation presentation.
- Implement the smallest useful toolkit: layout, text, card, list, table, rating, button, select, and simple form.
- Support local state changes and conditional presentation without invoking a model.
- Create authored document fixtures for the news reader, staff assignment view, and asset gallery. These fixtures are development samples, not claims of live data.
- Add a document explorer to the docs application so contributors can inspect a document and render it.
- Provide deterministic playback of known model/tool responses as a clearly labelled development mode.
- Reject unknown actions, malformed bindings, invalid extension requirements, excessive document sizes, and invalid state transitions.

Exit gate: all three document fixtures render through the same core contract. Selecting a news item, choosing a staff member in a draft, and selecting an image update the correct workspace without cross-session state leakage.

## Phase 2 First real model interaction

Prove the drop-and-go developer journey from a fresh consuming app.

Deliverables:

- Implement framework service registration and a provider-neutral model connection.
- Support opt-in tool attributes and explicit tool registration, using the same runtime metadata and validation path.
- Define descriptions, parameter schemas, result schemas, service lifetimes, cancellation, read/write classification, and error behaviour.
- Generate toolkit instructions from registered descriptors; include only relevant capability details and examples in context.
- Connect one local model profile first, then one selected frontier provider. Verify LM Studio and Ollama independently before listing both as supported.
- Implement streaming, timeout handling, bounded correction of invalid UI proposals, tool-step limits, context budgets, and usage accounting where the provider reports it.
- Support typed output or tool-based presentation requests according to verified model capabilities. A connection test alone is not evidence of successful tool use.
- Begin the GamingNews example with a fixture-backed content service and a real model composing the output.
- Document the first installation guide, C# service example, and two-component page.

Exit gate: a fresh app using the public package surface can connect a verified model, expose a service, and answer a request with an interactive list or table. No handwritten domain-specific Razor view is required for that workflow. At least one real local and one real frontier-model run is recorded with model/version details.

## Phase 3 Stateful interactions and persistence

Make the workspace survive follow-up requests, direct edits, and reconnection.

Implementation status: completed through deterministic tests and the Development-only SQLite recovery demo. See [the Phase 3 guide](phase-3-workspaces.md) and [decision 0006](architecture/0006-durable-workspaces.md). Summary generation currently means a bounded deterministic extractive history index; semantic summarization, lasting memory, and transcript retrieval are not implemented. The Phase 2 live-model gate remains deferred independently.

Deliverables:

- Implement revision-checked document updates, dirty-field protection, validated event payloads, and deterministic local effects.
- Bind UI submissions to explicitly registered server actions and structured results.
- Implement ownership checks, record version conflicts, cancellation, replay protection, and host-supplied idempotency support.
- Implement session storage interfaces, in-memory storage, and the SQLite adapter.
- Restore the document, exact draft values, selections, artifact references, and conversation continuity.
- Add bounded history selection and summary generation while retaining original records.
- Separate private sessions from shared resources. Keep shared-session collaboration out of the default behaviour.
- Add contract migration or a clear incompatibility message for saved sessions from older schema versions.

Exit gate: a user edits a form, asks a follow-up, reconnects, and recovers the draft. An outdated document update cannot silently overwrite it. Two staff sessions cannot read one another's private context, and conflicting edits produce a recoverable result.

## Phase 4 Content tools and developer setup

Expand expressiveness without making the consuming page more complicated.

Implementation status: implemented through optional content, image-editor, and AI setup packages, schema 0.3 contracts, deterministic tests, and the Development-only content demo. See [the Phase 4 guide](phase-4-content-and-setup.md) and [decision 0007](architecture/0007-content-extensions-and-setup.md). Markdown is a safe block subset; PDF extraction is text-only; image model context currently contains metadata, not pixels. Live-model verification remains deferred independently.

Deliverables:

- Add the initial content adapters and artifact store contracts, with configured size limits, ownership, provenance, and cleanup.
- Treat uploaded text, documents, and images as content, not authority to invoke tools or change application policy.
- Add cards, simple chart types, image galleries, bounded PDF text extraction, and the declared export formats.
- Implement renderer and capability-pack registration with typed configuration, events, example documents, and version requirements.
- Add an image editing pack with a preview surface and one deterministic editing operation for the asset example.
- Keep high-frequency pointer and viewport work inside the rendering adapter; synchronize meaningful state changes with the session.
- Provide embeddable connection setup, profile editing, connection/capability tests, and a development inspector.
- Support common generation options and advanced provider-specific options. Show unsupported options and the effective configuration.
- Separate administrator-controlled settings from user-adjustable preferences. Keep credentials on the appropriate trusted host and out of persisted output documents and diagnostics.
- Expose tool calls, results metadata, rendered documents, timings, and structured errors for debugging. Do not expose hidden model reasoning or log sensitive payloads by default.
- Provide documented fallback output when a model or renderer lacks a required capability.

Exit gate: a contributor can register a specialist renderer or tool through public extension contracts without changing core source. Selected settings affect the active profile, sensitive configuration stays outside client output, and unsupported capability paths produce useful behaviour.

## Phase 5 Three example applications

Implementation status: implemented as three independently runnable Interactive Server apps with default AiInput/AiOutput, synthetic Development-only identities, private workspaces, and real app-owned news/library, EF Core assignment, and image-version operations. See the [Phase 5 guide](phase-5-example-apps.md), [decision 0008](architecture/0008-standalone-example-apps.md), and [workflow test matrix](../tests/README.md). Optional model/feed configuration does not certify a live provider profile.

Build each example as an independently runnable Blazor Web App. Shared demo features may be reused in the documentation website, but each example must remain understandable without the docs host.

Each example has a clearly labelled fixture mode and a configurable live mode. Default setup must not require private business data, copyrighted game assets, an external database, or a paid API account.

### Gaming News and Tips

Deliver a content explorer with game preferences, dated articles, linked sources, version-aware tips, comparisons, bookmarks, and saved workspaces.

Use fixture content for deterministic demonstrations. Add a live source through a developer-configured content service; do not make external search or scraping an implicit core framework dependency.

Acceptance scenario: ask for relevant updates, select an article, compare it with another update, save the comparison, and reopen it. Facts retain their source and date. The follow-up operates on the user's current selection.

### Staff Business

Deliver authenticated example users and roles, jobs waiting on materials, assignment editing, operation history, private sessions, and shared business records.

Use an example-owned EF Core database, initially SQLite, for domain records. Keep its schema separate from framework session storage. Provide a documented route for replacing the business persistence layer. Synthetic staff and job data must be clearly identified.

Acceptance scenario: two staff users load the same job, one saves an assignment, and the second receives a version conflict while retaining their draft. A restricted user cannot invoke a prohibited action through either a generated UI event or a direct request.

### Game Asset Workbench

Deliver upload/import, gallery inspection, dimensions and metadata, zoomable preview, one supported transformation, recoverable versions, and PNG export.

Use original or redistribution-compatible sample assets and record their licenses. Start with a deterministic image operation such as outline adjustment or resizing. Optional generative artwork requires a separately configured capable service; it is not implied by selecting a chat model.

Acceptance scenario: import images, identify inconsistent dimensions, select one, request an adjustment, compare the original and preview, undo or choose another version, and export the displayed working artifact. Original uploads remain recoverable.

Exit gate for the phase: all three applications use the same document, session, binding, event, and action mechanisms. Domain-specific behaviour lives in example services or registered packs. Each example runs from its own README.

## Phase 6 Documentation and demonstration website

Complete the Blazor website as the project's public learning and evaluation surface.

Deliverables:

- Explain the two-component model and the developer journey.
- Provide installation, a minimal quickstart, attribute and explicit tool-registration guides, and sample service code.
- Document every supported output node, binding rule, event, error, action, and extension contract.
- Publish an interactive document explorer and clearly labelled deterministic demos that work without credentials.
- Offer optional live demos only with configured credentials, explicit resource limits, and session isolation. A public server cannot access a visitor's local Ollama or LM Studio merely through localhost.
- Provide local-provider setup, frontier-provider setup, supported model/profile results, and hosting limitations.
- Document persistence, ownership, multi-user integration, concurrency, file capabilities, and migration behaviour.
- Include the three example workflows and links to their source.
- Publish architecture decisions, contribution routes, extension tutorials, roadmap, release notes, and preview limitations.
- Include keyboard and small-screen checks for the site and demonstration experiences.
- Keep the library usable independently of the website and any project-operated service.

Exit gate: a developer outside the maintainer's machine can follow the quickstart, run the fixture demos, configure a supported live model if available, and understand how to add their own service or renderer. Documentation examples are validated against the current package API.

## Phase 7 Developer preview release

Release a useful, accurately scoped package set and invite external contributions.

Deliverables:

- Exercise contract and runtime tests, component interactions, browser workflows, session restoration, conflicting actions, and the three example scenarios.
- Perform a provider matrix against pinned or recorded model versions. Report verified tasks and limitations separately from basic connectivity.
- Verify both valid and invalid output handling, unavailable providers, interrupted streams, malformed events, unauthorized references, and stale revisions.
- Measure first useful output, follow-up latency, context overhead, render/update responsiveness, and large-result handling. Record repeatable workloads and initial budgets rather than inventing unsupported latency promises.
- Test keyboard operation, focus preservation, validation messages, chart alternatives, and responsive layouts.
- Install packed artifacts into a fresh consumer outside the source tree. Exercise the two-component quickstart without project references or private APIs.
- Include package readmes, license metadata, repository links, symbols/source information, versioned release notes, and a compatibility statement.
- Configure a protected release workflow using NuGet trusted publishing where applicable. Trusted publishing exchanges CI identity for short-lived credentials. [Microsoft NuGet trusted publishing guidance](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).
- Keep untrusted pull-request workflows separate from release and deployment credentials.
- Publish an initial prerelease such as 0.1.0-preview.1, tagged to the source and documented with known limitations.
- Deploy the docs site, verify installed package/source links, and create a release discussion describing contribution opportunities.
- Invite contributions through the repository and documentation. Select the announcement audience and channel before sending external messages.

Exit gate: another developer can consume the released packages, run all three sample applications in fixture mode, complete a verified live-model path, and submit a contribution using documented steps.

## Validation strategy

Keep deterministic checks separate from live model evaluations.

| Layer | Evidence to collect |
| --- | --- |
| Contracts | Valid and invalid documents, binding types, event validation, round-trip serialization, extension requirements, and version handling. |
| Runtime | Scoped tool execution, cancellation, bounded retries, malformed proposals, ownership, action results, persistence, and recovery. |
| Blazor | Selection, forms, updates, disposal, multiple sessions, focus, and preservation of edits. |
| Browser workflows | The three end-to-end sample scenarios, responsive layout, reconnect, keyboard operation, and conflict recovery. |
| Provider evaluation | The same representative tasks across declared local and frontier profiles, with model versions and observed failures recorded. |
| Package consumption | Fresh installation and quickstart execution using packaged artifacts. |
| Contributor setup | Clone, configure, build, and run fixture demos without paid credentials. |

Use deterministic provider substitutes in ordinary CI. Run live provider evaluations through maintainer-controlled jobs or explicit local runs. Evaluate observable outcomes and required data/actions; do not require identical natural-language responses or identical layouts from every run.

Choose test frameworks and commands during repository bootstrap, record them in CONTRIBUTING, and use behaviour-focused checks. Exact commands must match the selected SDK and test platform.

## Open-source collaboration and governance

Use one public repository initially so issues, contracts, code, examples, and docs can evolve together.

Keep these contribution lanes available:

- Documentation, translations when a localization design exists, and minimal examples.
- Output-document fixtures and error reproduction cases.
- Toolkit renderers, keyboard support, and accessibility improvements.
- Provider capability reports and adapter fixes.
- Content adapters, storage adapters, and specialist capability packs.
- Focused runtime and state-recovery improvements.

Give each issue a concrete outcome, relevant contract links, and acceptance checks. Route schema changes, new mandatory dependencies, and changes to authority or persistence through a short design proposal before implementation.

Maintain a contribution guide covering local setup, fixture data, coding conventions, dependency/license expectations, tests, changelogs, and review. Apply the same review and validation requirements to AI-assisted contributions.

State how maintainers are added, how disagreements are resolved, how releases are managed, and which versions receive fixes. Review ownership as contributors take responsibility for areas. Avoid implying a guaranteed support response time or release cadence before capacity is known.

Track breaking changes and provide migration notes during preview development. Version the output schema separately from package versions and document their compatibility.

## Decisions to settle during Phase 0

| Decision | Starting recommendation | Required before |
| --- | --- | --- |
| Name and package prefix | Accepted: IntentBlazor. Initial availability checks completed; package publication comes later. | Settled in Phase 0 |
| GitHub and NuGet ownership | GitHub: Kita72/IntentBlazor. NuGet ownership is configured before package publication. | GitHub settled; NuGet before preview |
| License and copyright | Accepted: MIT, copyright Tory Bertram and contributors. Check asset rights separately. | Settled in Phase 0 |
| Platform baseline | .NET 10 stable packages with .NET 11 verification; reassess docs/example targets at kickoff. | Project scaffolding |
| Initial frontier provider | Select one provider and recorded model profile for the first live path. | Phase 2 live evaluation |
| Orchestration dependency | Microsoft Agent Framework behind a replaceable runtime boundary if its selected version satisfies the contract. | Phase 2 implementation |
| Public demo hosting | Ordinary Blazor-capable host; deterministic demos enabled by default. | Docs deployment |
| Public live-demo cost policy | Disabled unless configured with limits and an identified operator. | Enabling public paid-model access |

These decisions do not prevent implementing the provider-independent contracts and authored document fixtures once the platform and package names are recorded.

## Developer preview release checklist

- [ ] Repository identity, ownership, license, contribution policy, and security contact are established.
- [ ] AiInput and AiOutput work through public APIs with explicitly scoped sessions.
- [ ] Attribute and explicit tool registration cover the documented examples.
- [ ] Documents compose normal UI and at least one specialist interactive renderer.
- [ ] Local interactions remain responsive without model calls for every event.
- [ ] Follow-up requests preserve selections and unsaved edits.
- [ ] Persisted sessions restore without duplicate writes or unauthorized data access.
- [ ] Ollama, LM Studio, and at least one frontier profile have individually recorded results.
- [ ] Three standalone sample apps pass their representative scenarios.
- [ ] Fresh package consumption and documented quickstart are verified.
- [ ] Documentation and deterministic demos run without paid-model credentials.
- [ ] Release notes distinguish supported capabilities, experimental features, and deferred work.
- [ ] Public GitHub contribution routes and preview feedback channels are available.

The first implementation milestone is Phase 0 followed by a deterministic renderer that demonstrates all three document shapes. That gives contributors a stable place to work while the live AI path develops.
