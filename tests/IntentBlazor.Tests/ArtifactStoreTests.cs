using System.Security.Claims;
using System.Text;
using IntentBlazor.Content;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

public sealed class ArtifactStoreTests
{
    [Fact]
    public async Task Artifacts_AreOwnerScopedImmutableAndReadableAfterReopening()
    {
        using ContentHarness h = new(); using ContentScope alice = h.Scope(); using ContentScope bob = h.Scope("owner-b");
        ContentArtifact artifact = await alice.Import("Alice private file"u8.ToArray(), "../private.txt", "text/plain");
        Assert.Equal("private.txt", artifact.Name);
        Assert.Null(await bob.Content.DescribeAsync(artifact.Reference));
        IArtifactStore reopened = new FileArtifactStore(new(h.DirectoryPath));
        Assert.Equal("Alice private file", Encoding.UTF8.GetString((await reopened.ReadAsync("owner-a", artifact.Reference))!));
        await Assert.ThrowsAsync<ContentRejectedException>(async () => await reopened.ReadAsync("owner-a", artifact.Reference with { Id = "../outside" }));
        await Assert.ThrowsAsync<ContentRejectedException>(async () => await reopened.PutAsync("owner-a", artifact, "replacement"u8.ToArray()));
        Assert.Equal("Alice private file", Encoding.UTF8.GetString((await reopened.ReadAsync("owner-a", artifact.Reference))!));
        alice.Identity.Owner = null;
        await Assert.ThrowsAsync<ContentRejectedException>(async () => await alice.Content.DescribeAsync(artifact.Reference));
    }

    [Fact]
    public async Task StorageQuotas_RejectAdditionalBytesAndCleanupRetainsDerivedParents()
    {
        using (ContentHarness bounded = new(ownerBytes: 5, maximumArtifacts: 1))
        using (ContentScope scope = bounded.Scope())
        {
            await scope.Import("12345"u8.ToArray(), "one.txt", "text/plain");
            await Assert.ThrowsAsync<ContentRejectedException>(async () => await scope.Import("1"u8.ToArray(), "two.txt", "text/plain"));
            Assert.Single(await bounded.Provider.GetRequiredService<IArtifactStore>().ListAsync("owner-a"));
        }
        using ContentHarness h = new(); using ContentScope alice = h.Scope(); using ContentScope bob = h.Scope("owner-b");
        ContentArtifact original = await alice.Import(ContentSamples.Image(), "original.png", "image/png");
        ContentArtifact derived = await alice.Content.ResizeAsync(original.Reference, 16, 16, Guid.NewGuid().ToString("N"));
        ContentArtifact unused = await alice.Import("unused"u8.ToArray(), "unused.txt", "text/plain");
        ContentArtifact otherOwner = await bob.Import("retain Bob"u8.ToArray(), "bob.txt", "text/plain");
        IArtifactStore store = h.Provider.GetRequiredService<IArtifactStore>();
        Assert.Equal(1, await store.CleanupAsync("owner-a", DateTimeOffset.MaxValue, [derived.Reference]));
        Assert.Null(await store.DescribeAsync("owner-a", unused.Reference));
        Assert.NotNull(await store.DescribeAsync("owner-a", original.Reference));
        Assert.NotNull(await store.DescribeAsync("owner-a", derived.Reference));
        Assert.NotNull(await store.DescribeAsync("owner-b", otherOwner.Reference));
    }

    [Fact]
    public async Task Downloads_RequireIdentityAndReturnAuthorizedExactBytesWithSafeHeaders()
    {
        using ContentHarness h = new(); using ContentScope scope = h.Scope();
        byte[] bytes = "<script>untrusted file</script>"u8.ToArray();
        ContentArtifact artifact = await scope.Import(bytes, "untrusted.txt", "text/plain");
        IArtifactStore store = h.Provider.GetRequiredService<IArtifactStore>();
        DefaultHttpContext context = new() { RequestServices = scope.Services }; context.Response.Body = new MemoryStream();
        IResult denied = await ArtifactDownload.ReadAsync(context, store, "owner-a", artifact.Reference, false);
        await denied.ExecuteAsync(context); Assert.Equal(401, context.Response.StatusCode);
        context = Http(scope.Services); var missing = await ArtifactDownload.ReadAsync(context, store, "owner-b", artifact.Reference, false);
        await missing.ExecuteAsync(context); Assert.Equal(404, context.Response.StatusCode);
        context = Http(scope.Services); var allowed = await ArtifactDownload.ReadAsync(context, store, "owner-a", artifact.Reference, false);
        await allowed.ExecuteAsync(context);
        Assert.Equal(200, context.Response.StatusCode); Assert.Equal(bytes, ((MemoryStream)context.Response.Body).ToArray());
        Assert.Contains("attachment", context.Response.Headers.ContentDisposition.ToString());
        Assert.Equal("no-store", context.Response.Headers.CacheControl); Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions);
    }

    [Fact]
    public void DatasetExports_ProduceValidJsonAndCsvWithoutSpreadsheetFormulaExecution()
    {
        OutputDataSet data = TestContent.Catalog.Get("news").Data[0];
        data = data with { Rows = [data.Rows[0].SetItem("title", JsonValues.From("=SUM(1,2)"))] };
        ContentExport json = ContentExports.Write(data, "json"), csv = ContentExports.Write(data, "csv");
        using var parsed = System.Text.Json.JsonDocument.Parse(json.Bytes);
        Assert.Equal("=SUM(1,2)", parsed.RootElement[0].GetProperty("title").GetString());
        Assert.Contains("\"'=SUM(1,2)\"", Encoding.UTF8.GetString(csv.Bytes));
        Assert.Equal("text/csv", csv.MediaType); Assert.Throws<ContentRejectedException>(() => ContentExports.Write(data, "html"));
    }
    private static DefaultHttpContext Http(IServiceProvider services)
    {
        DefaultHttpContext context = new()
        {
            RequestServices = services,
            User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "owner-a")], "test"))
        };
        context.Response.Body = new MemoryStream(); return context;
    }
}
