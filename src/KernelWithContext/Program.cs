using AIAppLib;
using KernelWithContext;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OpenAPILib;
using System.Text.Json;
using ToolsLib;

var credentialsJson = File.ReadAllText(@"D:\Configurations\TheAIThing\AzureOpenAICredentials.json");
var credentials = JsonSerializer.Deserialize<AzureOpenAICredentials>(credentialsJson);

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.None);
builder.Services.AddSingleton<HttpHandler>();
builder.Services.AddTransient<OpenApiContext>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddKeyedTransient("MyKernel", (sp, key) =>
{
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
    var functions = new List<Functions>();
    res.Parse((url, pathKey, pathItem, operationType, operationInfo) =>
    {
        var f = new Functions(httpHandler, url, pathKey, pathItem, operationType, operationInfo); functions.Add(f);
        var kf = KernelFunctionFactory.CreateFromMethod(f.ExecuteAsync, pathKey.Replace('/', '_').Replace('{', '_').Replace('}', '_'), operationInfo.Description);

        list.Add(kf);
        Console.WriteLine($"Added function: {url}/{pathKey}");
    });
  
    var kp = k.ImportPluginFromFunctions("Domotica", "Tutte le funzioni domotiche", list);
    return k;
});

using IHost host = builder.Build();

await host.RunAsync();