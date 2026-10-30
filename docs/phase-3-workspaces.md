# Phase 3: durable workspaces and application actions

Phase 3 implements resumable private workspaces and explicitly registered application saves. Consumers still place **AiInput** and **AiOutput** on their page. Identity, data authorization, storage, and business rules belong to host services. The public API remains experimental; no NuGet package has been published.

## Run the recovery demonstration

~~~powershell
dotnet run --project apps/IntentBlazor.Docs --no-launch-profile -- --environment Development --urls http://localhost:5180
~~~

Open http://localhost:5180/workspaces and choose **Start my private demo workspace**. This creates a protected cookie with a synthetic stable identity for this browser. It is a Development-only demonstration, not production authentication. Two tabs in the same browser share that identity and private workspace; a separate browser profile gets another identity. All demonstration business data is synthetic.

1. Select Cinderworks. Type new follow-up notes, choose Morgan, and move focus out of the edited field to commit the change. Sort the table if desired.
2. Type a request into AiInput, move focus out of the request field, and submit. The deterministic processor updates the presentation while retaining edited fields. No model is called.
3. Choose **Checkpoint and reconnect**. This saves the checkpoint, constructs a fresh `AiSession`, and restores from SQLite. Check the notes, owner, selection, sort order, and conversation inspector. Reload the browser to exercise a new Blazor circuit with the same cookie.
4. Choose **Simulate another staff save**, then **Save follow-up**. Another synthetic staff member really updates the shared SQLite record and its version. Your save returns `Conflict`; your exact draft stays available. Reconnect or reload again to recover it.
5. **Load latest records — discard local draft** is the explicit discard path. Select the record again to inspect current shared values. **Simulate restricted staff** makes the server reject saves without changing the business record.

The host stores private checkpoints in `artifacts/phase3-demo/private-workspaces.sqlite` and shared business records in `artifacts/phase3-demo/shared-jobs.sqlite`. Those paths are ignored by Git. Restarting the server preserves both files. Protecting a production workspace also requires the host's real account lifecycle, stable authentication/data-protection configuration, file permissions, backups, retention, and appropriate secret storage.

The complete compiling host integration is [WorkspaceDemo.cs](../apps/IntentBlazor.Docs/WorkspaceDemo.cs), the page is [Workspaces.razor](../apps/IntentBlazor.Docs/Components/Pages/Workspaces.razor), and the synthetic domain implementation is [StaffWorkspaces.cs](../examples/IntentBlazor.Examples/StaffWorkspaces.cs). Example-specific record rules live there, outside the framework.

## Keep the two-component page

The default DI-scoped `AiSession` is still used automatically:

~~~razor
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@inject AiSession Workspace

<AiInput />
<AiOutput />

@code {
    protected override async Task OnInitializedAsync()
    {
        var result = await Workspace.ConnectAsync("my-workspace");
        // Present result.Errors through your host's setup UI if connection fails.
        // On a new workspace, your input processor can create the first document.
    }
}
~~~

Use `@using IntentBlazor.Blazor` and `@using IntentBlazor.Runtime` in the host imports. For persistence setup prefer an interactive-only page (`prerender: false`) or a host lifecycle that connects once after interactivity. This avoids performing checkpoint setup twice during prerender and circuit creation. As before, multiple pairs can use explicitly created sessions and the existing `Session` parameter.

In an authenticated Interactive Server host, the storage/identity registration is:

~~~csharp
builder.Services.AddSqliteWorkspaces(Path.Combine(privateDataDirectory, "workspaces.sqlite"));
builder.Services.AddBlazorWorkspaceIdentity();
~~~

These extensions are in `IntentBlazor.Storage.Sqlite` and `IntentBlazor.Blazor`. Keep the file outside publicly served content. Configure the host's authentication/authorization system normally. The Blazor adapter requires an authenticated `ClaimTypes.NameIdentifier`; a custom `IWorkspaceIdentity` can include tenant identity or integrate another trusted account source. Never derive the owner from model output, a URL parameter, or client-submitted owner text.

Register your scoped `IWorkspaceAccess` implementation. It receives the trusted owner, required dataset IDs, and artifact references, and returns `WorkspaceAccess` with current authorized datasets. Recheck record-level permissions and artifact versions; persisted rows and identifiers are not proof of access. The default access service denies restoration and saves. The default anonymous identity does not enable persistence. Hosts wanting only transient fixture behavior need no additional registrations.

## Saving and recovering a checkpoint

