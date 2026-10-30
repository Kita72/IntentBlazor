# Phase 5: standalone example apps

All three apps use the same public document, session, binding, event, action, storage, and renderer contracts. Their Home pages contain the default pair:

~~~razor
<AiInput />
<AiOutput />
~~~

Example-owned services supply data and authored fixture documents. Shared [ExampleHost](../examples/IntentBlazor.ExampleHost/ExampleRegistration.cs) source supplies synthetic identity, optional model registration, and supporting workspace/setup UI. It is not required by framework consumers. No domain-specific renderer branches were added to core.

## Run from a clone

Use the pinned .NET SDK and PowerShell described in [CONTRIBUTING](../CONTRIBUTING.md). Run one or more commands from the repository root:

~~~powershell
dotnet run --project examples/GamingNews --no-launch-profile -- --environment Development --urls http://localhost:5181
dotnet run --project examples/StaffBusiness --no-launch-profile -- --environment Development --urls http://localhost:5182
dotnet run --project examples/AssetWorkbench --no-launch-profile -- --environment Development --urls http://localhost:5183
~~~

Each command runs its own host; use separate terminals for simultaneous apps. No key, model server, external database, private business data, or copyrighted game asset is needed. See each app's [news](../examples/GamingNews/README.md), [staff](../examples/StaffBusiness/README.md), or [asset](../examples/AssetWorkbench/README.md) README for its scenario.

These synthetic-account examples deliberately require Development. This is a deployment guard, not a production authentication implementation. News/assets create a random private owner; staff lets you choose Alex or Sam (Dispatcher), or Riley (Reader). Cookie names and data-protection purposes differ per app even when localhost ports share a browser. Sign-in/out use antiforgery-protected POSTs. The four-hour persistent identity cookie allows browser reopen; switching/clearing that identity loses the convenience login to its private workspaces without deleting the retained server files. Production hosts must supply durable accounts, tenant mapping, revocation/revalidation, retention, and their own access policies.

Default files live outside wwwroot under ignored `artifacts/phase5-news`, `artifacts/phase5-staff`, and `artifacts/phase5-assets`. Set trusted `Example:DataDirectory` to use another private directory. Framework checkpoints and app business/library records are separate files. To start a fresh demo, choose a fresh directory rather than deleting a working database. Stop the example hosts before rebuilding their locked assemblies on Windows.

## Gaming News and Tips

The default feed contains fictional games and dated synthetic articles with example.com source URLs. They demonstrate attribution, not real gaming facts. Select a game preference and submit a request to prioritize that game's updates. Select a primary article, choose another in **Compare with article**, and ask a follow-up. The current selections and preference survive the response.

**Bookmark selected article** and **Save comparison** execute registered application actions against your private library. A comparison stores both original article snapshots, dates, versions, sources, and tips. Choose it under **My saved comparisons**, save the workspace, then reload/reconnect. Snapshots remain available if the current feed drops those articles. Primary/secondary revision checks prevent silently saving changed articles. The example caps each private bookmark/comparison library at ten records to stay within presentation budgets.

For a configured live feed, set `News:FeedUrl` on the trusted host to an absolute HTTPS endpoint returning the [sample feed shape](../examples/GamingNews/sample-feed.json). It must return application/json without redirects; URL user-info is rejected. Keep any source authentication in a custom host source rather than a URL. There is a ten-second timeout, 64 KiB actual-stream budget, 25 current articles, and a 2048-byte serialized budget per article. Unknown/duplicate properties, duplicate IDs, invalid values, and non-HTTPS citation URLs fail closed. Game names are identifier tokens such as MyGame. A custom source can implement [INewsSource](../examples/GamingNews/NewsSource.cs); the core framework does not scrape/search the web. An unavailable initial source leaves AiInput usable for a retry, without substituting synthetic facts.

The model and news-source choices are independent. A configured feed can use deterministic documents, and a configured model can compose synthetic news. Source labels distinguish those combinations. Hosts are responsible for rights to externally supplied news content.

## Staff Business

The example-owned [BusinessDbContext](../examples/StaffBusiness/BusinessDbContext.cs) contains synthetic jobs waiting on materials, staff, shared operation history, and idempotency receipts. [IStaffRepository](../examples/StaffBusiness/StaffRepository.cs) is the application seam. Its EF implementation uses IDbContextFactory with short-lived contexts; no DbContext lives for a whole Blazor circuit.

