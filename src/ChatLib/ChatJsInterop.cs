using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ChatLib;

public sealed class ChatJsInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;
    private string? _moduleId;
    private DotNetObjectReference<ChatJsInterop>? dotNetRef;

    private string StaticPath => $"./_content/{nameof(ChatLib)}/";

    public ChatJsInterop(IJSRuntime jsRuntime)
    {
        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            $"{StaticPath}js/module.js"
        ).AsTask());
    }

    Action<string> action;

    public void Handle(Action<string> action)
    {
        this.action = action;
    }

    [JSInvokable]
    public void OnMessageReceived(string message)
    {
        this.action?.Invoke(message);
    }

    public async Task AddMessageAsync(string content, string sender)
    {
        EnsureSetup();
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("addMessage", _moduleId, content, sender);
    }

    public async Task SetupAsync(string moduleId, ElementReference canv, string? staticPath = null)
    {
        var module = await GetModuleAsync();
        _moduleId = moduleId;
        
        // Create a reference to this instance for JavaScript callbacks
        dotNetRef = DotNetObjectReference.Create(this);
        
        await module.InvokeVoidAsync("setup", moduleId, canv, staticPath ?? StaticPath, dotNetRef);
    }

    public async ValueTask DisposeAsync()
    {
        dotNetRef?.Dispose();
        
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }

    private Task<IJSObjectReference> GetModuleAsync() => _moduleTask.Value;

    private void EnsureSetup()
    {
        if (_moduleId is null)
            throw new InvalidOperationException("ChatJsInterop not initialized. Call SetupAsync(...) first.");
    }
}