`ConnectAsync(key)` resumes the current owner's existing checkpoint or attaches a new key. `RestoreAsync(key)` requires an existing checkpoint. Neither changes business records. After a successful connection, autosave coalesces completed local events and follow-up outcomes. `FlushAsync()` awaits queued autosaves, and `SaveAsync()` performs an explicit checkpoint save. Inspect `PersistenceError` and the returned `OperationResult`; a crash can lose work that has not completed its checkpoint. Components do not secretly select a durable key or owner.

Checkpoints retain exact values and original baselines, dirty markers, selections, table sorting, artifact metadata references, and conversation originals. No image/file bytes or secret provider configuration are stored by this feature. Clearing a restored draft restores its original edit baseline, even when a shared business record has changed since selection. Checkpoint saving never marks an application edit as successfully saved.

Checkpoint version comparisons prevent one circuit overwriting another. On `workspace-conflict`, the local draft remains available and autosave stops. Explicitly `RestoreAsync(key, discardLocal: true)` to discard it, or `ForkAsync(newKey)` to retain it in another private workspace. The host supplies any folder/chat listing interface; this phase provides workspace keys and recovery contracts, not a folder-management UI.

Changes made while a restore is awaiting I/O are protected by session revision checks. A changed signed-in identity clears the old private in-memory state and requires a fresh session. The Blazor components recheck ownership when their authentication cascade updates; host authorization must also validate current account permissions on every read/write.

## Register a real application action

Use `AddIntentAction<THandler>(ActionDefinition)` to register a unique action, a trusted UI label, its resource dataset, and typed scalar argument definitions. Implement `IWorkspaceActionHandler.AuthorizeAsync` and `ExecuteAsync` in the host. `ActionContext` contains the server-derived owner/key, runtime-issued idempotency key, resource ID, expected record version, and validated arguments.

The [schema 0.2 reference](../specs/output-document/v0.2-actions.md) shows the presentation binding. The model can propose a registered button, but cannot execute it through an AI data tool. The renderer obtains a server-issued `ActionRequest`; the runtime checks its source and revision, current record access, and handler permission before entering application execution. The displayed button uses the registered label, not a model's claim about the operation.

| Result | Runtime behavior |
| --- | --- |
| Succeeded | Persist the receipt, install validated returned data/values, and acknowledge only `AcceptedStates`. |
| Conflict | Show the application's concurrency result and retain the local draft. |
| Rejected / Failed | Show a bounded result and retain the local draft. |
| Indeterminate | Submission might have completed. Retain the draft and pending receipt; never automatically replay. |

The handler must enforce domain validation, authorization, expected versions, and transactional idempotency itself. The synthetic `SharedJobStore` conditionally updates the version and commits its own result receipt in the same transaction. The framework checkpoint database is separate; there is no cross-database transaction or universal exactly-once guarantee. `LastActionRequestId` and `GetActionReceiptAsync(id)` support checking an operation by its issued ID. For a pending/uncertain request, reconcile the host's business receipt before deliberately submitting another operation. Returning a success message without an actual committed operation violates the contract.

## Conversation and compatibility

`Archive` retains original user inputs, outcomes, and accepted output JSON. `Snapshot.Conversation` contains up to six recent turns within an 8,000-character input/output-excerpt budget plus an extractive index of up to twelve older requests. The inspector labels this index accurately; it is not a semantic AI summary. `History` remains the latest twenty display outcomes. Original tool transcripts are not archived automatically.

Limits: checkpoints are bounded to 4 MiB UTF-8 and 48 JSON levels, artifacts to 64 versioned references, and archives to 512 turns. New input is rejected once the archive reaches its count or pre-request 2 MiB size quota; existing originals are retained. The model context has its own Phase 2 budget and never receives the entire saved archive automatically. Hosts own store retention and deletion policies.

Output schema 0.1 remains valid for local interactions. Registered actions require schema 0.2; both use builtin toolkit 0.1. Saved format `1` supports those schemas. Unknown formats, schemas, duplicate JSON properties, inconsistent state, and revoked references fail without installing a partial workspace. SQLite's internal metadata schema is also `1`; an unknown schema fails initialization. No automatic migration or distributed store support is claimed.

Phase 2 live-model verification remains deferred. Phase 3's recovery acceptance is deterministic and independent of any model provider. Four phases remain before the developer preview: content/extensions/setup, complete example apps, complete docs/demos, and preview packaging/validation.
