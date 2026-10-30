using System.Text;
using IntentBlazor.Content;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

public sealed class ContentAdapterTests
{
    [Theory]
    [InlineData("txt", "text/plain")]
    [InlineData("md", "text/markdown")]
    [InlineData("json", "application/json")]
    [InlineData("csv", "text/csv")]
    [InlineData("png", "image/png")]
    [InlineData("jpg", "image/jpeg")]
    [InlineData("pdf", "application/pdf")]
    public async Task SupportedFormats_PreserveOriginalBytesAndExtractBoundedMetadata(string extension, string mediaType)
    {
        using ContentHarness h = new(); using ContentScope scope = h.Scope();
        byte[] bytes = extension switch
        {
            "png" => ContentSamples.Image(),
            "jpg" => ContentSamples.Image(jpeg: true),
            "pdf" => ContentSamples.Pdf(),
            "json" => "[{\"name\":\"Alpha\",\"count\":2}]"u8.ToArray(),
            "csv" => "name,notes\r\nAlpha,\"one,two\"\r\n"u8.ToArray(),
            _ => Encoding.UTF8.GetBytes("# Original synthetic content\n<script>alert('untrusted')</script>")
        };
        ContentArtifact artifact = await scope.Import(bytes, "sample." + extension, mediaType);
        Assert.Equal(mediaType, artifact.Reference.MediaType); Assert.Equal(bytes.Length, artifact.Bytes);
        Assert.Equal(bytes, await h.Provider.GetRequiredService<IArtifactStore>().ReadAsync("owner-a", artifact.Reference));
        Assert.Equal("User upload", artifact.Provenance); Assert.Null(artifact.Parent);
        if (extension is "png" or "jpg") { Assert.Equal(64, artifact.Width); Assert.Equal(64, artifact.Height); Assert.Null(artifact.Text); }
        else if (extension == "pdf") Assert.Contains("Original synthetic PDF text", artifact.Text);
        else Assert.NotEmpty(artifact.Text!);
        if (extension == "csv") Assert.Equal("one,two", artifact.StructuredData!.Rows[0]["notes"].GetString());
        if (extension == "json") Assert.Equal(2, artifact.StructuredData!.Rows[0]["count"].GetInt32());
    }

    [Theory]
    [InlineData("unsupported")]
    [InlineData("media-mismatch")]
    [InlineData("invalid-utf8")]
    [InlineData("duplicate-json")]
    [InlineData("csv-columns")]
    [InlineData("image-signature")]
    [InlineData("pdf-signature")]
    public async Task MalformedAndMismatchedContent_IsRejectedWithoutAnArtifact(string kind)
    {
        using ContentHarness h = new(); using ContentScope scope = h.Scope();
        (byte[] bytes, string name, string type) = kind switch
        {
            "unsupported" => ("<svg/>"u8.ToArray(), "image.svg", "image/svg+xml"),
            "media-mismatch" => ("text"u8.ToArray(), "notes.txt", "image/png"),
            "invalid-utf8" => (new byte[] { 255, 254, 255 }, "notes.txt", "text/plain"),
            "duplicate-json" => ("{\"x\":1,\"x\":2}"u8.ToArray(), "data.json", "application/json"),
            "csv-columns" => ("one,two\nonly-one"u8.ToArray(), "data.csv", "text/csv"),
            "image-signature" => (ContentSamples.Image(jpeg: true), "wrong.png", "image/png"),
            _ => ("not a PDF"u8.ToArray(), "wrong.pdf", "application/pdf")
        };
        await Assert.ThrowsAsync<ContentRejectedException>(async () => await scope.Import(bytes, name, type));
        Assert.Empty(await h.Provider.GetRequiredService<IArtifactStore>().ListAsync("owner-a"));
    }

    [Fact]
    public async Task StreamLimits_CheckActualBytesAndCancellation_WhileTextPreviewIsBounded()
    {
        using ContentHarness h = new(new(MaximumBytes: 8, MaximumTextCharacters: 4)); using ContentScope scope = h.Scope();
        ContentArtifact exact = await scope.Import("12345678"u8.ToArray(), "exact.txt", "text/plain");
        Assert.Equal("1234", exact.Text); Assert.True(exact.TextTruncated); Assert.Equal(8, exact.Bytes);
        await Assert.ThrowsAsync<ContentRejectedException>(async () => await scope.Import("123456789"u8.ToArray(), "over.txt", "text/plain"));
        using MemoryStream lies = new("12345"u8.ToArray());
        await Assert.ThrowsAsync<ContentRejectedException>(async () => await scope.Content.ImportAsync(new("lie.txt", "text/plain", 4), lies));
        using CancellationTokenSource cancel = new(); cancel.Cancel(); using MemoryStream cancelled = new("one"u8.ToArray());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await scope.Content.ImportAsync(new("cancel.txt", "text/plain", 3), cancelled, cancel.Token));
        Assert.Single(await h.Provider.GetRequiredService<IArtifactStore>().ListAsync("owner-a"));
    }

    [Fact]
    public async Task OversizedStructuredPreview_RetainsTheOriginalAndBoundedTextWithoutAnInvalidDataset()
    {
        using ContentHarness h = new(); using ContentScope scope = h.Scope();
        byte[] bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Enumerable.Range(0, 100).Select(index => new { name = index.ToString(), text = new string('x', 3000) }));
        ContentArtifact artifact = await scope.Import(bytes, "large.json", "application/json");
        Assert.Null(artifact.StructuredData); Assert.True(artifact.TextTruncated); Assert.Equal(16000, artifact.Text!.Length);
        Assert.Equal(bytes, await h.Provider.GetRequiredService<IArtifactStore>().ReadAsync("owner-a", artifact.Reference));
    }

    [Theory]
    [InlineData("pixels")]
    [InlineData("pdf-pages")]
    [InlineData("csv-rows")]
    public async Task ProcessingLimits_RejectExcessivePixelsPagesAndRows(string limit)
    {
        using ContentHarness h = new(new(MaximumPixels: 4095, MaximumPdfPages: 1)); using ContentScope scope = h.Scope();
        if (limit == "pixels") await Assert.ThrowsAsync<ContentRejectedException>(async () => await scope.Import(ContentSamples.Image(), "large.png", "image/png"));
        else if (limit == "pdf-pages") await Assert.ThrowsAsync<ContentRejectedException>(async () => await scope.Import(ContentSamples.Pdf(2), "large.pdf", "application/pdf"));
        else await Assert.ThrowsAsync<ContentRejectedException>(async () => await scope.Import(Encoding.UTF8.GetBytes("name\n" + string.Join('\n', Enumerable.Repeat("record", 251))), "large.csv", "text/csv"));
        Assert.Empty(await h.Provider.GetRequiredService<IArtifactStore>().ListAsync("owner-a"));
    }
}
