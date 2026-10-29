using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

public sealed class SessionTests
{
    [Theory]
    [InlineData("news", "headlines", "news-2", "selectedArticle")]
    [InlineData("staff", "jobs", "job-102", "selectedJob")]
    [InlineData("assets", "gallery", "asset-2", "selectedAsset")]
    public void Selection_UsesTheSameEventContractAcrossDomains(string fixture, string node, string key, string state)
    {
        using AiSession session = TestContent.Session();
        Assert.True(session.Open(TestContent.Catalog.Get(fixture)).Success);
        long originalRevision = session.Revision;
        TestContent.Select(session, node, key);
        Assert.Equal(key, TestContent.Value(session, state));
        Assert.Equal(originalRevision + 1, session.Revision);
        Assert.False(session.HasDraft);
    }

    [Fact]
    public void StaffDraft_IsPreservedAcrossRejectedSelectionAndClearsToTheSelectedRecord()
    {
        using AiSession session = TestContent.Session();
        session.Open(TestContent.Catalog.Get("staff"));
        TestContent.Select(session, "jobs", "job-101");
        Assert.True(session.Change("notesInput", "Call tomorrow.", session.Revision).Success);
        Assert.True(session.Change("assigneeInput", "staff-2", session.Revision).Success);

        OperationResult selection = session.Dispatch(new("jobs", UiEventKind.Select, session.Revision) { Value = JsonValues.From("job-102") });
        Assert.False(selection.Success);
        Assert.Equal("dirty-state", Assert.Single(selection.Errors).Code);
        Assert.Equal("job-101", TestContent.Value(session, "selectedJob"));
        Assert.Equal("Call tomorrow.", TestContent.Value(session, "notes"));
        Assert.Equal("staff-2", TestContent.Value(session, "assignee"));

        Assert.True(session.Dispatch(new("clearDraft", UiEventKind.Activate, session.Revision)).Success);
        Assert.Equal("Confirm delivery window.", TestContent.Value(session, "notes"));
        Assert.Equal("staff-1", TestContent.Value(session, "assignee"));
        Assert.False(session.HasDraft);
    }

    [Fact]
    public void AssetSelection_BindsImageMetadataAndWorkingLabel()
    {
        using AiSession session = TestContent.Session();
        session.Open(TestContent.Catalog.Get("assets"));
        TestContent.Select(session, "gallery", "asset-2");
        Assert.Equal("/samples/lantern.svg", JsonValues.Display(session.Read(ValueBinding.Selected("selectedAsset", "assets", "image"))));
        Assert.Equal("16 × 16 pixels", JsonValues.Display(session.Read(ValueBinding.Selected("selectedAsset", "assets", "dimensions"))));
        Assert.True(session.Change("labelInput", "Night lantern", session.Revision).Success);
        Assert.Equal("Night lantern", TestContent.Value(session, "workingLabel"));
        Assert.Equal("Lantern sprite", TestContent.Catalog.Get("assets").Data[0].Rows[1]["name"].GetString());
    }

    [Theory]
    [InlineData("stale", "stale-revision")]
    [InlineData("missing-node", "event-source")]
    [InlineData("foreign-record", "event-record")]
    [InlineData("wrong-event", "event-kind")]
    public void Dispatch_RejectsForgedEventsWithoutChangingState(string caseId, string code)
    {
        using AiSession session = TestContent.Session();
        session.Open(TestContent.Catalog.Get("news"));
        SessionEvent message = caseId switch
        {
            "stale" => new("headlines", UiEventKind.Select, 0) { Value = JsonValues.From("news-1") },
            "missing-node" => new("missing", UiEventKind.Select, session.Revision) { Value = JsonValues.From("news-1") },
            "foreign-record" => new("headlines", UiEventKind.Select, session.Revision) { Value = JsonValues.From("job-101") },
            _ => new("headlines", UiEventKind.Activate, session.Revision)
        };
        long revision = session.Revision;
        OperationResult result = session.Dispatch(message);
        Assert.Equal(code, Assert.Single(result.Errors).Code);
        Assert.Equal(revision, session.Revision);
        Assert.Equal(JsonValueKind.Null, session.Snapshot.Values["selectedArticle"].ValueKind);
    }

    [Fact]
    public void Dispatch_RejectsInputsInsideHiddenAncestors()
    {
        using AiSession session = TestContent.Session();
        session.Open(TestContent.Catalog.Get("staff"));
        OperationResult result = session.Change("notesInput", "Forged draft", session.Revision);
        Assert.Equal("event-source", Assert.Single(result.Errors).Code);
        Assert.Equal("", TestContent.Value(session, "notes"));
    }

