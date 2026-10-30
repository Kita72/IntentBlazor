using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using IntentBlazor.GamingNews;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

public sealed class Phase5NewsTests
{
    [Fact]
    public async Task PreferencesComparisonBookmarksAndSources_SurviveFollowupAndReopenWithoutAnotherOwnersLibrary()
    {
        using Phase5Harness h = await Phase5Harness.CreateAsync("news"); using Phase5Scope alex = h.Scope(); await alex.StartAsync();
        alex.Change("preference", "Emberfall"); alex.Select("articles", "ember-142"); alex.Change("comparisonPick", "ember-141");
        Assert.True((await alex.Session.SubmitAsync("Compare the current shield and route changes")).Success);
        Assert.Equal("ember-142", TestContent.Value(alex.Session, "selectedArticle")); Assert.Equal("ember-141", TestContent.Value(alex.Session, "comparisonArticle"));
        Assert.Equal("Emberfall", TestContent.Value(alex.Session, "preferredGame")); Assert.Equal("Emberfall", alex.Session.Data[0].Rows[0]["game"].GetString());
        Assert.Equal(ActionStatus.Succeeded, (await alex.ActivateAsync("bookmark")).Status);
        Assert.Equal(ActionStatus.Succeeded, (await alex.ActivateAsync("saveComparison")).Status);
        string acknowledged = (await alex.RenderAsync()).Output;
        Assert.Contains("Succeeded", acknowledged); Assert.Contains("Unsaved local draft", acknowledged);
        Assert.DoesNotContain("No application data has been saved", acknowledged);
        NewsLibrary library = h.Provider.GetRequiredService<NewsLibrary>(); SavedComparison saved = Assert.Single(library.Comparisons("alex"));
        Assert.Equal("ember-142", Assert.Single(library.Bookmarks("alex")).Id); Assert.Equal("1.4.2", saved.First.GameVersion);
        alex.Select("saved", saved.Id); Assert.True((await alex.Session.SaveAsync()).Success);
        h.Source.Articles = []; // An archived comparison must remain usable after the feed drops its articles.
        using Phase5Scope reopened = h.Scope(); await reopened.StartAsync();
        Assert.Equal(saved.Id, TestContent.Value(reopened.Session, "savedComparison")); Assert.Equal("ember-142", TestContent.Value(reopened.Session, "selectedArticle"));
        var html = await reopened.RenderAsync(); Assert.Contains(saved.First.SourceUrl, html.Output); Assert.Contains(saved.Second.SourceUrl, html.Output);
        Assert.Contains("2026-10-29", html.Output); Assert.Contains("1.4.2", html.Output); Assert.Contains("1.4.1", html.Output); Assert.Contains("rel=\"noopener noreferrer\"", html.Output);
        using Phase5Scope sam = h.Scope("sam"); await sam.StartAsync();
        Assert.Empty(library.Comparisons("sam")); Assert.Empty(library.Bookmarks("sam"));
        Assert.Null(sam.Session.Snapshot.Values["savedComparison"].GetString()); Assert.DoesNotContain("shield timing", (await sam.RenderAsync()).Output);
    }

    [Fact]
    public async Task ChangedArticleOrForgedOwner_CannotCommitAComparisonOrBookmark()
    {
        using Phase5Harness h = await Phase5Harness.CreateAsync("news"); using Phase5Scope scope = h.Scope(); await scope.StartAsync();
        scope.Select("articles", "ember-142"); h.Source.Articles = h.Source.Articles.Select(item => item.Id == "ember-142" ? item with { Tip = "Changed after selection" } : item).ToImmutableArray();
        Assert.Equal(ActionStatus.Conflict, (await scope.ActivateAsync("bookmark")).Status); Assert.Empty(h.Provider.GetRequiredService<NewsLibrary>().Bookmarks("alex"));
        var handler = scope.Services.GetRequiredService<BookmarkArticleAction>();
        Assert.Equal(ActionStatus.Rejected, (await handler.ExecuteAsync(new("sam", "main", Guid.NewGuid().ToString("N"), "ember-142", h.Source.Articles[0].Revision, ImmutableDictionary<string, JsonElement>.Empty), default)).Status);
        Assert.Empty(h.Provider.GetRequiredService<NewsLibrary>().Bookmarks("sam"));
    }

