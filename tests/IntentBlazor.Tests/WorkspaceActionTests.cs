using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntentBlazor.Tests;

public sealed class WorkspaceActionTests
{
    private static async Task<ActionRequest> Prepare(WorkspaceHarness h, WorkspaceScope scope, string note)
    {
        Assert.True((await scope.Session.ConnectAsync("work")).Success);
        Assert.True(scope.Session.Open(h.Staff.Create()).Success);
        TestContent.Select(scope.Session, "jobs", "job-101");
        Assert.True(scope.Session.Change("notesInput", note, scope.Session.Revision).Success);
        return Assert.IsType<ActionRequest>(scope.Session.PrepareAction("saveFollowup", scope.Session.Revision));
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("sqlite")]
    public async Task ApplicationSave_AcknowledgesActualResultAndPreventsReplay(string store)
    {
        using WorkspaceHarness h = new(store); using WorkspaceScope scope = h.Scope();
        ActionRequest request = await Prepare(h, scope, "Saved by Alice");
        Assert.Equal(ActionStatus.Succeeded, (await scope.Session.ExecuteActionAsync(request)).Status);
        Assert.Equal("Saved by Alice", h.Jobs.Read()[0].Rows[0]["notes"].GetString());
        Assert.Equal(2, h.Jobs.Read()[0].Rows[0]["version"].GetInt64());
        Assert.Equal("2", TestContent.Value(scope.Session, "recordVersion"));
        Assert.False(scope.Session.HasDraft);
        Assert.NotEqual(ActionStatus.Succeeded, (await scope.Session.ExecuteActionAsync(request)).Status);
        Assert.Equal(2, h.Jobs.Read()[0].Rows[0]["version"].GetInt64());
        ActionReceipt receipt = (await scope.Session.GetActionReceiptAsync(request.RequestId))!;
        Assert.Equal(ActionStatus.Succeeded, receipt.Result!.Status);
        await scope.Session.SaveAsync(); using WorkspaceScope restored = h.Scope();
        Assert.True((await restored.Session.RestoreAsync("work")).Success);
        Assert.Equal(ActionStatus.Succeeded, (await restored.Session.GetActionReceiptAsync(request.RequestId))!.Result!.Status);
        Assert.False(restored.Session.HasDraft);
    }

