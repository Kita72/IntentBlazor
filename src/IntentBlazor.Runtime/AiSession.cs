using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using IntentBlazor.Core;

namespace IntentBlazor.Runtime;

public sealed record SortState(string Field, bool Descending);
public sealed record WorkspaceSnapshot(
    OutputDocument? Document,
    long Revision,
    ImmutableDictionary<string, JsonElement> Values,
    ImmutableDictionary<string, SortState> Sorting,
    ImmutableHashSet<string> DirtyStates);
public sealed record SessionEvent(string NodeId, UiEventKind Kind, long ExpectedRevision)
{
    public JsonElement Value { get; init; } = JsonValues.Null;
    public string? SortField { get; init; }
    public bool Descending { get; init; }
}
public sealed record InputTurn(string Text, string Status);

public interface IInputProcessor
{
    ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken cancellationToken);
}

public sealed class InputRejectedException(string message) : Exception(message);

public sealed class AiSession(DocumentValidator validator, IInputProcessor processor) : IDisposable
{
    private OutputContent? _content;
    private ImmutableDictionary<string, JsonElement> _values = ImmutableDictionary<string, JsonElement>.Empty;
    private ImmutableDictionary<string, JsonElement> _baseline = ImmutableDictionary<string, JsonElement>.Empty;
    private ImmutableDictionary<string, SortState> _sorting = ImmutableDictionary<string, SortState>.Empty;
    private ImmutableHashSet<string> _dirty = ImmutableHashSet<string>.Empty;
    private readonly object _cancellationGate = new();
    private CancellationTokenSource? _cancellation;
    private int _busy;
    private bool _disposed;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public OutputDocument? Document => _content?.Document;
    public long Revision { get; private set; }
    public bool IsBusy => Volatile.Read(ref _busy) != 0;
    public bool HasDraft => !_dirty.IsEmpty;
    public ImmutableArray<DocumentError> Errors { get; private set; } = [];
    public ImmutableArray<InputTurn> History { get; private set; } = [];
    public WorkspaceSnapshot Snapshot => new(Document, Revision, _values, _sorting, _dirty);
    public event Action? Changed;

