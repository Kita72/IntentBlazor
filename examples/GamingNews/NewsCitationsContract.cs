using System.Collections.Immutable;
using IntentBlazor.Core;

namespace IntentBlazor.GamingNews;

public sealed record NewsCitationsConfiguration(string DataSetId, ImmutableArray<string> Selections);
public sealed class NewsCitationsContract : OutputExtensionContract<NewsCitationsConfiguration, object>
{
    public override ExtensionDescriptor Descriptor { get; } = new("news", "1.0", "citations", "Read-only HTTPS source/date/version citations from host-owned news or saved comparisons.", JsonValues.From(new NewsCitationsConfiguration("articles", ["selectedArticle"])));
    public override OperationResult Validate(NewsCitationsConfiguration configuration, OutputContent content) =>
        configuration.Selections is { IsDefault: false, Length: > 0 and <= 2 } && content.Data.Any(set => set.Id == configuration.DataSetId && set.Fields.ContainsKey("sourceUrl") && set.Fields.ContainsKey("source")) &&
        configuration.Selections.All(key => content.Document.State.TryGetValue(key, out var state) && state.Kind == StateKind.RecordReference && state.DataSetId == configuration.DataSetId)
            ? OperationResult.Ok : OperationResult.Fail("news-citations", "Select news records from the configured citation dataset.");
    public override ImmutableArray<string> EditableStates(NewsCitationsConfiguration configuration) => [];
    public override ImmutableArray<ExtensionEffect> Reduce(NewsCitationsConfiguration configuration, object message, ExtensionState state) => [];
}
