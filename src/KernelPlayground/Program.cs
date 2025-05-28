using AIAppLib;
using KernelPlayground;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Text.Json;
using ToolsLib;

var credentialsJson = File.ReadAllText(@"D:\Configurations\TheAIThing\AzureOpenAICredentials.json");
var credentials = JsonSerializer.Deserialize<AzureOpenAICredentials>(credentialsJson);

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.None);
builder.Services.AddSingleton<HttpHandler>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddKeyedTransient<Kernel>("MyKernel", (sp, key) =>
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
    var functions = new Functions(httpHandler);
    list.Add(k.CreateFunctionFromMethod(functions.ElencoDelleStanze, nameof(functions.ElencoDelleStanze), "Permette di avere una lista di stanze"));
    list.Add(k.CreateFunctionFromMethod(functions.LuciAcceseNellaStanza, nameof(functions.LuciAcceseNellaStanza), "Permette di identificare se ci sono Luci accese"));
    list.Add(k.CreateFunctionFromMethod(functions.Riassumi, nameof(functions.Riassumi), "Permette di riassumere i contenuti brevi"));
    list.Add(k.CreateFunctionFromMethod(functions.RunCode, nameof(functions.RunCode), "Permette di eseguire del codice"));
    list.Add(k.CreateFunctionFromMethod(functions.TagsOPCUA, nameof(functions.TagsOPCUA), "Permette di acquisire i tags OPC UA da un server"));
    list.Add(k.CreateFunctionFromMethod(functions.Schedule, nameof(functions.Schedule), "Permette di schedulare qualcosa"));
    list.Add(k.CreateFunctionFromMethod(functions.CercaInHttp, nameof(functions.CercaInHttp), "Permette di sognare ad occhi aperti"));
 
    var kp = k.ImportPluginFromFunctions("Domotica", "Tutte le funzioni domotiche", list);
    return k;
});

using IHost host = builder.Build();
await host.RunAsync();
