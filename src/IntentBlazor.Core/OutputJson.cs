using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntentBlazor.Core;

public static class OutputJson
{
    public const int MaximumBytes = 262144;
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string SerializeDocument(OutputDocument document) => Serialize(document);
    public static string SerializeContent(OutputContent content) => Serialize(content);
    public static OutputDocument ReadDocument(string json) => Read<OutputDocument>(json);
    public static OutputContent ReadContent(string json) => Read<OutputContent>(json);

    private static string Serialize<T>(T value)
    {
        string json = JsonSerializer.Serialize(value, Options);
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new JsonException("The output exceeds the 256 KiB limit.");
        return json;
    }

    private static T Read<T>(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new JsonException("The output exceeds the 256 KiB limit.");

        using JsonDocument parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        RejectDuplicates(parsed.RootElement);
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Output cannot be null.");
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException($"Duplicate property: {property.Name}.");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray()) RejectDuplicates(item);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            MaxDepth = 32,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
