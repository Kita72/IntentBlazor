using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using IntentBlazor.Core;
using IntentBlazor.Runtime;
using Microsoft.VisualBasic.FileIO;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace IntentBlazor.Content;

public sealed class ContentRejectedException(string message) : Exception(message);
public sealed record ContentAnalysis(string? Text = null, bool TextTruncated = false, int? Width = null,
    int? Height = null, OutputDataSet? StructuredData = null);
public interface IContentAdapter
{
    ImmutableArray<ContentCapability> Capabilities { get; }
    ContentAnalysis Read(byte[] bytes, string mediaType, ContentLimits limits, CancellationToken cancellationToken);
}

public sealed class TextContentAdapter : IContentAdapter
{
    public ImmutableArray<ContentCapability> Capabilities { get; } =
        [new("text/plain", ".txt", true, true, false, true), new("text/markdown", ".md", true, true, false, true),
         new("application/json", ".json", true, true, false, true), new("text/csv", ".csv", true, true, false, true)];
    public ContentAnalysis Read(byte[] bytes, string mediaType, ContentLimits limits, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        if (text.Contains('\0')) throw new ContentRejectedException("Text content cannot contain null characters.");
        OutputDataSet? data = null;
        if (mediaType == "application/json")
        {
            using JsonDocument document = JsonDocument.Parse(text, new() { MaxDepth = 16 });
            RejectDuplicates(document.RootElement);
            if (document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() <= 250)
            {
                var rows = document.RootElement.EnumerateArray().ToArray();
                if (rows.Length > 0 && rows.All(row => row.ValueKind == JsonValueKind.Object))
                {
                    string[] names = rows[0].EnumerateObject().Select(property => property.Name).ToArray();
                    if (names.Length <= 31 && rows.All(row => row.EnumerateObject().Count() == names.Length && names.All(name => row.TryGetProperty(name, out _))))
                        data = Structured(names, rows.Select(row => names.Select(name => row.GetProperty(name).Clone()).ToArray()).ToArray());
                }
            }
        }
        else if (mediaType == "text/csv") data = Csv(text, cancellationToken);
        return new(text[..Math.Min(text.Length, limits.MaximumTextCharacters)], text.Length > limits.MaximumTextCharacters, StructuredData: data);
    }
    private static OutputDataSet Csv(string text, CancellationToken token)
    {
        using TextFieldParser parser = new(new StringReader(text)) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        string[] names = parser.ReadFields() ?? throw new ContentRejectedException("CSV requires a header row.");
        if (names.Length is < 1 or > 31 || names.Any(string.IsNullOrWhiteSpace)) throw new ContentRejectedException("CSV supports 1 to 31 named columns.");
        List<JsonElement[]> rows = [];
        while (!parser.EndOfData)
        {
            token.ThrowIfCancellationRequested();
            string[] values = parser.ReadFields()!;
            if (values.Length != names.Length || rows.Count >= 250) throw new ContentRejectedException("CSV rows must match the header; at most 250 rows are supported.");
            rows.Add(values.Select(JsonValues.From).ToArray());
        }
        return Structured(names, rows.ToArray()) ?? throw new ContentRejectedException("CSV cells exceed the supported text limits.");
    }
    private static OutputDataSet? Structured(string[] names, JsonElement[][] rows)
    {
        if (rows.Any(row => row.Any(value => value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null) ||
            (value.ValueKind == JsonValueKind.String && value.GetString()!.Length > 4096) || (value.ValueKind == JsonValueKind.Number && !value.TryGetDecimal(out _))))) return null;
        var fields = ImmutableDictionary<string, DataField>.Empty.Add("rowKey", new() { Kind = StateKind.Text });
        string[] keys = names.Select((name, index) => IsIdentifier(name) && name != "rowKey" && names.Count(other => other == name) == 1 ? name : "column" + (index + 1)).ToArray();
        if (keys.Distinct().Count() != keys.Length) keys = names.Select((_, index) => "column" + (index + 1)).ToArray();
        for (int i = 0; i < names.Length; i++)
        {
            var kinds = rows.Select(row => row[i].ValueKind).Where(kind => kind != JsonValueKind.Null).Distinct().ToArray();
            if (kinds.Length > 1 && !kinds.All(kind => kind is JsonValueKind.True or JsonValueKind.False)) return null;
            StateKind kind = kinds.FirstOrDefault() switch { JsonValueKind.Number => StateKind.Number, JsonValueKind.True or JsonValueKind.False => StateKind.Boolean, _ => StateKind.Text };
            fields = fields.Add(keys[i], new() { Kind = kind, Nullable = rows.Any(row => row[i].ValueKind == JsonValueKind.Null) });
        }
        return new()
        {
            Id = "imported",
            KeyField = "rowKey",
            Fields = fields,
            Rows = rows.Select((row, index) =>
            keys.Select((key, i) => KeyValuePair.Create(key, row[i])).ToImmutableDictionary().Add("rowKey", JsonValues.From((index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)))).ToImmutableArray()
        };
    }
    private static bool IsIdentifier(string name) => name.Length is > 0 and <= 64 && char.IsAsciiLetter(name[0]) && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw new ContentRejectedException("JSON contains duplicate properties."); RejectDuplicates(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (JsonElement child in value.EnumerateArray()) RejectDuplicates(child);
    }
}

