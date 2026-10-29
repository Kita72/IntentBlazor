using IntentBlazor.AI;
using IntentBlazor.Core;

namespace IntentBlazor.Examples;

public sealed class NewsDataService(ExampleCatalog catalog)
{
    [AiDataTool("get_gaming_news", "Read the two synthetic gaming articles with source attribution, body, and ratings. Returns the articles dataset; no live news access.")]
    public ValueTask<OutputDataSet> GetArticlesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(catalog.Get("news").Data[0]);
    }
}
