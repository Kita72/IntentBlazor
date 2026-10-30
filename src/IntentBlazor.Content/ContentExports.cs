using System.Text;
using System.Text.Json;
using IntentBlazor.Core;

namespace IntentBlazor.Content;

public sealed record ContentExport(byte[] Bytes, string MediaType, string Extension);
public static class ContentExports
{
    public static ContentExport Write(OutputDataSet data, string format)
    {
        OutputContent probe = new()
        {
            Data = [data],
            Document = new()
            {
                Id = "export",
                RootId = "root",
                DataSets = [data.Id],
                Nodes = [new() { Id = "root", Kind = NodeKind.Text, Value = ValueBinding.Constant("Export") }]
            }
        };
        if (!new DocumentValidator().Validate(probe).Success) throw new ContentRejectedException("The export dataset is invalid or oversized.");
        string[] fields = data.Fields.Keys.Order(StringComparer.Ordinal).ToArray();
        if (format == "json") return new(JsonSerializer.SerializeToUtf8Bytes(data.Rows, OutputJson.Options), "application/json", ".json");
        if (format != "csv") throw new ContentRejectedException("Choose JSON or CSV export.");
        StringBuilder csv = new(); csv.Append(string.Join(',', fields.Select(Quote))).Append("\r\n");
        foreach (var row in data.Rows)
            csv.Append(string.Join(',', fields.Select(field => Quote(Cell(row[field]))))).Append("\r\n");
        return new(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", ".csv");
    }
    private static string Cell(JsonElement value)
    {
        string cell = JsonValues.Display(value);
        return value.ValueKind == JsonValueKind.String && cell.Length > 0 && (cell[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n') ? "'" + cell : cell;
    }
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
