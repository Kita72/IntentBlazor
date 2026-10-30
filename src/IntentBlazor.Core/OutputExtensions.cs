using System.Collections.Immutable;
using System.Text.Json;

namespace IntentBlazor.Core;

public sealed record ExtensionDescriptor(string Pack, string Version, string Renderer, string Description, JsonElement Example);
public sealed record ExtensionState(ImmutableDictionary<string, JsonElement> Values);
public sealed record ExtensionEffect(string StateKey, JsonElement Value, bool IsDraft = false);

// Contracts validate presentation and reduce local UI events. Application writes still use registered actions.
public interface IOutputExtensionContract
{
    ExtensionDescriptor Descriptor { get; }
    OperationResult Validate(JsonElement configuration, OutputContent content);
    ImmutableArray<string> EditableStates(JsonElement configuration);
    ImmutableArray<ExtensionEffect> Reduce(JsonElement configuration, JsonElement message, ExtensionState state);
}

public abstract class OutputExtensionContract<TConfiguration, TEvent> : IOutputExtensionContract
    where TConfiguration : class where TEvent : class
{
    public abstract ExtensionDescriptor Descriptor { get; }
    public abstract OperationResult Validate(TConfiguration configuration, OutputContent content);
    public virtual ImmutableArray<string> EditableStates(TConfiguration configuration) => [];
    public abstract ImmutableArray<ExtensionEffect> Reduce(TConfiguration configuration, TEvent message, ExtensionState state);
    public OperationResult Validate(JsonElement configuration, OutputContent content) => Validate(Read<TConfiguration>(configuration), content);
    public ImmutableArray<string> EditableStates(JsonElement configuration) => EditableStates(Read<TConfiguration>(configuration));
    public ImmutableArray<ExtensionEffect> Reduce(JsonElement configuration, JsonElement message, ExtensionState state) =>
        Reduce(Read<TConfiguration>(configuration), Read<TEvent>(message), state);
    public static T Read<T>(JsonElement value) where T : class =>
        value.Deserialize<T>(OutputJson.Options) ?? throw new JsonException("An extension value is required.");
}
