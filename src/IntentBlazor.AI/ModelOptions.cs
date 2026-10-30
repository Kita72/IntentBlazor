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
    public float? TopP { get; set; }
    public float? FrequencyPenalty { get; set; }
    public float? PresencePenalty { get; set; }
    public long? Seed { get; set; }
    public string? ReasoningEffort { get; set; }

    internal void Validate()
    {
        if (Timeout < TimeSpan.FromMilliseconds(1) || Timeout > TimeSpan.FromMinutes(10) ||
            MaximumModelCalls is < 1 or > 20 || MaximumToolCalls is < 1 or > 32 ||
            MaximumCorrections is < 0 or > 2 || MaximumContextCharacters is < 1024 or > 262144 ||
            MaximumOutputTokens is < 128 or > 32768 || MaximumReportedTokens is < 128 or > 1000000 ||
            Temperature is < 0 or > 2 || (Temperature.HasValue && !float.IsFinite(Temperature.Value)) ||
            TopP is < 0 or > 1 || FrequencyPenalty is < -2 or > 2 || PresencePenalty is < -2 or > 2 ||
            (TopP.HasValue && !float.IsFinite(TopP.Value)) || (FrequencyPenalty.HasValue && !float.IsFinite(FrequencyPenalty.Value)) ||
            (PresencePenalty.HasValue && !float.IsFinite(PresencePenalty.Value)) || ReasoningEffort is not (null or "low" or "medium" or "high"))
            throw new ArgumentException("Model limits are outside the supported bounds.");
    }
    public ModelOptions Copy() => (ModelOptions)MemberwiseClone();
}

public sealed record ModelProfile(string Provider, string Model, Uri? Endpoint, bool NativeTools = true,
    bool SupportsOutputTokenLimit = true, bool SupportsTemperature = true, bool IncrementalText = true,
    bool SupportsAdvancedSampling = true, bool SupportsReasoningEffort = false);

public sealed record ModelRunReport(string Status, string Provider, string Model, int ModelCalls,
    int ToolCalls, int Corrections, int Characters, long? InputTokens, long? OutputTokens, TimeSpan Elapsed, long? TotalTokens)
{
    public ImmutableArray<ModelToolObservation> Tools { get; init; } = [];
}
public sealed record ModelToolObservation(string Tool, string Outcome, string? DataSetId, int? Rows, TimeSpan Elapsed);

// Each call must return data authorized for the current application user.
public interface IOutputDataSource
{
    ValueTask<ImmutableArray<OutputDataSet>> LoadAsync(WorkspaceSnapshot workspace, CancellationToken cancellationToken);
}
