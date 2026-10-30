using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntentBlazor.Core;

namespace IntentBlazor.Runtime;

public interface IWorkspaceIdentity { ValueTask<string?> GetOwnerIdAsync(CancellationToken cancellationToken); }
public sealed record ArtifactReference(string Id, string MediaType, string Version);
public sealed record WorkspaceAccess(bool Allowed, ImmutableArray<OutputDataSet> Data);
public interface IWorkspaceAccess
{
    ValueTask<WorkspaceAccess> ReauthorizeAsync(string ownerId, ImmutableArray<string> dataSets,
        ImmutableArray<ArtifactReference> artifacts, CancellationToken cancellationToken);
}

public sealed record ConversationTurn(long Sequence, string Input, string Outcome, string? OutputJson);
public sealed record ContextTurn(long Sequence, string Input, string Outcome, string? OutputExcerpt);
public sealed record ConversationContext(int OriginalCount, string Summary, ImmutableArray<ContextTurn> Recent);

public static class ConversationHistory
{
    public static ConversationContext Select(ImmutableArray<ConversationTurn> archive, int maximumTurns = 6, int maximumCharacters = 8000)
    {
        if (maximumTurns is < 0 or > 20 || maximumCharacters is < 256 or > 16000) throw new ArgumentOutOfRangeException(nameof(maximumTurns));
        List<ContextTurn> recent = [];
        int remaining = maximumCharacters;
        foreach (ConversationTurn turn in archive.Reverse().Take(maximumTurns))
        {
            string excerpt = turn.OutputJson is null ? "" : turn.OutputJson[..Math.Min(512, turn.OutputJson.Length)];
            if (turn.Input.Length + excerpt.Length > remaining) break;
            remaining -= turn.Input.Length + excerpt.Length;
            recent.Insert(0, new(turn.Sequence, turn.Input, turn.Outcome, excerpt));
        }
        long firstRecent = recent.Count == 0 ? long.MaxValue : recent[0].Sequence;
        string summary = string.Join('\n', archive.Where(turn => turn.Sequence < firstRecent).TakeLast(12)
            .Select(turn => $"#{turn.Sequence} [{turn.Outcome}] {turn.Input[..Math.Min(120, turn.Input.Length)]}"));
        return new(archive.Length, summary.Length == 0 ? "" : "Extractive history index (original records retained):\n" + summary, [.. recent]);
    }
}

public sealed record WorkspaceCheckpoint
{
    public string FormatVersion { get; init; } = "1";
    public OutputContent Content { get; init; } = new();
    public ImmutableDictionary<string, JsonElement> Values { get; init; } = ImmutableDictionary<string, JsonElement>.Empty;
    public ImmutableDictionary<string, JsonElement> Baseline { get; init; } = ImmutableDictionary<string, JsonElement>.Empty;
    public ImmutableDictionary<string, SortState> Sorting { get; init; } = ImmutableDictionary<string, SortState>.Empty;
    public ImmutableHashSet<string> DirtyStates { get; init; } = [];
    public ImmutableArray<ArtifactReference> Artifacts { get; init; } = [];
    public ImmutableArray<ConversationTurn> Archive { get; init; } = [];
}

public sealed record StoredWorkspace(long Version, string Json);
public sealed record ActionReceipt(string Fingerprint, ActionResult? Result);
public interface IWorkspaceStore
{
    ValueTask<StoredWorkspace?> LoadAsync(string ownerId, string key, CancellationToken cancellationToken = default);
    ValueTask<bool> SaveAsync(string ownerId, string key, long expectedVersion, string json, CancellationToken cancellationToken = default);
    ValueTask<bool> TryClaimActionAsync(string ownerId, string key, string requestId, string fingerprint, CancellationToken cancellationToken = default);
    ValueTask<ActionReceipt?> GetActionAsync(string ownerId, string key, string requestId, CancellationToken cancellationToken = default);
    ValueTask CompleteActionAsync(string ownerId, string key, string requestId, string fingerprint, ActionResult result, CancellationToken cancellationToken = default);
}

public static class WorkspaceJson
{
    public const int MaximumBytes = 4 * 1024 * 1024;
    public static JsonSerializerOptions Options { get; } = Create();
    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 48
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
    public static string Write<T>(T value)
    {
        string json = JsonSerializer.Serialize(value, Options);
        CheckSize(json);
        return json;
    }
    public static T Read<T>(string json)
    {
        CheckSize(json);
        using JsonDocument parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 48 });
        CheckDuplicates(parsed.RootElement);
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("A workspace value is required.");
    }
    private static void CheckDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw new JsonException("Duplicate workspace property."); CheckDuplicates(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) CheckDuplicates(item);
    }
    public static void CheckSize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new JsonException("The workspace exceeds its 4 MiB quota.");
    }
    public static void CheckAddress(string ownerId, string key)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || ownerId.Length > 256 || ownerId.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(key) || key.Length > 128 || !key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            throw new ArgumentException("A trusted owner and bounded workspace key are required.");
    }
}
