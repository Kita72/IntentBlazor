using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace IntentBlazor.Core;

public sealed class DocumentValidator(IEnumerable<ActionDefinition>? actions = null)
{
    public IReadOnlyDictionary<string, ActionDefinition> Actions { get; } =
        (actions ?? []).ToImmutableDictionary(action => action.Name, StringComparer.Ordinal);
    public OperationResult Validate(OutputContent content)
    {
        try
        {
            ValidateCore(content);
            return OperationResult.Ok;
        }
        catch (InvalidOutputException error)
        {
            return new(false, [error.Error]);
        }
        catch (Exception error) when (error is JsonException or ObjectDisposedException or InvalidOperationException or ArgumentException)
        {
            return OperationResult.Fail("invalid-json", "Output must contain bounded, readable JSON values.");
        }
    }

    public static bool Matches(StateDefinition definition, JsonElement value,
        IReadOnlyDictionary<string, OutputDataSet> data)
    {
        if (value.ValueKind == JsonValueKind.Null) return definition.Nullable;
        return definition.Kind switch
        {
            StateKind.Text => value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= 4096,
            StateKind.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            StateKind.Number => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out decimal number) &&
                (definition.Minimum is null || number >= definition.Minimum) &&
                (definition.Maximum is null || number <= definition.Maximum),
            StateKind.RecordReference => value.ValueKind == JsonValueKind.String &&
                definition.DataSetId is not null && data.TryGetValue(definition.DataSetId, out OutputDataSet? set) &&
                set.Rows.Any(row => row[set.KeyField].GetString() == value.GetString()),
            _ => false
        };
    }

    private void ValidateCore(OutputContent content)
    {
        Need(content is not null && content.Document is not null, "missing-document", "", "A document is required.");
        OutputDocument doc = content.Document;
        Need(doc.SchemaVersion is "0.1" or "0.2", "schema-version", "schemaVersion", "Only schema 0.1 and 0.2 are supported.");
        Need(Identifier(doc.Id) && doc.Revision > 0, "identity", "id", "A valid ID and positive revision are required.");
        Need(doc.Requires is not null && doc.Requires.Count > 0 &&
            doc.Requires.All(pair => pair.Key == "builtin" && pair.Value == "0.1"),
            "toolkit", "requires", "Only the builtin 0.1 toolkit is available.");
        Need(!doc.Nodes.IsDefault && doc.Nodes.Length is > 0 and <= 128, "node-limit", "nodes", "Use 1 to 128 nodes.");
        Need(doc.State is not null && doc.State.Count <= 64, "state-limit", "state", "Use at most 64 state slots.");
        Need(!doc.DataSets.IsDefault && doc.DataSets.Length <= 16 && !content.Data.IsDefault &&
            content.Data.Length == doc.DataSets.Length, "datasets", "dataSets", "Provide exactly the declared datasets, up to 16.");
        Need(!doc.Interactions.IsDefault && doc.Interactions.Length <= 64, "interaction-limit", "interactions", "Use at most 64 interactions.");
        Need(doc.Nodes.All(node => node is not null && Identifier(node.Id) && Enum.IsDefined(node.Kind)) &&
            doc.Nodes.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count() == doc.Nodes.Length,
            "node-identity", "nodes", "Node IDs must be valid and unique.");
        Need(doc.DataSets.All(Identifier) && doc.DataSets.Distinct(StringComparer.Ordinal).Count() == doc.DataSets.Length &&
            content.Data.All(set => set is not null && Identifier(set.Id)) &&
            content.Data.Select(set => set.Id).Distinct(StringComparer.Ordinal).Count() == content.Data.Length,
            "dataset-identity", "dataSets", "Dataset IDs must be valid and unique.");

        Dictionary<string, OutputDataSet> data = content.Data.ToDictionary(set => set.Id, StringComparer.Ordinal);
        Need(doc.DataSets.All(data.ContainsKey), "dataset-reference", "dataSets", "A dataset is unavailable.");
        foreach (OutputDataSet set in data.Values)
        {
            Need(set.Fields is not null && set.Fields.Count is > 0 and <= 32 &&
                set.Fields.All(pair => Identifier(pair.Key) && pair.Value is not null &&
                    Enum.IsDefined(pair.Value.Kind) && pair.Value.Kind != StateKind.RecordReference),
                "data-schema", set.Id, "Datasets require 1 to 32 scalar fields.");
            Need(Identifier(set.KeyField) && set.Fields.TryGetValue(set.KeyField, out DataField? key) &&
                key.Kind == StateKind.Text && !key.Nullable,
                "data-key", set.Id, "Dataset keys must be non-null text fields.");
            Need(!set.Rows.IsDefault && set.Rows.Length <= 250, "row-limit", set.Id, "Use at most 250 rows per dataset.");
            HashSet<string> keys = new(StringComparer.Ordinal);
            foreach (ImmutableDictionary<string, JsonElement> row in set.Rows)
            {
                Need(row is not null && row.Count == set.Fields.Count && set.Fields.All(pair =>
                    row.TryGetValue(pair.Key, out JsonElement value) &&
                    Matches(new StateDefinition { Kind = pair.Value.Kind, Nullable = pair.Value.Nullable }, value, data)),
                    "data-value", set.Id, "Each row must match its declared scalar fields.");
                string rowKey = row[set.KeyField].GetString()!;
                Need(rowKey.Length is > 0 and <= 128 && keys.Add(rowKey), "duplicate-key", set.Id, "Row keys must be nonempty and unique.");
            }
        }

        foreach ((string name, StateDefinition definition) in doc.State)
        {
            Need(Identifier(name) && definition is not null && Enum.IsDefined(definition.Kind),
                "state-definition", name, "State names and types must be valid.");
            Need((definition.Kind == StateKind.RecordReference) == (definition.DataSetId is not null) &&
                (definition.DataSetId is null || data.ContainsKey(definition.DataSetId)),
                "state-dataset", name, "Record references require an available dataset.");
            Need((definition.Minimum is null && definition.Maximum is null) || definition.Kind == StateKind.Number,
                "state-range", name, "Ranges apply only to numeric state.");
            Need(definition.Minimum is null || definition.Maximum is null || definition.Minimum <= definition.Maximum,
                "state-range", name, "The minimum cannot exceed the maximum.");
            Need(Matches(definition, definition.InitialValue, data), "state-value", name, "The initial value does not match the state definition.");
        }

        Need(doc.Interactions.All(interaction => interaction is not null && Identifier(interaction.Id)) &&
            doc.Interactions.Select(interaction => interaction.Id).Distinct(StringComparer.Ordinal).Count() == doc.Interactions.Length,
            "interaction-identity", "interactions", "Interaction IDs must be valid and unique.");
        Dictionary<string, OutputInteraction> interactions = doc.Interactions.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (OutputInteraction interaction in interactions.Values)
        {
            Need(interaction.Action is null || (doc.SchemaVersion == "0.2" && Actions.ContainsKey(interaction.Action)),
                "unsupported-action", interaction.Id, "Actions require schema 0.2 and an explicitly registered definition.");
            Need(interaction.Event is UiEventKind.Select or UiEventKind.Activate && !interaction.Assignments.IsDefault &&
                interaction.Assignments.Length <= 16, "interaction", interaction.Id, "Declare bounded local select or activate effects.");
            Need(interaction.Assignments.All(assignment => assignment is not null && assignment.Value is not null &&
                Identifier(assignment.StateKey) && doc.State.ContainsKey(assignment.StateKey)) &&
                interaction.Assignments.Select(assignment => assignment.StateKey).Distinct(StringComparer.Ordinal).Count() == interaction.Assignments.Length,
                "effect-target", interaction.Id, "Assignments require unique existing state targets.");
            Need(interaction.Action is not null ? interaction.Assignments.IsEmpty && !interaction.ClearDrafts :
                interaction.ClearDrafts ? interaction.Assignments.IsEmpty : !interaction.Assignments.IsEmpty,
                "empty-effect", interaction.Id, "Declare assignments or a clear-draft effect, without combining them.");
            if (interaction.Action is null)
                Need(interaction.Resource is null && interaction.ExpectedVersion is null && interaction.Arguments is { Count: 0 },
                    "action-properties", interaction.Id, "Action bindings belong only to registered actions.");
        }

        Dictionary<string, OutputNode> nodes = doc.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        Need(Identifier(doc.RootId) && nodes.ContainsKey(doc.RootId), "root", "rootId", "The root node must exist.");
        foreach (OutputNode node in doc.Nodes)
        {
            Need(node.Title is not null && node.Title.Length <= 160 && !node.Children.IsDefault && !node.Fields.IsDefault,
                "node-properties", node.Id, "Node labels and collections must be valid.");
            bool container = node.Kind is NodeKind.Layout or NodeKind.Card or NodeKind.Form;
            Need(container || node.Children.IsEmpty, "leaf-children", node.Id, "Only layout, card, and form nodes have children.");
            Need(node.Children.All(nodes.ContainsKey), "child-reference", node.Id, "A child node is unavailable.");
            if (node.WhenState is not null)
                Need(doc.State.TryGetValue(node.WhenState, out StateDefinition? condition) &&
                    condition.Kind is StateKind.Boolean or StateKind.RecordReference,
                    "condition", node.Id, "Conditions use boolean state or record selection.");

            if (node.Kind is NodeKind.Text or NodeKind.Rating or NodeKind.Image)
            {
                Need(node.Value is not null, "binding", node.Id, "This node requires a value binding.");
                StateKind? kind = ValidateBinding(node.Value, doc, data, null);
                Need(node.Kind != NodeKind.Rating || (kind is null or StateKind.Number &&
                    node.RatingMaximum is >= 1 and <= 10), "rating", node.Id, "Ratings require numeric values and a scale from 1 to 10.");
                Need(node.Kind != NodeKind.Image || kind is null or StateKind.Text, "image", node.Id, "Image sources must be text.");
                Need(node.Kind != NodeKind.Image || node.Value.Source == BindingSource.SelectedField,
                    "image-binding", node.Id, "Images must bind a selected field from host-provided data.");
            }
            if (node.Kind is NodeKind.List or NodeKind.Table or NodeKind.Select)
            {
                Need(node.DataSetId is not null && data.ContainsKey(node.DataSetId), "node-dataset", node.Id, "A declared dataset is required.");
                OutputDataSet set = data[node.DataSetId];
                if (node.Kind is NodeKind.List or NodeKind.Select)
                    Need(node.LabelField is not null && set.Fields.TryGetValue(node.LabelField, out DataField? field) &&
                        field.Kind == StateKind.Text, "label-field", node.Id, "A text label field is required.");
                if (node.Kind == NodeKind.Table)
                    Need(node.Fields.Length is > 0 and <= 16 && node.Fields.All(field => field is not null &&
                        set.Fields.ContainsKey(field.Field) && !string.IsNullOrWhiteSpace(field.Label) && field.Label.Length <= 160) &&
                        node.Fields.Select(field => field.Field).Distinct(StringComparer.Ordinal).Count() == node.Fields.Length,
                        "table-fields", node.Id, "Tables require unique declared fields and labels.");
                if (node.SelectionState is not null)
                    Need(doc.State.TryGetValue(node.SelectionState, out StateDefinition? selection) &&
                        selection.Kind == StateKind.RecordReference && selection.DataSetId == node.DataSetId,
                        "selection", node.Id, "Selection state must reference this dataset.");
            }
            if (node.Kind is NodeKind.Input or NodeKind.Select)
            {
                Need(node.StateKey is not null && doc.State.TryGetValue(node.StateKey, out _),
                    "input-state", node.Id, "Inputs require an existing state slot.");
                StateDefinition state = doc.State[node.StateKey];
                Need(node.Kind == NodeKind.Select
                    ? state.Kind == StateKind.RecordReference && state.DataSetId == node.DataSetId
                    : state.Kind is StateKind.Text or StateKind.Number,
                    "input-type", node.Id, "Input type and state must agree.");
            }
            if (node.Kind is NodeKind.List or NodeKind.Table or NodeKind.Rating or NodeKind.Input or NodeKind.Select or NodeKind.Button or NodeKind.Image)
                Need(!string.IsNullOrWhiteSpace(node.Title), "label", node.Id, "Interactive and media nodes require a label.");

            if (node.InteractionId is not null)
            {
                Need(interactions.TryGetValue(node.InteractionId, out _), "interaction-reference", node.Id, "The interaction is unavailable.");
                OutputInteraction interaction = interactions[node.InteractionId];
                Need(node.Kind == NodeKind.Button ? interaction.Event == UiEventKind.Activate :
                    node.Kind is NodeKind.List or NodeKind.Table && interaction.Event == UiEventKind.Select,
                    "event-kind", node.Id, "The node and interaction event kinds do not agree.");
                if (interaction.Action is { } name)
                {
                    ActionDefinition definition = Actions[name];
                    Need(node.Kind == NodeKind.Button && interaction.Resource is { Source: BindingSource.State, StateKey: not null } &&
                        doc.State.TryGetValue(interaction.Resource.StateKey, out StateDefinition? resource) &&
                        resource.Kind == StateKind.RecordReference && resource.DataSetId == definition.DataSetId,
                        "action-resource", node.Id, "Actions target selected host-owned records.");
                    Need(interaction.ExpectedVersion is not null &&
                        ValidateBinding(interaction.ExpectedVersion, doc, data, null) is StateKind.Text or StateKind.Number,
                        "action-version", node.Id, "Actions require a text or numeric expected record version.");
                    Need(interaction.Arguments is not null && interaction.Arguments.Count == definition.Parameters.Count &&
                        interaction.Arguments.All(pair => definition.Parameters.ContainsKey(pair.Key) && pair.Value is not null),
                        "action-arguments", node.Id, "Arguments must match the registered action parameters.");
                    foreach (var pair in interaction.Arguments)
                    {
                        StateDefinition parameter = definition.Parameters[pair.Key];
                        StateKind? sourceKind = ValidateBinding(pair.Value, doc, data, null);
                        Need(sourceKind == parameter.Kind || (sourceKind == StateKind.RecordReference && parameter.Kind == StateKind.Text) ||
                            (sourceKind is null && parameter.Nullable), "action-argument-type", node.Id, "An action argument has the wrong type.");
                    }
                }
                foreach (StateAssignment assignment in interaction.Assignments)
                {
                    StateKind? source = ValidateBinding(assignment.Value, doc, data, node);
                    StateKind target = doc.State[assignment.StateKey].Kind;
                    Need(source is null || source == target ||
                        (source == StateKind.Text && target == StateKind.RecordReference) ||
                        (source == StateKind.RecordReference && target == StateKind.Text),
                        "effect-type", node.Id, "The assignment type does not match its target.");
                    if (assignment.Value.Source == BindingSource.Literal)
                        Need(Matches(doc.State[assignment.StateKey], assignment.Value.Literal, data),
                            "effect-value", node.Id, "The literal assignment is invalid.");
                }
            }
            Need(node.Kind != NodeKind.Button || node.InteractionId is not null, "button-interaction", node.Id, "Buttons require a local interaction.");
        }
        HashSet<string> visited = new(StringComparer.Ordinal);
        Visit(doc.RootId, nodes, visited, 0);
        Need(visited.Count == nodes.Count, "unreachable-node", "nodes", "All nodes must belong to the root tree.");
        Need(interactions.Keys.All(id => doc.Nodes.Any(node => node.InteractionId == id)),
            "unused-interaction", "interactions", "Every interaction must be attached to a node.");
        _ = OutputJson.SerializeContent(content);
    }

    private static StateKind? ValidateBinding(ValueBinding binding, OutputDocument doc,
        Dictionary<string, OutputDataSet> data, OutputNode? eventNode)
    {
        Need(Enum.IsDefined(binding.Source), "binding-source", "", "Unknown binding source.");
        switch (binding.Source)
        {
            case BindingSource.Literal:
                Need(binding.Literal.ValueKind is JsonValueKind.Null or JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False,
                    "literal", "", "Literal bindings must be scalar.");
                Need(binding.Literal.ValueKind != JsonValueKind.Number || binding.Literal.TryGetDecimal(out _),
                    "literal", "", "Numbers must fit the decimal range.");
                Need(binding.Literal.ValueKind != JsonValueKind.String || binding.Literal.GetString()!.Length <= 4096,
                    "literal", "", "Text bindings cannot exceed 4096 characters.");
                return binding.Literal.ValueKind switch
                {
                    JsonValueKind.String => StateKind.Text,
                    JsonValueKind.Number => StateKind.Number,
                    JsonValueKind.True or JsonValueKind.False => StateKind.Boolean,
                    _ => null
                };
            case BindingSource.State:
                Need(binding.StateKey is not null && doc.State.ContainsKey(binding.StateKey), "binding-state", "", "A state binding must name an existing slot.");
                return doc.State[binding.StateKey].Kind;
            case BindingSource.SelectedField:
                Need(binding.StateKey is not null && doc.State.TryGetValue(binding.StateKey, out StateDefinition? state) &&
                    state.Kind == StateKind.RecordReference && state.DataSetId == binding.DataSetId,
                    "binding-selection", "", "A selected field requires matching record-reference state.");
                break;
            case BindingSource.EventValue:
                Need(eventNode?.Kind is NodeKind.List or NodeKind.Table, "binding-event", "", "Event values are available only in selection effects.");
                return StateKind.Text;
            case BindingSource.EventField:
                Need(eventNode?.Kind is NodeKind.List or NodeKind.Table && eventNode.DataSetId == binding.DataSetId,
                    "binding-event", "", "Event fields must come from the selected dataset.");
                break;
        }
        Need(binding.DataSetId is not null && data.TryGetValue(binding.DataSetId, out _) &&
            binding.Field is not null && data[binding.DataSetId].Fields.ContainsKey(binding.Field),
            "binding-field", "", "The dataset field is unavailable.");
        return data[binding.DataSetId].Fields[binding.Field].Kind;
    }

    private static void Visit(string id, Dictionary<string, OutputNode> nodes, HashSet<string> visited, int depth)
    {
        Need(depth <= 16, "depth-limit", id, "Node trees cannot exceed depth 16.");
        Need(visited.Add(id), "node-cycle", id, "Nodes cannot be cyclic or shared by multiple parents.");
        foreach (string child in nodes[id].Children) Visit(child, nodes, visited, depth + 1);
    }

    private static bool Identifier(string? value) => value is { Length: > 0 and <= 64 } &&
        char.IsAsciiLetter(value[0]) && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static void Need([DoesNotReturnIf(false)] bool condition, string code, string path, string message)
    {
        if (!condition) throw new InvalidOutputException(new(code, path, message));
    }

    private sealed class InvalidOutputException(DocumentError error) : Exception(error.Message)
    {
        public DocumentError Error { get; } = error;
    }
}
