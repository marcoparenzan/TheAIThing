using AIAppLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OpenAPILib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using ToolsLib;

namespace KernelLib;

public static class OpenAPIScenario
{
    public static Kernel Build(IServiceProvider sp)
    {
        var credentialsJson = File.ReadAllText(@"D:\Configurations\TheAIThing\AzureOpenAICredentials.json");
        var credentials = JsonSerializer.Deserialize<AzureOpenAICredentials>(credentialsJson);

        var kb =
            Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    credentials.DeploymentName,
                    credentials.Endpoint,
                    credentials.ApiKey
                );
        var k = kb.Build();
        var list = new List<KernelFunction>();

        var httpHandler = sp.GetService<HttpHandler>();
        var openApiContext = sp.GetService<OpenApiContext>();
        var res = openApiContext.GetAsync("https://localhost:7176/openapi/v1.json").Result; // Adjust URL as needed
        var functions = new List<OpenAPIFunctions>();
        res.Parse((url, pathKey, pathItem, operationType, operationInfo) =>
        {
            var f = new OpenAPIFunctions(httpHandler, url, pathKey, pathItem, operationType, operationInfo); functions.Add(f);
            var kf = KernelFunctionFactory.CreateFromMethod(f.ExecuteAsync, pathKey.Replace('/', '_').Replace('{', '_').Replace('}', '_'), operationInfo.Description);

            list.Add(kf);
            Console.WriteLine($"Added function: {url}/{pathKey}");
        });

        var kp = k.ImportPluginFromFunctions("Domotica", "Tutte le funzioni domotiche", list);

        return k;
    }

}
