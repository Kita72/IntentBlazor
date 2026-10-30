using IntentBlazor.Runtime;
using Microsoft.AspNetCore.Components;

namespace IntentBlazor.Blazor;

public abstract class SessionComponent : ComponentBase, IAsyncDisposable
{
    [Inject] private AiSession DefaultSession { get; set; } = default!;
    [Parameter] public AiSession? Session { get; set; }
    [CascadingParameter] private Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState>? AuthenticationState { get; set; }
    protected AiSession Current { get; private set; } = default!;
    private AiSession? _subscribed;
    private bool _disposed;
    protected override async Task OnParametersSetAsync()
    { if (AuthenticationState is not null) await Current.ValidateOwnerAsync(); }

    protected override void OnParametersSet()
    {
        Current = Session ?? DefaultSession;
        if (ReferenceEquals(Current, _subscribed)) return;
        if (_subscribed is not null) _subscribed.Changed -= OnChanged;
        _subscribed = Current;
        _subscribed.Changed += OnChanged;
    }

    private async void OnChanged()
    {
        if (_disposed) return;
        try { await InvokeAsync(() => { if (!_disposed) StateHasChanged(); }); }
        catch (Exception error) { if (!_disposed) await DispatchExceptionAsync(error); }
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_subscribed is not null) _subscribed.Changed -= OnChanged;
        _subscribed = null;
        return ValueTask.CompletedTask;
    }
}
