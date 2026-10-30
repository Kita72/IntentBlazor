using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace IntentBlazor.Core;

public sealed partial class DocumentValidator
{
    public static string ExtensionKey(string pack, string version, string renderer) => $"{pack}/{version}/{renderer}";
    public bool TryExtension(ExtensionNode extension, [NotNullWhen(true)] out IOutputExtensionContract? contract) =>
        Extensions.TryGetValue(ExtensionKey(extension.Pack, extension.Version, extension.Renderer), out contract);

    private void ValidateAdditionalNode(OutputNode node, OutputContent content)
    {
        bool additional = node.Kind is NodeKind.Markdown or NodeKind.Chart or NodeKind.Gallery or NodeKind.Artifact or NodeKind.Extension;
        Need(!additional || content.Document.SchemaVersion == "0.3", "node-version", node.Id, "This node requires schema 0.3.");
        Need(node.Kind == NodeKind.Extension || node.Extension is null, "extension-node", node.Id, "Extension configuration belongs to extension nodes.");
        Need(node.Kind == NodeKind.Chart || node.Chart is null, "chart-node", node.Id, "Chart configuration belongs to chart nodes.");
        if (additional) Need(!string.IsNullOrWhiteSpace(node.Title), "label", node.Id, "Content and extension nodes require labels.");
        if (node.Kind == NodeKind.Gallery)
            Need(node.DataSetId is not null && content.Data.Any(set => set.Id == node.DataSetId && node.ImageField is not null &&
                set.Fields.TryGetValue(node.ImageField, out DataField? field) && field.Kind == StateKind.Text),
                "gallery-field", node.Id, "A gallery requires a host-owned image field.");
        if (node.Kind == NodeKind.Chart)
        {
            ChartDefinition? chart = node.Chart;
            OutputDataSet? set = content.Data.FirstOrDefault(item => item.Id == node.DataSetId);
            Need(chart is not null && Enum.IsDefined(chart.Kind) && set is not null && set.Rows.Length <= 50 &&
                set.Fields.TryGetValue(chart.LabelField, out DataField? label) && label.Kind == StateKind.Text &&
                set.Fields.TryGetValue(chart.ValueField, out DataField? value) && value.Kind == StateKind.Number &&
                set.Rows.All(row => row[chart.LabelField].ValueKind == JsonValueKind.String && row[chart.ValueField].TryGetDecimal(out decimal number) &&
                    number is >= -1000000000 and <= 1000000000), "chart-fields", node.Id, "Charts require up to 50 labelled numeric values between -1e9 and 1e9.");
        }
        if (node.Kind is NodeKind.Markdown or NodeKind.Artifact)
            Need(node.Value is not null && ValidateBinding(node.Value, content.Document, content.Data.ToDictionary(set => set.Id), null) is null or StateKind.Text or StateKind.RecordReference,
                "content-binding", node.Id, "Content nodes require a text or artifact-reference binding.");
        if (node.Kind == NodeKind.Extension)
        {
            ExtensionNode? extension = node.Extension;
            Need(extension is not null && Identifier(extension.Pack) && Identifier(extension.Renderer) &&
                extension.Version is { Length: > 0 and <= 32 } && extension.Version.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-') &&
                extension.Configuration.ValueKind == JsonValueKind.Object && extension.Configuration.GetRawText().Length <= 8192 &&
                extension.Fallback is { Length: > 0 and <= 500 }, "extension-config", node.Id, "An extension needs a bounded typed configuration, version and fallback.");
            if (content.Document.Requires.TryGetValue(extension.Pack, out string? required))
                Need(required == extension.Version, "extension-version", node.Id, "The extension version conflicts with its required pack.");
            if (TryExtension(extension, out IOutputExtensionContract? contract))
            {
                OperationResult validation = contract.Validate(extension.Configuration, content);
                Need(validation.Success, "extension-config", node.Id, validation.Success ? "" : validation.Errors.FirstOrDefault()?.Message ?? "The extension rejected its configuration.");
                Need(contract.EditableStates(extension.Configuration).All(content.Document.State.ContainsKey), "extension-state", node.Id, "An extension references unavailable state.");
            }
            else Need(node.Children.IsEmpty, "extension-fallback", node.Id, "An unavailable optional extension must have no interactive children.");
        }
    }
}
