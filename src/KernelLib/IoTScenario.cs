using AIAppLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System.Text.Json;
using ToolsLib;

namespace KernelLib;

public static class IoTScenario
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

        //list.Add(k.CreateFunctionFromMethod(datasetService.GetModelSchema, nameof(datasetService.GetModelSchema), "Elenco delle tabelle nello schema del report"));
        //list.Add(k.CreateFunctionFromMethod(datasetService.ExecuteDaxRows, nameof(datasetService.ExecuteDaxRows), "Esecuzione del codice dax"));
        //list.Add(k.CreateFunctionFromMethod(FileCsv, nameof(FileCsv), "Quando vuoi salvare un contenuto csv, bnasta specificare il nome"));

        void FileCsv(string name, string content) => File.WriteAllText(name, content);

        var kp = k.ImportPluginFromFunctions("Automazione", "Tutte le funzioni di automazione", list);
        return k;
    }

}
