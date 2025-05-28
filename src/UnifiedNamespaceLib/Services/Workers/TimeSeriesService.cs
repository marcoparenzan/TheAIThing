using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UnifiedNamespaceLib.Services.Workers;

public class TimeSeriesService([ServiceKey] string serviceKey, IConfiguration config, IServiceProvider sp, ILogger<TimeSeriesService> logger) : IWorkerService
{
    private IMessagingService? messagingService;
    private Queue<(string deviceId, DateTimeOffset? timestamp, object value)> queue = new();

    public async Task ExecuteAsync()
    {
        var workerConfig = config.GetSection("workers").GetSection(serviceKey);

        var cts = new CancellationTokenSource();

        try
        {
            this.messagingService = sp.GetKeyedService<IMessagingService>(workerConfig[$"messagingClient"]);
            messagingService.Handle(async msg =>
            {
                queue.Enqueue((msg.Topic, DateTimeOffset.Now, msg.Payload));
                while (queue.Count > 60)
                {
                    queue.Dequeue();
                }
            });
            await messagingService.ConnectAsync();
            await messagingService.SubscribeAsync("devices/#");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing MQTT client: {ex.Message}");
            return;
        }
    }
}