public sealed class ImageContentAdapter : IContentAdapter
{
    public ImmutableArray<ContentCapability> Capabilities { get; } =
        [new("image/png", ".png", true, false, true, true), new("image/jpeg", ".jpg", true, false, true, true), new("image/jpeg", ".jpeg", true, false, true, true)];
    public ContentAnalysis Read(byte[] bytes, string mediaType, ContentLimits limits, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using SKCodec codec = SKCodec.Create(new SKMemoryStream(bytes)) ?? throw new ContentRejectedException("The image could not be decoded.");
        if ((mediaType == "image/png" && codec.EncodedFormat != SKEncodedImageFormat.Png) || (mediaType == "image/jpeg" && codec.EncodedFormat != SKEncodedImageFormat.Jpeg))
            throw new ContentRejectedException("The image signature does not match its declared format.");
        CheckDimensions(codec.Info.Width, codec.Info.Height, limits);
        using SKBitmap decoded = SKBitmap.Decode(codec) ?? throw new ContentRejectedException("The image is incomplete.");
        return new(Width: decoded.Width, Height: decoded.Height);
    }
    public static void CheckDimensions(int width, int height, ContentLimits limits)
    {
        if (width is < 1 or > 16384 || height is < 1 or > 16384 || (long)width * height > limits.MaximumPixels)
            throw new ContentRejectedException("The image dimensions exceed the configured pixel limit.");
    }
}

public sealed class PdfContentAdapter : IContentAdapter
{
    public ImmutableArray<ContentCapability> Capabilities { get; } = [new("application/pdf", ".pdf", true, true, false, true)];
    public ContentAnalysis Read(byte[] bytes, string mediaType, ContentLimits limits, CancellationToken cancellationToken)
    {
        if (!bytes.AsSpan().StartsWith("%PDF-"u8)) throw new ContentRejectedException("The file does not have a PDF signature.");
        using PdfDocument document = PdfDocument.Open(bytes, new ParsingOptions { UseLenientParsing = false, MaxStackDepth = 64 });
        if (document.NumberOfPages > limits.MaximumPdfPages) throw new ContentRejectedException("The PDF exceeds the configured page limit.");
        StringBuilder text = new(); bool truncated = false;
        for (int number = 1; number <= document.NumberOfPages; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string page = ContentOrderTextExtractor.GetText(document.GetPage(number));
            int room = limits.MaximumTextCharacters - text.Length;
            if (page.Length > room) { text.Append(page.AsSpan(0, Math.Max(0, room))); truncated = true; break; }
            text.Append(page); if (text.Length < limits.MaximumTextCharacters) text.Append('\n');
        }
        return new(text.ToString(), truncated);
    }
}