    [Theory]
    [InlineData("bad-url")]
    [InlineData("duplicate")]
    [InlineData("null")]
    [InlineData("too-many")]
    [InlineData("oversized")]
    public async Task ConfiguredNewsFeed_RejectsInvalidRecordsBeforeTheyEnterTheWorkspace(string failure)
    {
        var article = (await new FixtureNewsSource().ReadAsync())[0];
        IEnumerable<GameArticle> values = failure switch
        {
            "bad-url" => [article with { SourceUrl = "javascript:alert(1)" }],
            "duplicate" => [article, article],
            "null" => [null!],
            "too-many" => Enumerable.Range(0, 26).Select(i => article with { Id = "article-" + i }),
            _ => [article with { Summary = new string('x', 601) }]
        };
        Assert.Throws<InputRejectedException>(() => NewsJson.Validate(values));
    }

    [Fact]
    public async Task ConfiguredNewsFeed_ReadsTheExactEndpointAndEnforcesActualStreamBytesAndUnknownMembers()
    {
        GameArticle[] articles = [(await new FixtureNewsSource().ReadAsync())[0]];
        FeedHandler handler = new(JsonSerializer.Serialize(articles, NewsJson.Options));
        using HttpClient client = new(handler) { BaseAddress = new("https://feed.example.test/updates.json") }; HttpNewsSource source = new(client);
        Assert.Equal(articles, (await source.ReadAsync()).ToArray()); Assert.Equal(client.BaseAddress, handler.LastUri);
        handler.Json = "[" + new string(' ', 65536) + "]"; await Assert.ThrowsAsync<InputRejectedException>(async () => await source.ReadAsync());
        handler.Json = "[{\"unexpected\":true}]"; await Assert.ThrowsAsync<InputRejectedException>(async () => await source.ReadAsync());
        handler.Json = JsonSerializer.Serialize(articles, NewsJson.Options).Replace("\"id\":", "\"Id\":\"shadow\",\"id\":", StringComparison.Ordinal);
        await Assert.ThrowsAsync<InputRejectedException>(async () => await source.ReadAsync());
        using CancellationTokenSource cancelled = new(); cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await source.ReadAsync(cancelled.Token));
    }
    [Fact]
    public async Task PrivateNewsLibrary_EnforcesQuotaAndIdempotentRequestPayloads()
    {
        using Phase5Harness h = await Phase5Harness.CreateAsync("news"); NewsLibrary library = h.Provider.GetRequiredService<NewsLibrary>();
        GameArticle first = h.Source.Articles[0], second = h.Source.Articles[1]; string request = Guid.NewGuid().ToString("N");
        library.Save("alex", request, first, second); library.Save("alex", request, first, second); Assert.Single(library.Comparisons("alex"));
        Assert.Throws<InputRejectedException>(() => library.Save("alex", request, first, second with { Tip = "Different snapshot" }));
        for (int i = 1; i < 10; i++) library.Save("alex", Guid.NewGuid().ToString("N"), first, second);
        Assert.Equal(10, library.Comparisons("alex").Length);
        Assert.Throws<InputRejectedException>(() => library.Save("alex", Guid.NewGuid().ToString("N"), first, second));
        Assert.Empty(library.Comparisons("sam"));
    }
    private sealed class FeedHandler(string json) : HttpMessageHandler
    {
        public string Json { get; set; } = json;
        public Uri? LastUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); LastUri = request.RequestUri; return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new FeedContent(Encoding.UTF8.GetBytes(Json)) { Headers = { ContentType = new("application/json") } } }); }
    }
    private sealed class FeedContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes));
    }
}
