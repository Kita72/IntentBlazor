using System.Collections.Immutable;
using IntentBlazor.Core;

namespace IntentBlazor.Quickstart;

public sealed record ScoreConfiguration(string StateKey);
public sealed record ScoreEvent(int Delta);

public sealed class ScoreContract : OutputExtensionContract<ScoreConfiguration, ScoreEvent>
{
    public override ExtensionDescriptor Descriptor { get; } = new("sample", "1.0", "score",
        "A local score from 0 to 10 with accessible increment and decrement controls.", JsonValues.From(new ScoreConfiguration("score")));

    public override OperationResult Validate(ScoreConfiguration configuration, OutputContent content) =>
        content.Document.State.TryGetValue(configuration.StateKey, out StateDefinition? state) &&
        state.Kind == StateKind.Number && !state.Nullable && state.Minimum == 0 && state.Maximum == 10
            ? OperationResult.Ok : OperationResult.Fail("score-state", "Use a non-null numeric state with minimum 0 and maximum 10.");

    public override ImmutableArray<string> EditableStates(ScoreConfiguration configuration) => [configuration.StateKey];

    public override ImmutableArray<ExtensionEffect> Reduce(ScoreConfiguration configuration, ScoreEvent message, ExtensionState state)
    {
        if (message.Delta is not (-1 or 1)) throw new ArgumentException("Use a step of one.");
        decimal current = state.Values[configuration.StateKey].GetDecimal();
        return [new(configuration.StateKey, JsonValues.From(Math.Clamp(current + message.Delta, 0, 10)), IsDraft: true)];
    }
}
