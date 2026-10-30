using System.Collections.Immutable;
using System.Text.Json;
using IntentBlazor.Core;
using IntentBlazor.ExampleHost;
using IntentBlazor.Runtime;
using Microsoft.Data.Sqlite;

namespace IntentBlazor.GamingNews;

public sealed record SavedComparison(string Id, GameArticle First, GameArticle Second);
public sealed class NewsLibrary
{
    private readonly string _connection;
    public NewsLibrary(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS bookmarks(owner TEXT NOT NULL,id TEXT NOT NULL,article TEXT NOT NULL,PRIMARY KEY(owner,id));
            CREATE TABLE IF NOT EXISTS comparisons(owner TEXT NOT NULL,id TEXT NOT NULL,content TEXT NOT NULL,PRIMARY KEY(owner,id));
            CREATE TABLE IF NOT EXISTS news_receipts(owner TEXT NOT NULL,request TEXT NOT NULL,payload TEXT NOT NULL,PRIMARY KEY(owner,request));
            """; command.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var connection = new SqliteConnection(_connection); connection.Open(); return connection; }
    public ImmutableArray<GameArticle> Bookmarks(string owner) => Read<GameArticle>(owner, "SELECT article FROM bookmarks WHERE owner=$owner ORDER BY id LIMIT 25");
    public ImmutableArray<SavedComparison> Comparisons(string owner) => Read<SavedComparison>(owner, "SELECT content FROM comparisons WHERE owner=$owner ORDER BY id LIMIT 20");
    private ImmutableArray<T> Read<T>(string owner, string query)
    {
        using var connection = Open(); using var command = connection.CreateCommand(); command.CommandText = query; command.Parameters.AddWithValue("$owner", owner);
        using var reader = command.ExecuteReader(); var items = ImmutableArray.CreateBuilder<T>();
        while (reader.Read()) items.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), NewsJson.Options)!); return items.ToImmutable();
    }
    public void Save(string owner, string request, GameArticle first, GameArticle? second)
    {
        string payload = JsonSerializer.Serialize(new[] { first, second }, NewsJson.Options);
        using var connection = Open(); using var transaction = connection.BeginTransaction(); using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT payload FROM news_receipts WHERE owner=$owner AND request=$request";
        command.Parameters.AddWithValue("$owner", owner); command.Parameters.AddWithValue("$request", request);
        if (command.ExecuteScalar() is string prior)
        { if (prior != payload) throw new InputRejectedException("The news save request was reused with different content."); return; }
        string table = second is null ? "bookmarks" : "comparisons";
        command.Parameters.Clear(); command.CommandText = "SELECT COUNT(*) FROM " + table + " WHERE owner=$owner"; command.Parameters.AddWithValue("$owner", owner);
        if (Convert.ToInt64(command.ExecuteScalar()) >= 10) throw new InputRejectedException("The example library is full. Use a new private identity for another sample library.");
        string id = second is null ? first.Id : request;
        string content = second is null ? JsonSerializer.Serialize(first, NewsJson.Options) : JsonSerializer.Serialize(new SavedComparison(id, first, second), NewsJson.Options);
        command.Parameters.Clear(); command.CommandText = second is null ? "INSERT OR REPLACE INTO bookmarks VALUES($owner,$id,$content)" : "INSERT INTO comparisons VALUES($owner,$id,$content)";
        command.Parameters.AddWithValue("$owner", owner); command.Parameters.AddWithValue("$id", id); command.Parameters.AddWithValue("$content", content); command.ExecuteNonQuery();
        command.Parameters.Clear(); command.CommandText = "INSERT INTO news_receipts VALUES($owner,$request,$payload)";
        command.Parameters.AddWithValue("$owner", owner); command.Parameters.AddWithValue("$request", request); command.Parameters.AddWithValue("$payload", payload); command.ExecuteNonQuery(); transaction.Commit();
    }
    public static OutputDataSet Articles(IEnumerable<GameArticle> articles, string id = "articles") => ExampleData.TextSet(id, articles.Select(item => ExampleData.Row(
        ("id", item.Id), ("game", item.Game), ("title", item.Title), ("source", item.Source), ("sourceUrl", item.SourceUrl), ("published", item.PublishedUtc.ToString("u")),
        ("gameVersion", item.GameVersion), ("summary", item.Summary), ("tip", item.Tip), ("revision", item.Revision))),
        "id", "game", "title", "source", "sourceUrl", "published", "gameVersion", "summary", "tip", "revision");
    public OutputDataSet Saved(string owner) => ExampleData.TextSet("saved", Comparisons(owner).Select(item => ExampleData.Row(
        ("id", item.Id), ("title", item.First.Title + " / " + item.Second.Title),
        ("details", $"{item.First.Title} · {item.First.GameVersion} · {item.First.PublishedUtc:u}\n{item.First.Summary}\nTip: {item.First.Tip}\n\n{item.Second.Title} · {item.Second.GameVersion} · {item.Second.PublishedUtc:u}\n{item.Second.Summary}\nTip: {item.Second.Tip}"),
        ("sourceUrl", item.First.SourceUrl), ("secondUrl", item.Second.SourceUrl), ("source", item.First.Source), ("secondSource", item.Second.Source))),
        "id", "title", "details", "sourceUrl", "secondUrl", "source", "secondSource");
}
