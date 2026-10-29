using System.Collections.Immutable;
using IntentBlazor.Core;
using IntentBlazor.Runtime;

namespace IntentBlazor.Examples;

public sealed class ExampleCatalog
{
    public ImmutableDictionary<string, OutputContent> Fixtures { get; } =
        new[] { "news", "staff", "assets" }.ToImmutableDictionary(name => name, Load, StringComparer.Ordinal);

    public OutputContent Get(string name) => Fixtures.TryGetValue(name, out OutputContent? fixture)
        ? fixture : throw new InputRejectedException("Fixture mode accepts only: news, staff, or assets.");

    private static OutputContent Load(string name)
    {
        using Stream stream = typeof(ExampleCatalog).Assembly.GetManifestResourceStream($"IntentBlazor.Fixtures.{name}.json")
            ?? throw new InvalidOperationException($"Missing authored fixture: {name}.");
        using StreamReader reader = new(stream);
        return OutputJson.ReadContent(reader.ReadToEnd());
    }
}

public sealed class ExampleInputProcessor(ExampleCatalog catalog) : IInputProcessor
{
    public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(catalog.Get(input.Trim().ToLowerInvariant()));
    }
}
