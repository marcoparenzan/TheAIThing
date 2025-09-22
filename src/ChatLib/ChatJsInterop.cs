using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ChatLib;

public sealed class ChatJsInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;
    private string? _moduleId;

    private string StaticPath => $"./_content/{nameof(ChatLib)}/";

    public ChatJsInterop(IJSRuntime jsRuntime)
    {
        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            $"{StaticPath}js/module.js"
        ).AsTask());
    }

    public async Task SetupAsync(string moduleId, object objRef, ElementReference canv, string? staticPath = null)
    {
        var module = await GetModuleAsync();
        _moduleId = moduleId;
        await module.InvokeVoidAsync("setup", moduleId, objRef, canv, staticPath ?? StaticPath);
    }

    public async Task StartAsync()
    {
        EnsureSetup();
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("start", _moduleId);
    }

    // Convenience overload using the module id set in SetupAsync
    public async Task ShowReportAsync(string accessToken, string embedUrl, string embedReportId)
    {
        EnsureSetup();
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("showReport", _moduleId, accessToken, embedUrl, embedReportId);
    }

    // Existing signature retained (explicit module id)
    public async Task ShowReportAsync(string moduleId, string accessToken, string embedUrl, string embedReportId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("showReport", moduleId, accessToken, embedUrl, embedReportId);
    }

    public async Task SetAsync<TValue>(string name, TValue value)
    {
        EnsureSetup();
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("set", _moduleId, name, value);
    }

    public async ValueTask DisposeAsync()
    {
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
