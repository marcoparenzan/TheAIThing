using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PowerBIEmbeddingLib;

public class PowerBIEmbeddingJsInterop : IAsyncDisposable
{
    Lazy<Task<IJSObjectReference>> moduleTask;
    string moduleId;

    string StaticPath => $"./_content/{nameof(PowerBIEmbeddingLib)}/";

    public PowerBIEmbeddingJsInterop(IJSRuntime jsRuntime)
    {
        moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            $"{StaticPath}js/module.js"
        ).AsTask());
    }

    public async Task SetupAsync(string moduleId, object objRef, ElementReference canv, string staticPath = null)
    {
        var module = await moduleTask.Value;
        this.moduleId = moduleId;
        await module.InvokeVoidAsync("setup", moduleId, objRef, canv, staticPath ?? StaticPath);
    }

    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }
    }

    public async Task StartAsync()
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("start", moduleId);
    }

    public async Task ShowReportAsync(string powerBiEmbeddingId, string accessToken, string embedUrl, string embedReportId)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("showReport", powerBiEmbeddingId, accessToken, embedUrl, embedReportId);
    }


    public async Task SetAsync<TValue>(string name, TValue value)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("set", moduleId, name, value);
    }

}
