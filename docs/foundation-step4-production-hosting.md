# Production hosting: a compiled host integration

Foundation Step 4 adds a compiled .NET 10 Interactive Server recipe at [examples/ProductionHost](../examples/ProductionHost/README.md). It shows the application code around IntentBlazor while preserving the default `<AiInput />` / `<AiOutput />` page. The minimal [quickstart](site/quickstart.md) remains unchanged. This guide is verified with offline tests and local packages; it does not certify your login provider, model, production server or deployment.

The original Phases 0–6 are implemented and Phase 7 has an unpublished `0.1.0-preview.1` candidate. At this Step 4 completion, two foundation steps remained. [Step 5 compatibility/release operations](foundation-step5-release-operations.md) is now implemented, leaving **Step 6** explicit NuGet publication/public-feed verification and current-candidate public hosting. [The foundation plan](foundation-completion-plan.md) tracks their separate acceptance criteria. GitHub remains a `main`-only source/history backup under [decision 0014](architecture/0014-main-only-repository.md).

## 1 Keep the application boundary explicit

| IntentBlazor supplies | The consuming application supplies |
| --- | --- |
| A scoped session and default input/output pair | A real authentication provider, current session validity and account permissions |
| Validated documents, protected edits and revision checks | Authorized C# reads, business transactions, record ownership and expected versions |
| Workspace checkpoint and artifact interfaces/adapters | Stable private owner/key mapping, storage locations, host workspace names/navigation |
| Registered actions and durable action receipts | Authorization on every write, transactional business idempotency and reconciliation |
| Per-request model/tool/context limits and cancellation | Per-user admission budgets, concurrency, operational monitoring and provider billing controls |
| Optional content, setup and renderer extension packages | File quotas, retention/backups, encryption, ingress limits and supported deployment topology |

These host classes are sample application code, not new mandatory library services. Copy/adapt the recipe into your own host. There is no framework account database, required navigation component or hosted model service.

## 2 Start with a real host and private storage

Run the introduction without accounts or inference:

~~~powershell
dotnet run --project examples/ProductionHost --no-launch-profile -- --environment Development --urls http://localhost:5184
~~~

Open `/` and `/health/ready`. Protected routes return 401 until your host signs in an authorized user. There is no synthetic sign-in endpoint. For real cookie sign-in, configure an HTTPS endpoint; the recipe's cookie always uses Secure, HttpOnly and SameSite Strict. Development HTTP is only useful for inspecting the anonymous introduction. Do not deploy the three synthetic-account example apps as a production identity solution.

For Production configure `Host:DataDirectory` to an absolute private directory outside the application/content tree. The recipe rejects a relative path, a public descendant, a parent containing the application, and a path through existing linked directories. Give only the intended host process and backup operator access. Keep it on persistent local storage, separate from deployment replacement directories.

| Private location | Contents and recovery purpose |
| --- | --- |
| `workspaces.sqlite` | Framework checkpoints, sequence numbers and issued-action receipts |
| `business.sqlite` | App-owned private notes, logical workspace catalog and transactional business receipts |
| `usage.sqlite` | Per-owner UTC-day accepted-request counts, retained across restart |
| `artifacts/` | Immutable owner-scoped attachments and provenance |
| `keys/` | Persisted, encrypted data-protection keys for protected cookies |

