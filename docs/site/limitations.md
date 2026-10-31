# Preview boundaries and remaining gates

**The `0.1.0-preview.1` candidate is prepared; no public package preview has been released.** Output schemas remain experimental. Phase 7 adds verification and release tooling; live-model acceptance and NuGet owner/policy/publication gates remain open. See the [readiness record](../phase-7-readiness.md).

## Implemented with deterministic evidence

- Two primary components, strict output documents, all [listed nodes](nodes.md), typed bindings/events, local selection/sorting/editing, revision checks, and dirty-field preservation.
- Registered server actions, private durable workspaces, restored-reference authorization, bounded conversation history/summaries, artifact metadata, and conflicting-save recovery.
- Bounded file adapters, private artifact storage/delivery, real PNG resizing, typed renderer registration, AI setup policy, and inspection of safe progress/usage metadata.
- Ollama, LM Studio, OpenAI, and experimental Codex adapters tested with scripted HTTP/process/model clients. Those tests do not prove a specific live model performs the task well.
- Independent news, staff, and asset apps using synthetic Development-only identities; a smaller credential-free quickstart; searchable static docs and an isolated interactive document explorer.

See [release notes](../../CHANGELOG.md), [test evidence](../../tests/README.md), and [Phase 6 completion](../phase-6-completion.md) for observed checks.

## Not certified or released

No local or frontier model/version has passed the live provider matrix yet. Connectivity and presentation probes are not tool-use or workflow certification. Live feed refresh is optional and separately configured; the bundled Gaming News data is synthetic. The Codex adapter does not call or share memory with this desktop chat.

Public NuGet publication still requires a confirmed owner/policy and current successful local/frontier evidence. A separate OIDC release workflow, IIS deployment helper and server renderer regression budgets are implemented. Observed failed model runs are not latency/cost guarantees. Ordinary contributor builds remain free of model accounts and credentials. Preview upgrades may break APIs or schemas; review release notes and keep durable data backups before upgrading.

The initial verified hosting shape is Interactive Server. WebAssembly, MAUI/Hybrid, distributed persistence, real-time collaborative document editing, and multi-node recovery are not certified. The framework does not compile generated code or give a model unrestricted access to tools, URLs, files, business services, or renderer types.

## Content limits

Supported imports are UTF-8 text/Markdown, bounded JSON/CSV, PNG/JPEG, and text-based PDF. Image context exposes dimensions/metadata rather than automatically attaching vision payloads. No OCR, scanned-PDF understanding, arbitrary office document editing, audio/video processing, or generative asset production is implied. The Asset Workbench transforms original synthetic images; selecting a chat model does not create an image generation service.

## Use the evidence at its actual scope

Fixture browser checks prove deterministic UI behavior. Scripted tests prove adapter contracts and failure handling. Package-only consumers prove current compiled API consumption and deterministic fixture execution. None alone proves a production model, deployment, authentication integration, security audit, complete assistive-technology compatibility, or a performance guarantee.

The [provider matrix](../provider-evaluation.md) remains the record for live results. The [roadmap](../../ROADMAP.md) and [implementation plan](../implementation-plan.md) describe the remaining acceptance work without committing release dates.
