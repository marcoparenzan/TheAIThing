using Microsoft.OpenApi.Models;
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

    public void Parse(Action<(string uri, string name, string description)> handler)
    {
        foreach (var path in openApiDocument.Paths)
        {
            var pathEntry = path.Key;
            var pathItem = path.Value;
            if (pathItem.Operations.Count == 0) continue;
            foreach (var operation in pathItem.Operations)
            {
                var method = operation.Key.ToString().ToUpperInvariant();
                var operationId = operation.Value.OperationId ?? $"{method}_{pathEntry.Replace("/", "_")}";
                var description = operation.Value.Description ?? "No description provided.";

                handler(($"{openApiDocument.Servers[0].Url.TrimEnd('/')}{pathEntry}", operationId, description));
            }
        }
    }
}
