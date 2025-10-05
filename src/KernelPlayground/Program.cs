using AIAppLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OpenAPILib;
using System.Text.Json;
using ToolsLib;

var credentialsJson = File.ReadAllText(@"D:\Configurations\TheAIThing\AzureOpenAICredentials.json");
var credentials = JsonSerializer.Deserialize<AzureOpenAICredentials>(credentialsJson);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.None);
builder.Services.AddSingleton<HttpHandler>();
builder.Services.AddTransient<OpenApiContext>();

builder.Services.AddKeyedTransient("MyKernel", (Func<IServiceProvider, object?, Kernel>)((sp, key) =>
{
    return KernelLib.IoTScenario.Build(sp);
    //return KernelLib.OpenAPIScenario.Build(sp);
}));


builder.Services.AddHostedService<ConsoleWorker>();

using IHost host = builder.Build();
await host.RunAsync();