    [Fact]
    public void Sort_UsesTypedValuesAndRejectsUndisplayedFields()
    {
        using AiSession session = TestContent.Session();
        OutputContent content = TestContent.Catalog.Get("staff");
        content = TestContent.WithRows(content, content.Data[0].Rows.SetItem(1,
            content.Data[0].Rows[1].SetItem("priority", JsonValues.From(10))));
        session.Open(content);
        OutputNode node = session.Document!.Nodes.Single(item => item.Id == "jobs");
        Assert.True(session.Dispatch(new("jobs", UiEventKind.Sort, session.Revision) { SortField = "priority" }).Success);
        Assert.Equal("job-101", session.RecordKey(node, session.Rows(node)[0]));
        Assert.True(session.Dispatch(new("jobs", UiEventKind.Sort, session.Revision) { SortField = "priority", Descending = true }).Success);
        Assert.Equal("job-102", session.RecordKey(node, session.Rows(node)[0]));
        OperationResult result = session.Dispatch(new("jobs", UiEventKind.Sort, session.Revision) { SortField = "notes" });
        Assert.Equal("sort-field", Assert.Single(result.Errors).Code);
        Assert.True(session.Snapshot.Sorting["jobs"].Descending);
    }

    [Fact]
    public void InvalidProposal_RetainsDocumentSelectionAndRevision()
    {
        using AiSession session = TestContent.Session();
        session.Open(TestContent.Catalog.Get("news"));
        TestContent.Select(session, "headlines", "news-2");
        OutputDocument document = session.Document!;
        long revision = session.Revision;
        OperationResult result = session.Open(TestContent.Catalog.Get("assets") with
        { Document = TestContent.Catalog.Get("assets").Document with { SchemaVersion = "99" } });
        Assert.False(result.Success);
        Assert.Same(document, session.Document);
        Assert.Equal(revision, session.Revision);
        Assert.Equal("news-2", TestContent.Value(session, "selectedArticle"));
    }

    [Fact]
    public void InvalidJson_RetainsTheLastWorkingDocument()
    {
        using AiSession session = TestContent.Session();
        session.Open(TestContent.Catalog.Get("news"));
        Assert.False(session.OpenJson("{", []).Success);
        Assert.Equal("news-reader", session.Document!.Id);
        Assert.Equal("invalid-json", Assert.Single(session.Errors).Code);
    }

    [Fact]
    public void Replacement_PreservesDraftUnlessExplicitlyDiscarded()
    {
        using AiSession session = TestContent.Session();
        session.Open(TestContent.Catalog.Get("assets"));
        TestContent.Select(session, "gallery", "asset-1");
        session.Change("labelInput", "My crate", session.Revision);
        Assert.False(session.Open(TestContent.Catalog.Get("news")).Success);
        Assert.Equal("My crate", TestContent.Value(session, "workingLabel"));
        long revision = session.Revision;
        Assert.True(session.Open(TestContent.Catalog.Get("news"), discardDraft: true).Success);
        Assert.True(session.Revision > revision);
        Assert.False(session.HasDraft);
        Assert.False(session.Dispatch(new("headlines", UiEventKind.Select, revision) { Value = JsonValues.From("news-1") }).Success);
    }

    [Fact]
    public void Effects_RejectAtomicallyWhenAReferencedRecordIsInvalid()
    {
        OutputContent content = TestContent.Catalog.Get("staff");
        var changedRow = content.Data[0].Rows[0].SetItem("assignee", JsonValues.From("unknown-staff"));
        content = TestContent.WithRows(content, content.Data[0].Rows.SetItem(0, changedRow));
        using AiSession session = TestContent.Session();
        Assert.True(session.Open(content).Success);
        OperationResult result = session.Dispatch(new("jobs", UiEventKind.Select, session.Revision) { Value = JsonValues.From("job-101") });
        Assert.Equal("effect-value", Assert.Single(result.Errors).Code);
        Assert.Equal(JsonValueKind.Null, session.Snapshot.Values["selectedJob"].ValueKind);
        Assert.Equal("", TestContent.Value(session, "notes"));
    }