    public OperationResult Open(OutputContent content, bool discardDraft = false, long? expectedRevision = null)
    {
        if (_disposed) return Fail("disposed", "The session has been disposed.");
        if (expectedRevision is not null && expectedRevision != Revision)
            return Fail("stale-revision", "The workspace changed while this output was being prepared.");
        if (HasDraft && !discardDraft)
            return Fail("dirty-state", "Clear the current draft before replacing the workspace.");

        OutputContent copy;
        try { copy = OutputJson.ReadContent(OutputJson.SerializeContent(content)); }
        catch (Exception error) when (error is JsonException or ObjectDisposedException or ArgumentException or InvalidOperationException)
        { return Fail("invalid-json", "The proposed output is not valid bounded JSON."); }
        OperationResult validation = validator.Validate(copy);
        if (!validation.Success) return Fail(validation);

        _content = copy;
        _values = copy.Document.State.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.InitialValue);
        _baseline = _values;
        _dirty = ImmutableHashSet<string>.Empty;
        _sorting = ImmutableDictionary<string, SortState>.Empty;
        Revision++;
        return Succeed();
    }

    public OperationResult OpenJson(string documentJson, ImmutableArray<OutputDataSet> data)
    {
        try { return Open(new() { Document = OutputJson.ReadDocument(documentJson), Data = data }); }
        catch (JsonException) { return Fail("invalid-json", "The proposed document is not valid bounded JSON."); }
    }

    public JsonElement Read(ValueBinding binding) => Resolve(binding, _values, JsonValues.Null);

    public bool IsVisible(OutputNode node)
    {
        if (node.WhenState is null) return true;
        JsonElement value = _values[node.WhenState];
        return value.ValueKind is not JsonValueKind.Null and not JsonValueKind.False;
    }

    public ImmutableArray<ImmutableDictionary<string, JsonElement>> Rows(OutputNode node)
    {
        OutputDataSet set = DataSet(node.DataSetId!);
        if (!_sorting.TryGetValue(node.Id, out SortState? sort)) return set.Rows;
        IOrderedEnumerable<ImmutableDictionary<string, JsonElement>> ordered = sort.Descending
            ? set.Rows.OrderByDescending(row => row[sort.Field], ScalarComparer.Instance)
            : set.Rows.OrderBy(row => row[sort.Field], ScalarComparer.Instance);
        return ordered.ThenBy(row => row[set.KeyField].GetString(), StringComparer.Ordinal).ToImmutableArray();
    }

    public string RecordKey(OutputNode node, IReadOnlyDictionary<string, JsonElement> row) =>
        row[DataSet(node.DataSetId!).KeyField].GetString()!;

    public OperationResult Change(string nodeId, string rawValue, long expectedRevision)
    {
        OutputNode? node = Document?.Nodes.FirstOrDefault(item => item.Id == nodeId);
        if (node?.StateKey is null) return Fail("input-node", "This node is not an editable input.");
        StateDefinition definition = Document!.State[node.StateKey];
        JsonElement value;
        if (rawValue.Length > 4096) return Fail("input-limit", "Input cannot exceed 4096 characters.");
        if (rawValue.Length == 0 && definition.Nullable) value = JsonValues.Null;
        else if (definition.Kind == StateKind.Number)
        {
            if (!decimal.TryParse(rawValue, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out decimal number))
                return Fail("input-value", "Enter a valid number.");
            value = JsonValues.From(number);
        }
        else value = JsonValues.From(rawValue);
        return Dispatch(new(nodeId, UiEventKind.Change, expectedRevision) { Value = value });
    }

    public OperationResult Dispatch(SessionEvent message)
    {
        if (_disposed || Document is null) return Fail("empty-session", "Open a document before interacting.");
        if (message.ExpectedRevision != Revision) return Fail("stale-revision", "This event belongs to an older workspace revision.");
        OutputNode? node = Document.Nodes.FirstOrDefault(item => item.Id == message.NodeId);
        if (node is null || !IsVisiblePath(node)) return Fail("event-source", "The event source is unavailable.");
        Dictionary<string, OutputDataSet> data = _content!.Data.ToDictionary(set => set.Id, StringComparer.Ordinal);

        if (message.Kind == UiEventKind.Change)
        {
            if (node.Kind is not (NodeKind.Input or NodeKind.Select) || node.StateKey is null)
                return Fail("event-kind", "This node does not accept changes.");
            if (!DocumentValidator.Matches(Document.State[node.StateKey], message.Value, data))
                return Fail("input-value", "The value does not match the field's type, range, or available records.");
            _values = _values.SetItem(node.StateKey, message.Value.Clone());
            _dirty = _values[node.StateKey].GetRawText() == _baseline[node.StateKey].GetRawText()
                ? _dirty.Remove(node.StateKey) : _dirty.Add(node.StateKey);
        }
        else if (message.Kind == UiEventKind.Sort)
        {
            if (node.Kind != NodeKind.Table || message.SortField is null ||
                !node.Fields.Any(field => field.Field == message.SortField))
                return Fail("sort-field", "Sorting requires a displayed table field.");
            _sorting = _sorting.SetItem(node.Id, new(message.SortField, message.Descending));
        }
        else
        {
            if (node.InteractionId is null) return Fail("event-kind", "This node has no declared interaction.");
            OutputInteraction interaction = Document.Interactions.Single(item => item.Id == node.InteractionId);
            if (interaction.Event != message.Kind ||
                (message.Kind == UiEventKind.Activate && message.Value.ValueKind != JsonValueKind.Null))
                return Fail("event-kind", "The event does not match the declared interaction.");
            if (message.Kind == UiEventKind.Select &&
                (message.Value.ValueKind != JsonValueKind.String ||
                 !DataSet(node.DataSetId!).Rows.Any(row => row[DataSet(node.DataSetId!).KeyField].GetString() == message.Value.GetString())))
                return Fail("event-record", "The selected record is not available to this workspace.");

            ImmutableDictionary<string, JsonElement> proposed = _values;
            if (interaction.ClearDrafts)
            {
                foreach (string state in _dirty) proposed = proposed.SetItem(state, _baseline[state]);
            }
            foreach (StateAssignment assignment in interaction.Assignments)
            {
                if (_dirty.Contains(assignment.StateKey))
                    return Fail("dirty-state", "Clear the draft before changing a record that supplies its values.");
                JsonElement value = Resolve(assignment.Value, _values, message.Value);
                if (!DocumentValidator.Matches(Document.State[assignment.StateKey], value, data))
                    return Fail("effect-value", "An interaction produced a value that does not match its state definition.");
                proposed = proposed.SetItem(assignment.StateKey, value);
            }
            _values = proposed;
            if (interaction.ClearDrafts) _dirty = ImmutableHashSet<string>.Empty;
            _baseline = _baseline.SetItems(proposed.Where(pair => !_dirty.Contains(pair.Key)));
        }
        Revision++;
        return Succeed();
    }

    public async Task<OperationResult> SubmitAsync(string input, CancellationToken cancellationToken = default)
    {
        if (_disposed) return Fail("disposed", "The session has been disposed.");
        if (string.IsNullOrWhiteSpace(input) || input.Length > 2048)
            return Fail("input-limit", "Enter between 1 and 2048 characters.");
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return Fail("busy", "An input request is already running.");

        long expectedRevision = Revision;
        using CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_cancellationGate) _cancellation = source;
        Errors = [];
        Changed?.Invoke();
        OperationResult result;
        string status;
        try
        {
            source.Token.ThrowIfCancellationRequested();
            OutputContent next = await processor.ProcessAsync(input.Trim(), Snapshot, source.Token);
            source.Token.ThrowIfCancellationRequested();
            result = Open(next, expectedRevision: expectedRevision);
            status = result.Success ? "completed" : "rejected";
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            result = Fail("cancelled", "The request was cancelled. The current workspace is unchanged.");
            status = "cancelled";
        }
        catch (InputRejectedException error)
        {
            result = Fail("input-rejected", error.Message.Length <= 500 ? error.Message : "The input was rejected.");
            status = "rejected";
        }
        catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException)
        {
            result = Fail("input-failed", "The input handler failed. The current workspace is unchanged.");
            status = "failed";
        }
        finally
        {
            lock (_cancellationGate) _cancellation = null;
            Interlocked.Exchange(ref _busy, 0);
        }
        History = History.Add(new(input.Trim(), status));
        if (History.Length > 20) History = History.RemoveAt(0);
        Changed?.Invoke();
        return result;
    }

    public void Cancel()
    {
        lock (_cancellationGate) _cancellation?.Cancel();
    }

    public void Dispose()
    {
        _disposed = true;
        Cancel();
        Changed = null;
    }

    private OutputDataSet DataSet(string id) => _content!.Data.Single(set => set.Id == id);

    private bool IsVisiblePath(OutputNode node)
    {
        if (!IsVisible(node)) return false;
        OutputNode? parent = Document!.Nodes.FirstOrDefault(candidate => candidate.Children.Contains(node.Id));
        return parent is null || IsVisiblePath(parent);
    }

    private JsonElement Resolve(ValueBinding binding, IReadOnlyDictionary<string, JsonElement> values, JsonElement eventValue)
    {
        switch (binding.Source)
        {
            case BindingSource.Literal: return binding.Literal;
            case BindingSource.State: return values[binding.StateKey!];
            case BindingSource.EventValue: return eventValue;
            case BindingSource.SelectedField:
            case BindingSource.EventField:
                JsonElement key = binding.Source == BindingSource.SelectedField ? values[binding.StateKey!] : eventValue;
                if (key.ValueKind == JsonValueKind.Null) return JsonValues.Null;
                OutputDataSet set = DataSet(binding.DataSetId!);
                return set.Rows.FirstOrDefault(row => row[set.KeyField].GetString() == key.GetString())?[binding.Field!] ?? JsonValues.Null;
            default: return JsonValues.Null;
        }
    }

    private OperationResult Succeed()
    {
        Errors = [];
        Changed?.Invoke();
        return OperationResult.Ok;
    }

    private OperationResult Fail(string code, string message) => Fail(OperationResult.Fail(code, message));

    private OperationResult Fail(OperationResult result)
    {
        Errors = result.Errors;
        Changed?.Invoke();
        return result;
    }

    private sealed class ScalarComparer : IComparer<JsonElement>
    {
        public static ScalarComparer Instance { get; } = new();
        public int Compare(JsonElement left, JsonElement right)
        {
            if (left.ValueKind == JsonValueKind.Null) return right.ValueKind == JsonValueKind.Null ? 0 : -1;
            if (right.ValueKind == JsonValueKind.Null) return 1;
            if (left.ValueKind == JsonValueKind.Number) return left.GetDecimal().CompareTo(right.GetDecimal());
            return StringComparer.OrdinalIgnoreCase.Compare(JsonValues.Display(left), JsonValues.Display(right));
        }
    }
}
