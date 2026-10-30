using System.Collections.Immutable;
using System.Text.Json;
using IntentBlazor.Core;

namespace IntentBlazor.Runtime;

public sealed partial class AiSession
{
    public OperationResult DispatchExtension<TEvent>(string nodeId, long expectedRevision, TEvent message)
    {
        if (_disposed || _ownershipInvalidated || Document is null || IsBusy) return Fail("extension-unavailable", "The extension is unavailable while the workspace is busy or closed.");
        if (Revision != expectedRevision) return Fail("stale-revision", "This extension event belongs to an older workspace revision.");
        OutputNode? node = Document.Nodes.FirstOrDefault(item => item.Id == nodeId);
        if (node?.Extension is not { } extension || !IsVisiblePath(node) || !validator.TryExtension(extension, out IOutputExtensionContract? contract))
            return Fail("extension-unavailable", "The extension is unavailable.");
        try
        {
            JsonElement payload = JsonValues.From(message);
            if (payload.GetRawText().Length > 4096) return Fail("extension-limit", "The extension event exceeds its limit.");
            ImmutableArray<ExtensionEffect> effects = contract.Reduce(extension.Configuration, payload, new(_values));
            if (effects.IsDefault || effects.Length > 16 || effects.Any(item => item is null) || effects.Select(item => item.StateKey).Distinct().Count() != effects.Length)
                return Fail("extension-effects", "The extension returned invalid local effects.");
            var data = _content!.Data.ToDictionary(set => set.Id, StringComparer.Ordinal);
            var editable = contract.EditableStates(extension.Configuration);
            if (effects.Any(effect => effect is null || !editable.Contains(effect.StateKey) ||
                !Document.State.TryGetValue(effect.StateKey, out StateDefinition? state) || !DocumentValidator.Matches(state, effect.Value, data) ||
                (!effect.IsDraft && _dirty.Contains(effect.StateKey))))
                return Fail("extension-effects", "The extension cannot overwrite these state values.");
            var copied = effects.Select(effect => effect with { Value = effect.Value.Clone() }).ToImmutableArray();
            foreach (ExtensionEffect effect in copied)
            {
                _values = _values.SetItem(effect.StateKey, effect.Value.Clone());
                if (effect.IsDraft) _dirty = effect.Value.GetRawText() == _baseline[effect.StateKey].GetRawText() ? _dirty.Remove(effect.StateKey) : _dirty.Add(effect.StateKey);
                else _baseline = _baseline.SetItem(effect.StateKey, effect.Value.Clone());
            }
            Revision++; return Succeed();
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        { return Fail("extension-event", "The extension event was rejected. The current workspace is retained."); }
    }

    public ImmutableArray<OutputDataSet> Data => _content?.Data ?? [];
}
