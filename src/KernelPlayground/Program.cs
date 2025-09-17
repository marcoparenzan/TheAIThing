using AIAppLib;
using AzureApplicationLib;
using FabricLib;
using KernelPlayground;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.PowerBI.Api;
using Microsoft.SemanticKernel;
using System.Text.Json;
using ToolsLib;
using AnalysisServicesLib;
using Azure.Core;

var credentialsJson = File.ReadAllText(@"D:\Configurations\TheAIThing\AzureOpenAICredentials.json");
var credentials = JsonSerializer.Deserialize<AzureOpenAICredentials>(credentialsJson);

var fabricAppConfigJson = File.ReadAllText(@"D:\Configurations\TheAIThing\FabricApp.json");
var fabricAppConfig = JsonSerializer.Deserialize<AzureApplicationConfig>(fabricAppConfigJson);
var fabricApp = new PowerBiApplication(fabricAppConfig);

var workspaceInfoJson = File.ReadAllText(@"D:\Configurations\TheAIThing\TheAIThing-Dev.json");
var workspaceInfo = JsonSerializer.Deserialize<WorkspaceConfig>(workspaceInfoJson);

await fabricApp.ConfidentialAuthenticateAsync();

var workspaceId = Guid.Parse(workspaceInfo.Id);
var reportItems = await fabricApp.PowerBiClient.Reports.GetReportsInGroupAsync(workspaceId);
var selectedReportItem = reportItems.Value[0];

var dataset = await fabricApp.PowerBiClient.Datasets.GetDatasetInGroupAsync(workspaceId, selectedReportItem.DatasetId);

var reportEmbedToken = await fabricApp.GetReportEmbedTokenInfo(workspaceId, selectedReportItem.Id);

var accessToken = new Microsoft.AnalysisServices.AccessToken(fabricApp.AuthenticationResult.AccessToken, fabricApp.AuthenticationResult.ExpiresOn);
var datasetService = new DatasetService(workspaceInfo.Uri, accessToken, selectedReportItem.DatasetId, dataset.Name);

// workspaceInfo.Uri must be the XMLA endpoint, e.g. powerbi://api.powerbi.com/v1.0/myorg/YourWorkspace
var schema = datasetService.GetModelSchema();

//// Print schema (tables, columns, measures)
//Console.WriteLine(JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true }));

//// Example DAX: list all measures via DMV
//var dax = @"EVALUATE SELECTCOLUMNS(TMSCHEMA_MEASURES, ""TableName"", [TableName], ""Name"", [Name], ""Expression"", [Expression])";
//var daxResult = datasetService.ExecuteDax(dax);
//Console.WriteLine($"DAX result rows: {daxResult.Rows.Count}");

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.None);
builder.Services.AddSingleton<HttpHandler>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddKeyedTransient("MyKernel", (Func<IServiceProvider, object?, Kernel>)((sp, key) =>
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

    //list.Add(k.CreateFunctionFromMethod(functions.CercaInHttp, nameof(functions.CercaInHttp), "Permette di sognare ad occhi aperti"));
    //list.Add(k.CreateFunctionFromMethod((Delegate)functions.StanzeDiUnaCasa, nameof(functions.StanzeDiUnaCasa), "Permette di avere una lista di stanze"));
    //list.Add(k.CreateFunctionFromMethod(functions.LuciAcceseNellaStanza, nameof(functions.LuciAcceseNellaStanza), "Permette di identificare se ci sono Luci accese"));
    //list.Add(k.CreateFunctionFromMethod(functions.CondizionatoreAcceso, nameof(functions.CondizionatoreAcceso), "Lo stato dei condizionatori"));
    //list.Add(k.CreateFunctionFromMethod(functions.ToggleCondizionamento, nameof(functions.ToggleCondizionamento), "Permette di accendere e spegnere il condizionatore"));
    //list.Add(k.CreateFunctionFromMethod(functions.InviaViaEMail, nameof(functions.InviaViaEMail), "permette di mandare qualcosa via email"));

    //list.Add(k.CreateFunctionFromMethod(functions.Riassumi, nameof(functions.Riassumi), "Permette di riassumere i contenuti brevi"));
    //list.Add(k.CreateFunctionFromMethod(functions.RunCode, nameof(functions.RunCode), "Permette di eseguire del codice"));
    //list.Add(k.CreateFunctionFromMethod(functions.TagsOPCUA, nameof(functions.TagsOPCUA), "Permette di acquisire i tags OPC UA da un server"));
    //list.Add(k.CreateFunctionFromMethod(functions.Schedule, nameof(functions.Schedule), "Permette di schedulare qualcosa"));

    list.Add(k.CreateFunctionFromMethod(datasetService.GetModelSchema, nameof(datasetService.GetModelSchema), "Elenco delle tabelle nello schema del report"));
    list.Add(k.CreateFunctionFromMethod(datasetService.ExecuteDaxRows, nameof(datasetService.ExecuteDaxRows), "Esecuzione del codice dax"));


    var kp = k.ImportPluginFromFunctions("Automazione", "Tutte le funzioni di automazione", list);
    return k;
}));

using IHost host = builder.Build();
await host.RunAsync();
