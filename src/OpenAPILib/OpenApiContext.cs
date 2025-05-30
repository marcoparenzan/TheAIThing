using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Models.Interfaces;
using Microsoft.OpenApi.Reader;
using System.Collections.Generic;
using ToolsLib;

namespace OpenAPILib;

public class OpenApiContext(HttpHandler httpHandler)
{
    public async Task<OpenApiContextResult> GetAsync(string url)
    {
        var stream = await httpHandler.OpenAsync(url);
        var reader = new OpenApiJsonReader();
        var result = await reader.ReadAsync(stream, new OpenApiReaderSettings
        {
            LeaveStreamOpen = false
        });
        var result1 = new OpenApiContextResult
        {
            openApiDocument = result.Document
        };
        return result1;
    }
}

public class OpenApiContextResult
{
    internal OpenApiDocument openApiDocument;

    public void Parse(Action<string, string, IOpenApiPathItem, OperationType, OpenApiOperation> handler)
    {
        var url = openApiDocument.Servers[0].Url.TrimEnd('/');

        foreach (var path in openApiDocument.Paths)
        {
            var pathEntry = path.Key;
            var pathItem = path.Value;
            if (pathItem.Operations.Count == 0) continue;
            foreach (var operation in pathItem.Operations)
            {
                handler(url, path.Key, path.Value, operation.Key, operation.Value);
            }
        }
    }
}