1. Open independent browser sessions/profiles as Alex and Sam. Select the same job before either saves.
2. Edit notes/assignment in both. Ask Sam's follow-up while leaving a draft.
3. Save Alex's assignment. Sam's save reports a record-version conflict and retains the exact local draft.
4. Reconnect Sam's saved workspace or ask for a refresh. Compare **Current saved notes**, **Current loaded record version**, and **Draft expected version** with the retained draft and shared history.
5. After reviewing all fields, choose **Use current record version after review**, then save. This explicitly submits the retained values against the new version; it is not an automatic field merge.
6. Sign in as Riley. Local inspection/drafting works, but Save is denied by server authorization. Direct service calls are denied too.

Multiple tabs in one browser share the sign-in cookie; use separate browser profiles for ordinary simultaneous-user evaluation. Existing circuits carry their scoped authenticated identity. The automated tests use separate identity/service scopes and real shared SQLite files.

`business.sqlite` is separate from `workspaces.sqlite`. The business transaction commits the assignment, operation history, and matching application receipt together. Replaying an identical application request returns its receipt; a changed payload is rejected. The framework still does not provide a distributed transaction with the app database. A conflict or uncertain outcome must not be blindly retried.

For another backend, implement IStaffRepository or replace the factory's SQLite registration with your selected EF provider and application migrations. The example uses EnsureCreated for an isolated synthetic database, not a production migration history. Do not point it at an existing business database. [Microsoft's Blazor/EF guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core?view=aspnetcore-10.0) describes short-lived context factories; [EF concurrency guidance](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) describes concurrency tokens.

## Asset Workbench

Use **Attach content** in AiInput, then ask to inspect the attachments. Or choose **Add original sample assets**, which imports two actual procedural PNGs with different sizes. The version picker reports dimension groups from current authorized artifact metadata, including newly resized versions.

Select a version, edit target width/height, and choose **Resize preview**. This invokes the existing image pack action and creates a new nearest-neighbor PNG. Compare **Show original**/**Show preview** and local zoom. **Use original as working version** changes the working reference without deleting derived versions. Select a derived version again, download the displayed file, and save/reopen the workspace. Finish/clear dimension drafts before choosing another version through the picker.

The [typed version contract](../examples/AssetWorkbench/AssetVersionsContract.cs) and [renderer](../examples/AssetWorkbench/Components/AssetVersions.razor) are app-owned public-extension examples. Original uploads stay immutable. A detached original is retained in storage, but parent comparison requires its reference to remain attached; the picker falls back to the earliest currently attached parent. Host retention/cleanup remains explicit. See the [Phase 4 capabilities](phase-4-content-and-setup.md) for byte/file/pixel limits, supported formats, metadata-only image model context, and download ownership.

The samples are authored by this project's procedural generator and licensed under MIT; see [ASSET-LICENSE](../examples/AssetWorkbench/ASSET-LICENSE.md). No generative image service or vision capability is implied by configuring a chat model.

## Optional model configuration

All apps default to Ai:Provider=Fixture. Configure the host with Ai:Provider (Ollama, LMStudio, OpenAI, or experimental Codex) and Ai:Model to opt into model requests. Ai:Endpoint configures a local compatible endpoint; Ai:CodexExecutable configures the workstation CLI. Host-managed Ai:ApiKey can be supplied through user secrets/another secret provider when required. Each app has its own UserSecretsId. Never put credentials in a feed URL, output document, fixture, or committed appsettings file.

The [Phase 2 connection guide](phase-2-model-connections.md) and [provider matrix](provider-evaluation.md) describe adapter boundaries and the deferred live gate. There is no inference on initialization/restoration. Submitting through AiInput in model mode uses the app's authorized IOutputDataSource with the existing model pipeline. Image context remains metadata/text rather than pixels. Open **AI connection and diagnostics** for scoped setup/inspection; a probe is an explicit model request, separate from ordinary fixture checks.

## Supporting workspace controls

Save workspace persists a private checkpoint, including unfinished fields; it does not acknowledge a business save. Reconnect first saves the checkpoint and then restores it with fresh authorized domain data. **Save a copy** forks the current document/state into another private key. Enter that key to open the saved workspace; restoring over a dirty workspace is rejected. A browser reload starts/reopens main; alternate keys can be reopened through the toolbar. Retention, backups, and durable account access belong to the host.

Run `pwsh -File scripts/Verify.ps1` for the repository acceptance suite. The [test matrix](../tests/README.md) names the workflow and boundary tests. These apps are experimental examples, not a public framework release or certification of live models, WebAssembly/Hybrid, multi-instance storage, or production identity.
