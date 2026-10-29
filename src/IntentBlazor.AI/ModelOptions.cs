using System.Collections.Immutable;
using IntentBlazor.Core;
using IntentBlazor.Runtime;

namespace IntentBlazor.AI;

public sealed class ModelOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(90);
    public int MaximumModelCalls { get; set; } = 6;
    public int MaximumToolCalls { get; set; } = 8;
    public int MaximumCorrections { get; set; } = 1;
    public int MaximumContextCharacters { get; set; } = 60000;
    public int MaximumOutputTokens { get; set; } = 8000;
    public int MaximumReportedTokens { get; set; } = 32000;
    public float? Temperature { get; set; }
    public bool UseJsonSchema { get; set; } = true;

    internal void Validate()
    {
        if (Timeout < TimeSpan.FromMilliseconds(1) || Timeout > TimeSpan.FromMinutes(10) ||
            MaximumModelCalls is < 1 or > 20 || MaximumToolCalls is < 1 or > 32 ||
            MaximumCorrections is < 0 or > 2 || MaximumContextCharacters is < 1024 or > 262144 ||
            MaximumOutputTokens is < 128 or > 32768 || MaximumReportedTokens is < 128 or > 1000000 ||
            Temperature is < 0 or > 2 || (Temperature.HasValue && !float.IsFinite(Temperature.Value)))
            throw new ArgumentException("Model limits are outside the supported bounds.");
    }
}

public sealed record ModelProfile(string Provider, string Model, Uri? Endpoint, bool NativeTools = true,
    bool SupportsOutputTokenLimit = true, bool SupportsTemperature = true, bool IncrementalText = true);

public sealed record ModelRunReport(string Status, string Provider, string Model, int ModelCalls,
    int ToolCalls, int Corrections, int Characters, long? InputTokens, long? OutputTokens, TimeSpan Elapsed, long? TotalTokens);

// Each call must return data authorized for the current application user.
public interface IOutputDataSource
{
    ValueTask<ImmutableArray<OutputDataSet>> LoadAsync(WorkspaceSnapshot workspace, CancellationToken cancellationToken);
}
