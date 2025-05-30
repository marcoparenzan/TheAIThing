using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UnifiedNamespaceLib.Services.Workers;

public class WorkerServicesManager(IConfiguration config, IServiceProvider sp, ILogger<WorkerServicesManager> logger) : BackgroundService
{
    private List<Task> tasks = new();

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var serviceNames = config["WorkerServicesManager:workerNames"].Split(';');
        foreach (var serviceName in serviceNames)
        {
            var serviceInstance = sp.GetKeyedService<IWorkerService>(serviceName);
            var task = Task.Factory.StartNew(serviceInstance.ExecuteAsync);
            logger.LogInformation($"Started {serviceName} service");
            tasks.Add(task);
            await Task.Delay(2000);
        }
    }
}
