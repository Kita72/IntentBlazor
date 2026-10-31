using System.Collections.Immutable;
using System.Text.Json;
using IntentBlazor.Core;
using IntentBlazor.Examples;
using IntentBlazor.Runtime;

namespace IntentBlazor.Docs.Documentation;

public sealed class DocumentExplorer : IDisposable
{
    private readonly ExampleCatalog _catalog;
    private string _baseline = "";
    private OutputDocument? _shownDocument;
    public AiSession Session { get; }
    public string JsonDraft { get; set; } = "";
    public bool HasJsonDraft => JsonDraft != _baseline;
    public string Fixture { get; private set; } = "news";
    public OperationResult EditorResult { get; private set; } = OperationResult.Ok;
    public event Action? Changed;
    private static readonly JsonSerializerOptions InspectorOptions = new() { WriteIndented = true };

    public DocumentExplorer(ExampleCatalog catalog)
    {
        _catalog = catalog;
        Session = new(new DocumentValidator(), new GuardedInput(catalog, () => HasJsonDraft));
        Session.Changed += OnSessionChanged;
        OpenFixture("news");
    }

    public OperationResult OpenFixture(string name)
    {
        if (!_catalog.Fixtures.ContainsKey(name)) return Reject("fixture", "Choose news, staff, or assets.");
        if (HasJsonDraft) return Reject("editor-dirty", "Render or restore your JSON draft before switching fixtures.");
        EditorResult = Session.Open(_catalog.Get(name));
        if (EditorResult.Success) { Fixture = name; SynchronizeEditor(); }
        Changed?.Invoke();
        return EditorResult;
    }

    public OperationResult Render()
    {
        EditorResult = Session.OpenJson(JsonDraft, Session.Data);
        if (EditorResult.Success) SynchronizeEditor();
        Changed?.Invoke();
        return EditorResult;
    }

    public void RestoreEditor()
    {
        SynchronizeEditor();
        EditorResult = OperationResult.Ok;
        Changed?.Invoke();
    }

    public OperationResult TryInvalidSchema()
    {
        if (HasJsonDraft) return Reject("editor-dirty", "Render or restore your JSON draft before loading the invalid example.");
        JsonDraft = OutputJson.SerializeDocument(Session.Document! with { SchemaVersion = "99" });
        return Render();
    }

    public string StateJson => JsonSerializer.Serialize(new
    {
        Session.Revision,
        Session.Snapshot.Values,
        Session.Snapshot.Sorting,
        Session.Snapshot.DirtyStates,
        Session.Snapshot.Conversation,
        Session.History
    }, InspectorOptions);
    public string DataJson => JsonSerializer.Serialize(Session.Data, OutputJson.Options);
    public string NodeJson(string? id) => JsonSerializer.Serialize(Session.Document?.Nodes.FirstOrDefault(node => node.Id == id), OutputJson.Options);

    public ImmutableArray<(OutputNode Node, int Depth)> Tree()
    {
        ImmutableArray<(OutputNode, int)>.Builder result = ImmutableArray.CreateBuilder<(OutputNode, int)>();
        if (Session.Document is { } doc) Visit(doc.RootId, 0);
        return result.ToImmutable();
        void Visit(string id, int depth)
        {
            OutputNode node = Session.Document!.Nodes.First(node => node.Id == id);
            result.Add((node, depth));
            foreach (string child in node.Children) Visit(child, depth + 1);
        }
    }

    private OperationResult Reject(string code, string message)
    {
        EditorResult = OperationResult.Fail(code, message);
        Changed?.Invoke();
        return EditorResult;
    }
    private void OnSessionChanged()
    {
        if (!ReferenceEquals(_shownDocument, Session.Document) && !HasJsonDraft)
        {
            SynchronizeEditor();
            EditorResult = OperationResult.Ok;
        }
        Changed?.Invoke();
    }
    private void SynchronizeEditor()
    {
        _shownDocument = Session.Document;
        JsonDraft = _baseline = _shownDocument is null ? "" : OutputJson.SerializeDocument(_shownDocument);
        Fixture = _catalog.Fixtures.FirstOrDefault(pair => pair.Value.Document.Id == _shownDocument?.Id).Key ?? Fixture;
    }
    public void Dispose()
    {
        Session.Changed -= OnSessionChanged;
        Session.Dispose();
    }

    private sealed class GuardedInput(ExampleCatalog catalog, Func<bool> hasDraft) : IInputProcessor
    {
        public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken cancellationToken) =>
            hasDraft() ? ValueTask.FromException<OutputContent>(new InputRejectedException("Render or restore your JSON draft before a fixture command.")) :
            new ExampleInputProcessor(catalog).ProcessAsync(input, workspace, cancellationToken);
    }
}
