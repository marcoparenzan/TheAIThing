using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Models.Interfaces;
using ToolsLib;

namespace KernelWithContext;

public class Functions(HttpHandler httpHandler, string url, string pathKey, IOpenApiPathItem pathItem, OperationType operationType, OpenApiOperation operationInfo)
{
    public async Task<object> ExecuteAsync(string? what)
    {
        var fullPath = $"{url}{pathKey.Replace("{what}", Uri.EscapeDataString(what))}";
        return await httpHandler.GetAsync(fullPath);
    }
}