The recipe persists data-protection keys with a stable application discriminator. Windows uses current-user DPAPI by default. For another host, use a valid host-managed encryption certificate (`Host:KeyEncryptionCertificateThumbprint`) or the `protectKeys` callback with your approved data-protection provider. Non-Windows Production startup rejects missing explicit key encryption. Never copy certificate passwords, model keys or publish profiles into source, checkpoints, rendered documents or logs. Persisting a key file alone is not a portable key-recovery strategy: DPAPI is tied to its Windows security context; a replacement server needs an appropriate recoverable encryption/key-management design. See [Microsoft's IIS data-protection guidance](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0#data-protection).

## 3 Map trusted authentication to a private owner

Keep the host's ASP.NET Core Identity/OIDC/enterprise sign-in flow. Normalize its validated server principal to one authenticated identity containing exactly one `ClaimTypes.NameIdentifier` and exactly one `tenant_id`. Namespace the tenant by issuer when multiple identity providers can issue the same subject. The recipe rejects missing, ambiguous, unauthenticated, blank, control-containing or oversized identifiers and hashes the stable tenant/subject pair into the storage owner.

`HostIdentity` reads `AuthenticationStateProvider` in the circuit, then consults `IHostAccountDirectory` on each operation. It does not use `IHttpContextAccessor` to infer a long-lived circuit identity. `ConfiguredHostAccounts` is an empty-by-default server permission allowlist, with duplicate entries denied and current configuration reloaded. It is useful for integration; replace it with your own account/permission database for real accounts. The directory must deny revoked/disabled accounts and return the same authenticated tenant/subject, never a browser-selected account. The real authentication integration separately enforces individual session expiration and revalidation.

For example, this is **server-owned permission configuration**, not a way to sign in:

~~~json
{
  "Host": {
    "Accounts": [
      { "Tenant": "trusted-issuer/tenant", "Subject": "authenticated-subject", "CanEdit": true, "CanConfigureAi": false }
    ]
  }
}
~~~

Do not accept owner/tenant/role claims from input text, model tools, hidden fields or arbitrary headers. A workspace ID or artifact URL is an address, not authorization. The default `IWorkspaceIdentity`/`IWorkspaceAccess` do not grant durable anonymous access.

Configure authentication-state revalidation and sign-out/session revocation in the actual login integration. An HTTP cookie change does not by itself update every already-connected circuit. The recipe's current server directory is checked at the service boundary, but its configuration allowlist is not a complete Identity security-stamp/session implementation. Microsoft documents [Blazor server authentication and revalidation](https://learn.microsoft.com/aspnet/core/blazor/security/?view=aspnetcore-10.0#authentication-state-management-at-sign-out). After a sign-in/owner change, use a full navigation/new circuit rather than reusing a prior owner's session.

The endpoint policy uses the current HTTP principal and the same server directory for each private artifact request. The endpoint passes the identical owner mapping to `MapIntentContent`. This aligns circuit storage ownership with HTTP downloads and rejects revoked accounts before delivery. File responses are private, `no-store`, `nosniff` and sandboxed; original bytes remain outside `wwwroot`.

## 4 Register the host integration once

The compiled sample uses these existing public extension contracts:

~~~csharp
builder.Services.AddHostWorkspace(builder.Configuration, paths);
~~~

`AddHostWorkspace` is **application code in this recipe**. It registers the scoped identity/access services, SQLite checkpoint store, content adapter, authorized note service, action handler and request admission wrapper. `HostNotes.ReadAsync` carries `[AiDataTool]`; the `attributeTools: false` alternative uses `AddTool<HostNotes>(nameof(HostNotes.ReadAsync), ...)` against the same method. Both are compiled and exercised offline. Reads have no user/owner argument and load only the authenticated account's data.

Default mode has an authored `HostOfflineProcessor` and **no `IChatClient` registration**. It makes no request at startup, on page load or when reopening a saved workspace. Merely adding `Ai:Provider` settings does not activate a model. For a host that intentionally connects a provider, the optional `configureModel` callback receives the existing `IntentModelBuilder`; use its public Ollama, LM Studio, OpenAI or Codex connection primitives with server-owned configuration/credential resolution. [The connection guide](phase-2-model-connections.md) describes those boundaries. Connection examples are not live verification or authorization to run inference on Tory's machine.

Registration finishes by wrapping either processor with `HostLimitedInputProcessor`. Keep this wrapper outermost if adding other processors or replacing services later. Provider SDK calls remain in `IntentBlazor.AI`; domain services never make model requests.

## 5 Keep the workspace page on two default components

[Workspace.razor](../examples/ProductionHost/Components/Pages/Workspace.razor) authorizes the page, asks `HostWorkspaceController` to open a currently owned logical key, and renders:

~~~razor
<AiInput />
<AiOutput />
~~~

Neither component needs an identity parameter, event bridge or explicit session. Their scoped `AiSession` belongs to the circuit. `HostWorkspaceController` verifies that the selected key appears in the current user's catalog before connecting; it never generates a replacement key on every render. The first request is user initiated.

`/workspaces` is host chrome outside the pair. `HostNotes.ListWorkspacesAsync` supplies names and stable keys; `CreateWorkspaceAsync` demonstrates bounded host-owned naming and key creation. Different owners may use the same key (`notes`) without sharing a checkpoint. A second workspace separates conversation/draft state; the example's note business record remains shared between that same user's workspaces. This distinction is deliberate: a workspace is not a new business database partition.

Use full navigation to leave a private workspace in this recipe. Do not silently discard edits when switching; let autosave finish, surface persistence conflicts, and retain/fork/review a conflicting draft. Concurrent circuits share business records and an admission ledger, while each keeps its own session, selection and draft. Checkpoint sequence conflicts are different from business-record version conflicts.

## 6 Authorize saves at the service boundary

The output document can propose the registered `save_note` action; the normal AiOutput confirmation UI must issue the action request. Presentation content cannot authorize it. `HostSaveNote.AuthorizeAsync` rechecks current edit permission and owner/resource identity. `HostNotes.SaveAsync` repeats that check, validates the note's 512-character bound and expected record version, and performs the write plus business idempotency receipt in one SQLite transaction.

A denied or conflicting action keeps the private draft. A successful result acknowledges the accepted state and returns fresh authorized data. Duplicate business request IDs with identical arguments return the recorded result; changed arguments are rejected. Framework receipts protect the workspace action boundary, while transactional application receipts protect a committed business write if the later framework receipt fails. Never blindly retry an uncertain write after reconnect: inspect its receipt/business state and let the user review before issuing a new request. See the [workspace/action guide](phase-3-workspaces.md).

## 7 Budget requests across users and circuits

HTTP rate limiting alone does not govern each input event on an existing circuit. The host wrapper admits work at the `IInputProcessor` boundary and holds the lease across model calls, read tools, corrections and output preparation. It forwards streaming progress and cancellation, and checks identity again before returning output.

Defaults in `Host:Requests` are 50 accepted requests per owner per UTC day, one in flight per owner and eight in flight across the process. The SQLite admission counter survives a restart. A request that is accepted and later fails/cancels still consumes one admission; a request rejected before admission does not. Leases release on success, exceptions, cancellation and identity changes. There is no queue; show the recoverable rejection and let the user retry. Accepted requests are bounded independently by model timeout/call/context/output limits. AI setup allows only host-approved options, never arbitrary endpoints or credentials. `CanProbe` stays false so a probe cannot bypass the admission boundary.

These are **request-count limits**, not token billing or a provider-dollar guarantee. Add your application's usage accounting, upstream billing caps, ingress/connection/upload limits and operational alerts according to its workload. The coordinator is for one process: daily counts use transactional SQLite writes, but active concurrency leases are not distributed across multiple worker processes. Do not infer a multi-node guarantee.

`/ai-settings` compiles the optional `<AiSetup />` / `<AiInspector />` tools under the host policy. Its state is scoped to that page's circuit. To affect a workspace's next request, embed the tools in that same circuit; do not make setup state a singleton or assume it follows full navigation. Neither optional tool becomes a required third interaction component.

## 8 Reopen history and understand memory limits

`ConnectAsync` on a known owner/key opens a new workspace or restores its supported checkpoint. Restoration reauthorizes current datasets and each artifact reference, replaces cached data with fresh authorized records, and checks all saved values and referenced rows. Removed permissions, unknown datasets, missing files or incompatible values fail closed. The existing local draft remains intact when a restore fails.

The checkpoint retains the original conversation archive (up to 512 turns, with the existing size quotas). `Snapshot.Conversation` selects bounded recent context: six recent turns with an 8,000-character budget by default, plus an extractive index of older inputs. `History` is a shorter UI view. This is not semantic summarization, a vector store, cross-workspace retrieval or automatic folder memory. Host-owned names/navigation do not create semantic recall. Reopening restores the supported archive, draft, selections, sorting and artifact references; it does not restore a live socket or resume/replay a pending model request.

## 9 Retention, backups and upgrades

Choose a retention policy for private workspaces/conversations, application data, uploaded/derived artifacts, usage counts, write receipts and key material. Record who may restore or delete each class. The framework store has no general workspace enumeration/deletion API; use a deliberately authorized host maintenance process. Do not infer retention from a browser tab closing. Purging receipts too early can reintroduce duplicate business writes, and deleting keys can invalidate protected cookies.

For artifacts, `IArtifactStore.CleanupAsync(owner, before, retained)` retains referenced artifacts and their parent provenance. Build the retained-reference set from **all** retained checkpoints/business records for that owner, not merely the currently open tab. No destructive cleanup job runs automatically in this sample. Usage-day rows can be pruned by a host-owned policy after their accounting period; never delete the current UTC day as a way to reset an exhausted allowance.

Use this consistent backup procedure for the single-process recipe:

1. Drain requests and explicit business actions; finish/acknowledge pending checkpoint saves or report their uncertainty. A backup preserves persisted state, not an unacknowledged browser edit.
2. Stop the app process/app pool so no writer remains. Copy the **whole private root** to an access-controlled backup, including all SQLite databases and any associated WAL/SHM files, artifacts and keys. Also retain the selected package versions, app configuration/grant source and the recoverable external key/certificate material according to your policy. Do not copy credentials into the repository.
3. Start the app and verify health. Keep a documented rollback bundle and backup identity/version manifest.
4. Rehearse restoration into an isolated private directory using the pinned application/package versions. Verify owner isolation, a private draft and history, attachment bytes, business and framework receipts, and the still-consumed usage allowance before directing real traffic there.

For a live backup, use a database-supported online backup/snapshot plus coordinated artifact/key/application consistency; a plain copy of an actively written `.sqlite` file alone is insufficient. Microsoft's [SQLite backup API guidance](https://learn.microsoft.com/dotnet/standard/data/sqlite/backup) describes online database copies. The stopped-host procedure above is the one exercised by this step's offline tests.

Current compatibility is explicit:

| Layer | Supported boundary |
| --- | --- |
| Checkpoint envelope | `formatVersion: "1"` |
| Output document | schemas `0.1`, `0.2`, `0.3`; builtin toolkit `0.1`; required extension versions must still be registered |
| Framework SQLite store | metadata `schema = "1"`; unknown schema is rejected |
| Application database | Host-owned schema and migration policy; do not treat framework initialization as an application migration engine |

Versioned [saved checkpoint fixtures](../specs/workspace-checkpoints/README.md) retain a private draft and archived turn for each supported output schema. Unsupported formats/schemas and malformed checkpoints are rejected without rewriting stored bytes or silently discarding the current draft. Before upgrading, back up the whole recovery set, pin candidate pack/package versions, restore representative saved data under current permissions, check dirty edits and private files, and rehearse rollback. Use your app's own migrations for its business schema. Do not overwrite a future-version checkpoint with an empty workspace to make startup succeed.

## 10 Deploy only the integration you have verified

The current supported foundation is one .NET 10 Interactive Server process with local persistent storage. Supply the correct .NET hosting runtime, TLS, exact `AllowedHosts`, WebSockets/circuit support, process identity/permissions, persisted/encrypted keys and a real authentication integration. Configure `Host:KnownProxy` only for a verified immediate proxy; the recipe trusts only that exact address with one forwarding hop and does not enable forwarded-host rewriting. IIS integration and another reverse proxy have different forwarding responsibilities. Follow [Microsoft's proxy guidance](https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer/?view=aspnetcore-10.0).

Set finite circuit retention/connection limits and plan for recycle/disconnects. A surviving circuit can reconnect; a lost circuit requires durable restoration. Session affinity, shared storage, key distribution, active quota coordination and multi-node business transactions need a separate design before scale-out. WASM/MAUI Hybrid and .NET 11 compatibility remain separately unverified. Provider `localhost` refers to the server, not a visitor's laptop.

The existing [MonsterASP docs deployment](phase-7-hosting.md) remains a public fixture learning site. This step neither deploys the new host recipe nor updates that site. Publishing/deployment require a separate explicit task. Keep production health output minimal; log status/correlation/counters, not credentials, note bodies, private attachments, prompts, model output or identity cookies. Real external authentication, host TLS/WebSockets, platform key recovery and provider/model behavior remain deployment-specific acceptance work.

## Offline evidence and commands

~~~powershell
pwsh -File scripts/Verify.ps1
~~~

The full local check builds and formats the solution, runs existing and new behavior tests, checks links, packs/audits eight libraries, and executes independent package consumers plus offline browser regressions. It does not start Ollama/LM Studio/model workers or run paid inference. Dependency/browser installation may download tools/packages; the application test scenarios use only local fixtures, TestServer and loopback traffic.

After packing, the focused independent consumer is:

~~~powershell
pwsh -File scripts/Verify-ProductionHost.ps1
~~~

It copies the sample outside the repository, converts every framework project reference to a local package reference, builds every page and executes real public APIs for default input/output, authorized C# data/action, protected draft/history restore, and bounded AI setup. `artifacts/step4/production-host-consumer.json` records the result and consumer directory. The existing independent quickstart verifies the public renderer-extension tutorial. `FoundationHostTests` and `FoundationHostHttpTests` cover current claims/grants, private owners/circuits/artifacts, permission/version/idempotency failures, admission limits/releases/restarts, supported saved formats, failed restoration and the stopped-host backup. Browser/model/hosting compatibility beyond the recorded test environments is not implied.
