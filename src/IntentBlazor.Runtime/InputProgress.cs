using IntentBlazor.Core;

namespace IntentBlazor.Runtime;

public sealed record InputProgress(string Stage, int ModelCalls, int ToolCalls, int Characters);

public interface IStreamingInputProcessor : IInputProcessor
{
    ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace,
        IProgress<InputProgress> progress, CancellationToken cancellationToken);
}
