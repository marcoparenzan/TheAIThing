using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace UnifiedNamespaceLib.Services.Workers;

public class TimerServices([ServiceKey] string serviceKey, IConfiguration config, IServiceProvider sp, ILogger<TimerServices> logger) : IWorkerService
{
    private IMessagingService? messaging;

    public async Task ExecuteAsync()
    {
        var workerConfig = config.GetSection("workers").GetSection(serviceKey);

        this.messaging = sp.GetKeyedService<IMessagingService>(workerConfig[$"messagingClient"]);
        await messaging.ConnectAsync();

        var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await periodicTimer.WaitForNextTickAsync())
        {
            var now = DateTimeOffset.Now;
            var body = new
            {
                now
            };
            await PublishAsync($"timers/1s", body);

            if (now.Second % 5 == 0)
            {
                await PublishAsync($"timers/5s", body);
                if (now.Second % 15 == 0)
                {
                    await PublishAsync($"timers/15s", body);
                }
                if (now.Second == 0)
                {
                    await PublishAsync($"timers/1m", body);
                    if (now.Minute == 0)
                    {
                        await PublishAsync($"timers/1h", body);
                    }
                }
            }
        }
    }

    async Task PublishAsync<TMessage>(string topic, TMessage message)
    {
        try
        {
            await messaging.PublishAsync(topic, message, retainFlag: false);
            Console.WriteLine($"{topic}: {JsonSerializer.Serialize(message)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR {serviceKey}: {ex.Message}");
        }
    }
}
