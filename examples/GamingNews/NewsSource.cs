using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntentBlazor.Core;
using IntentBlazor.Runtime;

namespace IntentBlazor.GamingNews;

public sealed record GameArticle(string Id, string Game, string Title, string Source, string SourceUrl, DateTimeOffset PublishedUtc,
    string GameVersion, string Summary, string Tip)
{
    [JsonIgnore] public string Revision => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this, NewsJson.Options)))[..32];
}
public interface INewsSource { ValueTask<ImmutableArray<GameArticle>> ReadAsync(CancellationToken token = default); }
public static class NewsJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
    public static ImmutableArray<GameArticle> Validate(IEnumerable<GameArticle> input)
    {
        var articles = input.ToImmutableArray();
        if (articles.Length > 25 || articles.Any(item => item is null) || articles.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != articles.Length ||
            articles.Any(item => item is null || !Id(item.Id) || !Id(item.Game) || item.Game == "all" || !Text(item.Title, 120) || !Text(item.Source, 80) ||
                !SafeSource(item.SourceUrl) || !Text(item.GameVersion, 32) || !Text(item.Summary, 600) || !Text(item.Tip, 400) ||
                item.PublishedUtc.Year is < 2000 or > 2100 || JsonSerializer.SerializeToUtf8Bytes(item, Options).Length > 2048) || JsonSerializer.SerializeToUtf8Bytes(articles, Options).Length > 64 * 1024)
            throw new InputRejectedException("The configured news feed contains invalid, duplicate, or excessive articles.");
        return articles.OrderByDescending(item => item.PublishedUtc).ThenBy(item => item.Id, StringComparer.Ordinal).ToImmutableArray();
    }
    public static bool SafeSource(string? value) => value is { Length: > 0 and <= 512 } && Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && !value.Any(char.IsControl);
    private static bool Id(string? value) => value is { Length: > 0 and <= 64 } && char.IsAsciiLetter(value[0]) && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static bool Text(string? value, int maximum) => value is { Length: > 0 } && value.Length <= maximum && !value.Any(c => char.IsControl(c) && c is not '\n' and not '\t');
}
public sealed class FixtureNewsSource : INewsSource
{
    public ValueTask<ImmutableArray<GameArticle>> ReadAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(NewsJson.Validate([
            new("ember-142", "Emberfall", "Emberfall 1.4.2: shield timing", "Synthetic patch journal", "https://example.com/intentblazor/emberfall/1-4-2", new(2026,10,29,16,0,0,TimeSpan.Zero), "1.4.2", "The fictional update widens the shield timing window and revises stamina recovery.", "For version 1.4.2, test a shield build before spending stamina upgrades."),
            new("ember-141", "Emberfall", "Emberfall 1.4.1: resource routes", "Synthetic strategy journal", "https://example.com/intentblazor/emberfall/1-4-1", new(2026,10,22,16,0,0,TimeSpan.Zero), "1.4.1", "The fictional previous update changed mining routes and introduced a new outpost.", "This route applies to 1.4.1. Confirm resource locations after later patches."),
            new("sky-230", "SkyforgeTactics", "Skyforge Tactics 2.3: squad changes", "Synthetic tactics journal", "https://example.com/intentblazor/skyforge/2-3", new(2026,10,28,12,0,0,TimeSpan.Zero), "2.3", "Fictional support units receive stronger adjacency bonuses.", "On 2.3, keep a support unit adjacent to your defensive line."),
            new("sky-220", "SkyforgeTactics", "Skyforge Tactics 2.2: map rotation", "Synthetic map journal", "https://example.com/intentblazor/skyforge/2-2", new(2026,10,20,12,0,0,TimeSpan.Zero), "2.2", "The fictional map rotation favors narrow passages.", "For 2.2 maps, compare movement range before choosing heavy units.")
        ]));
    }
}
public sealed class HttpNewsSource(HttpClient client) : INewsSource
{
    public async ValueTask<ImmutableArray<GameArticle>> ReadAsync(CancellationToken token = default)
    {
        using var response = await client.GetAsync("", HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 64 * 1024 || response.Content.Headers.ContentType?.MediaType != "application/json")
            throw new InputRejectedException("The configured news source must return a bounded JSON feed.");
        await using Stream stream = await response.Content.ReadAsStreamAsync(token); using MemoryStream buffer = new();
        byte[] chunk = new byte[4096]; int count;
        while ((count = await stream.ReadAsync(chunk, token)) > 0)
        { if (buffer.Length + count > 64 * 1024) throw new InputRejectedException("The news feed exceeds its byte limit."); buffer.Write(chunk, 0, count); }
        try
        {
            byte[] bytes = buffer.ToArray(); using JsonDocument json = JsonDocument.Parse(bytes);
            if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.EnumerateArray().Any(row => row.ValueKind != JsonValueKind.Object ||
                row.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != row.EnumerateObject().Count())) throw new JsonException();
            return NewsJson.Validate(JsonSerializer.Deserialize<GameArticle[]>(bytes, NewsJson.Options) ?? throw new JsonException());
        }
        catch (JsonException) { throw new InputRejectedException("The configured news feed is not a supported article array."); }
    }
}
