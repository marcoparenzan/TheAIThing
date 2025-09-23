using AIAppLib;
using AnalysisServicesLib;
using AzureApplicationLib;
using FabricLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerBI.Api;
using Microsoft.SemanticKernel;
using System.Text.Json;
using ToolsLib;

namespace KernelLib;

public static class PowerBIScenario
{
    private static DatasetService datasetService;

    public static void Ensure()
    {
        if (datasetService is not null) return; 

        var configJson = File.ReadAllText(@"D:\Configurations\TheAIThing\PowerBIApplicationConfig.json");
        var config = JsonSerializer.Deserialize<AzureApplicationConfig>(configJson);
        var app = new PowerBiApplication(config);

        var workspaceInfoJson = File.ReadAllText(@"D:\Configurations\TheAIThing\TheAIThing-Dev.json");
        var workspaceInfo = JsonSerializer.Deserialize<WorkspaceConfig>(workspaceInfoJson);

        app.ConfidentialAuthenticateAsync().Wait();

        var workspaceId = Guid.Parse(workspaceInfo.Id);
        var reportItems = app.PowerBiClient.Reports.GetReportsInGroup(workspaceId);
        var selectedReportItem = reportItems.Value[1];

        var dataset = app.PowerBiClient.Datasets.GetDatasetInGroup(workspaceId, selectedReportItem.DatasetId);

        var reportEmbedToken = app.GetReportEmbedTokenInfo(workspaceId, selectedReportItem.Id).Result;

        var accessToken = new Microsoft.AnalysisServices.AccessToken(app.AuthenticationResult.AccessToken, app.AuthenticationResult.ExpiresOn);
        datasetService = new DatasetService(workspaceInfo.Uri, accessToken, selectedReportItem.DatasetId, dataset.Name);
    }

    public static ModelSchema GetModelSchema()
    {
        Ensure();
        return datasetService.GetModelSchema();
    }

    public static IReadOnlyList<Dictionary<string, object?>> ExecuteDaxRows(string dax)
    {
        Ensure();
        return datasetService.ExecuteDaxRows(dax);
    }

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

        var functions = new Functions(httpHandler);

        list.Add(k.CreateFunctionFromMethod(GetModelSchema, nameof(GetModelSchema), "Elenco delle tabelle nello schema del report"));
        list.Add(k.CreateFunctionFromMethod(ExecuteDaxRows, nameof(ExecuteDaxRows), "Esecuzione del codice dax"));
        list.Add(k.CreateFunctionFromMethod(FileCsv, nameof(FileCsv), "Quando vuoi salvare un contenuto csv, bnasta specificare il nome"));

        void FileCsv(string name, string content) => File.WriteAllText(name, content);

        var kp = k.ImportPluginFromFunctions("Automazione", "Tutte le funzioni di automazione", list);
        return k;
    }

}