    [Fact]
    public async Task SharedRecordConflict_RetainsOtherStaffDraftAndPrivateWorkspaces()
    {
        using WorkspaceHarness h = new("sqlite"); using WorkspaceScope alice = h.Scope(); using WorkspaceScope bob = h.Scope("demo-bob");
        ActionRequest first = await Prepare(h, alice, "Alice saved"), second = await Prepare(h, bob, "Bob unfinished");
        Assert.Equal(ActionStatus.Succeeded, (await alice.Session.ExecuteActionAsync(first)).Status);
        Assert.Equal(ActionStatus.Conflict, (await bob.Session.ExecuteActionAsync(second)).Status);
        Assert.Equal("Bob unfinished", TestContent.Value(bob.Session, "notes")); Assert.True(bob.Session.HasDraft);
        Assert.Equal("1", TestContent.Value(bob.Session, "recordVersion"));
        Assert.Equal("Alice saved", h.Jobs.Read()[0].Rows[0]["notes"].GetString());
        await alice.Session.SaveAsync(); await bob.Session.SaveAsync();
        Assert.Contains("Bob unfinished", (await h.Store.LoadAsync("demo-bob", "work"))!.Json);
        Assert.DoesNotContain("Bob unfinished", (await h.Store.LoadAsync("demo-alice", "work"))!.Json);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("revoked-record")]
    [InlineData("forged")]
    [InlineData("stale")]
    [InlineData("cancelled")]
    public async Task DeniedInvalidAndCancelledRequests_DoNotMutateTheApplication(string kind)
    {
        using WorkspaceHarness h = new(); using WorkspaceScope scope = h.Scope(); ActionRequest request = await Prepare(h, scope, "Retain draft");
        using CancellationTokenSource cancellation = new();
        switch (kind)
        {
            case "permission": scope.Permissions.CanSave = false; break;
            case "revoked-record": h.Access.RemoveSelectedRow = true; break;
            case "forged": request = request with { NodeId = "clearDraft" }; break;
            case "stale": scope.Session.Change("notesInput", "Newer draft", scope.Session.Revision); break;
            default: cancellation.Cancel(); break;
        }
        Assert.NotEqual(ActionStatus.Succeeded, (await scope.Session.ExecuteActionAsync(request, cancellation.Token)).Status);
        Assert.Equal(1, h.Jobs.Read()[0].Rows[0]["version"].GetInt64());
        Assert.Equal("Confirm delivery window.", h.Jobs.Read()[0].Rows[0]["notes"].GetString());
        Assert.True(scope.Session.HasDraft); Assert.Null(await scope.Session.GetActionReceiptAsync(request.RequestId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UncertainSubmittedAction_NeverReexecutesOrClearsTheDraft(bool failReceipt)
    {
        using WorkspaceHarness h = new("sqlite", throwAfterSave: !failReceipt, failReceipt: failReceipt);
        using WorkspaceScope scope = h.Scope(); ActionRequest request = await Prepare(h, scope, "Possibly saved");
        ActionResult result = await scope.Session.ExecuteActionAsync(request);
        Assert.Equal(ActionStatus.Indeterminate, result.Status); Assert.DoesNotContain("private", result.Message);
        Assert.True(scope.Session.HasDraft); Assert.Equal("Possibly saved", TestContent.Value(scope.Session, "notes"));
        Assert.Equal(2, h.Jobs.Read()[0].Rows[0]["version"].GetInt64());
        Assert.Equal(ActionStatus.Indeterminate, (await scope.Session.ExecuteActionAsync(request)).Status);
        Assert.Equal(2, h.Jobs.Read()[0].Rows[0]["version"].GetInt64());
        Assert.Null((await scope.Session.GetActionReceiptAsync(request.RequestId))!.Result);
    }

    [Fact]
    public void DomainIdempotency_UsesTheHostKeyAndRejectsChangedPayloads()
    {
        using WorkspaceHarness h = new();
        ActionContext context = new("demo-alice", "work", Guid.NewGuid().ToString("N"), "job-101", "1",
            ImmutableDictionary<string, System.Text.Json.JsonElement>.Empty.Add("notes", JsonValues.From("Once")).Add("assignee", JsonValues.From("staff-1")));
        Assert.Equal(ActionStatus.Succeeded, h.Jobs.Save(context, CancellationToken.None).Status);
        Assert.Equal(ActionStatus.Succeeded, h.Jobs.Save(context, CancellationToken.None).Status);
        Assert.Equal(ActionStatus.Rejected, h.Jobs.Save(context with { Arguments = context.Arguments.SetItem("notes", JsonValues.From("Different")) }, CancellationToken.None).Status);
        Assert.Equal(2, h.Jobs.Read()[0].Rows[0]["version"].GetInt64()); Assert.Equal("Once", h.Jobs.Read()[0].Rows[0]["notes"].GetString());
    }

    [Fact]
    public async Task ReceiptRead_RechecksOwnershipAfterAccessResolution()
    {
        using WorkspaceHarness h = new(); using WorkspaceScope scope = h.Scope();
        ActionRequest request = await Prepare(h, scope, "Private saved note");
        Assert.Equal(ActionStatus.Succeeded, (await scope.Session.ExecuteActionAsync(request)).Status);
        h.Access.BeforeResolve = _ => { scope.Identity.Owner = "demo-bob"; return Task.CompletedTask; };
        Assert.Null(await scope.Session.GetActionReceiptAsync(request.RequestId));
        Assert.Null(scope.Session.Document); Assert.Empty(scope.Session.Snapshot.Values);
        Assert.Equal("Private saved note", h.Jobs.Read()[0].Rows[0]["notes"].GetString());
    }

    [Fact]
    public async Task Renderer_UsesRegisteredActionLabelAndShowsConfirmedOutcome()
    {
        using WorkspaceHarness h = new(); using WorkspaceScope scope = h.Scope(); ActionRequest request = await Prepare(h, scope, "From UI");
        await using HtmlRenderer renderer = new(scope.Services, scope.Services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<IntentBlazor.Blazor.AiOutput>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Session"] = scope.Session }));
            Assert.Contains("Save follow-up", output.ToHtmlString()); Assert.DoesNotContain("Model label is replaced", output.ToHtmlString());
            Assert.Contains("Unsaved local draft", output.ToHtmlString());
            await scope.Session.ExecuteActionAsync(request);
            Assert.Contains("Succeeded", output.ToHtmlString()); Assert.DoesNotContain("Unsaved local draft", output.ToHtmlString());
        });
    }

    [Theory]
    [InlineData("schema", "unsupported-action")]
    [InlineData("unknown", "unsupported-action")]
    [InlineData("arguments", "action-arguments")]
    [InlineData("resource", "action-resource")]
    [InlineData("version", "action-version")]
    public void ActionDocuments_RequireKnownVersionedTypedDefinitions(string kind, string code)
    {
        using WorkspaceHarness h = new();
        var validator = h.Provider.GetRequiredService<DocumentValidator>(); OutputContent content = h.Staff.Create();
        if (kind == "schema") content = content with { Document = content.Document with { SchemaVersion = "0.1" } };
        else content = content with
        {
            Document = content.Document with
            {
                Interactions = content.Document.Interactions.Select(action => action.Action is null ? action : kind switch
                {
                    "unknown" => action with { Action = "unregistered_save" },
                    "arguments" => action with { Arguments = action.Arguments.Remove("notes") },
                    "resource" => action with { Resource = ValueBinding.Constant("job-101") },
                    _ => action with { ExpectedVersion = ValueBinding.Constant(false) }
                }).ToImmutableArray()
            }
        };
        Assert.Equal(code, Assert.Single(validator.Validate(content).Errors).Code);
    }
}