    [Theory]
    [InlineData("-1", false)]
    [InlineData("0", true)]
    [InlineData("5", true)]
    [InlineData("6", false)]
    [InlineData("not-a-number", false)]
    public void NumericInput_EnforcesDeclaredBounds(string input, bool accepted)
    {
        OutputContent content = new()
        {
            Document = new()
            {
                Id = "numeric-input",
                RootId = "quantity",
                State = ImmutableDictionary<string, StateDefinition>.Empty.Add("quantity",
                    new() { Kind = StateKind.Number, InitialValue = JsonValues.From(2), Minimum = 0, Maximum = 5 }),
                Nodes = [new() { Id = "quantity", Kind = NodeKind.Input, Title = "Quantity", StateKey = "quantity" }]
            }
        };
        using AiSession session = TestContent.Session();
        Assert.True(session.Open(content).Success);
        Assert.Equal(accepted, session.Change("quantity", input, session.Revision).Success);
        Assert.Equal(accepted ? input : "2", TestContent.Value(session, "quantity"));
    }

    [Fact]
    public async Task Input_UsesConfiguredProcessorAndRecordsTheOutcome()
    {
        using AiSession session = TestContent.Session();
        Assert.True((await session.SubmitAsync(" assets ")).Success);
        Assert.Equal("asset-gallery", session.Document!.Id);
        Assert.Equal(new InputTurn("assets", "completed"), Assert.Single(session.History));
        Assert.False((await session.SubmitAsync("invent an application")).Success);
        Assert.Equal("asset-gallery", session.Document.Id);
        Assert.Equal("rejected", session.History[^1].Status);
    }

    [Fact]
    public async Task Input_CancellationAndBusyGuardPreserveExistingOutput()
    {
        TaskCompletionSource<OutputContent> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using AiSession session = TestContent.Session(new DelegateInputProcessor((_, _, token) =>
        {
            calls++;
            return new(pending.Task.WaitAsync(token));
        }));
        session.Open(TestContent.Catalog.Get("news"));
        Task<OperationResult> first = session.SubmitAsync("staff");
        Assert.True(session.IsBusy);
        Assert.Equal("busy", Assert.Single((await session.SubmitAsync("assets")).Errors).Code);
        Assert.Equal(1, calls);
        session.Cancel();
        Assert.Equal("cancelled", Assert.Single((await first).Errors).Code);
        Assert.False(session.IsBusy);
        Assert.Equal("news-reader", session.Document!.Id);
    }

    [Fact]
    public async Task Input_RejectsAResponseAfterTheWorkspaceChanges()
    {
        TaskCompletionSource<OutputContent> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using AiSession session = TestContent.Session(new DelegateInputProcessor((_, _, _) => new(pending.Task)));
        session.Open(TestContent.Catalog.Get("staff"));
        Task<OperationResult> request = session.SubmitAsync("news");
        TestContent.Select(session, "jobs", "job-101");
        pending.SetResult(TestContent.Catalog.Get("news"));
        Assert.Equal("stale-revision", Assert.Single((await request).Errors).Code);
        Assert.Equal("staff-followups", session.Document!.Id);
        Assert.Equal("job-101", TestContent.Value(session, "selectedJob"));
    }

    [Fact]
    public async Task Input_FailureDoesNotExposeHandlerExceptionOrLoseOutput()
    {
        using AiSession session = TestContent.Session(new DelegateInputProcessor((_, _, _) =>
            ValueTask.FromException<OutputContent>(new InvalidOperationException("private backend detail"))));
        session.Open(TestContent.Catalog.Get("news"));
        OperationResult result = await session.SubmitAsync("request");
        Assert.Equal("input-failed", Assert.Single(result.Errors).Code);
        Assert.DoesNotContain("private backend detail", result.Errors[0].Message);
        Assert.Equal("news-reader", session.Document!.Id);
        Assert.False(session.IsBusy);
    }

    [Fact]
    public async Task DefaultRegistration_IsScopedAndRequiresNoModel()
    {
        ServiceCollection services = new();
        services.AddIntentBlazor();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope first = provider.CreateScope();
        using IServiceScope second = provider.CreateScope();
        AiSession one = first.ServiceProvider.GetRequiredService<AiSession>();
        AiSession two = second.ServiceProvider.GetRequiredService<AiSession>();
        Assert.Same(one, first.ServiceProvider.GetRequiredService<AiSession>());
        Assert.NotSame(one, two);
        one.Open(TestContent.Catalog.Get("news"));
        two.Open(TestContent.Catalog.Get("news"));
        TestContent.Select(one, "headlines", "news-1");
        Assert.Equal(JsonValueKind.Null, two.Snapshot.Values["selectedArticle"].ValueKind);
        Assert.Equal("input-rejected", Assert.Single((await one.SubmitAsync("hello")).Errors).Code);
    }
}
