using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using UnifiedNamespaceLib.Models;

namespace UnifiedNamespaceLib.Services.Workers;

public class CacheService([ServiceKey] string serviceKey, IConfiguration config, IServiceProvider sp, ILogger<CacheService> logger) : IWorkerService
{
    private IMessagingService? messagingService;
    private ConcurrentDictionary<string, object> cache = new();

    public async Task ExecuteAsync()
    {
        var workerConfig = config.GetSection("workers").GetSection(serviceKey);
        var retainService = sp.GetKeyedService<IRetainedMessageService>(workerConfig[$"retainService"]);

        var cts = new CancellationTokenSource();

        try
        {
            this.messagingService = sp.GetKeyedService<IMessagingService>(workerConfig[$"messagingClient"]);
            messagingService.Handle(async msg =>
            {
                var payloadBytes = msg.Payload.ToArray();
                var payload = Encoding.UTF8.GetString(payloadBytes);
                await retainService.AddAsync(msg.Topic, payload);
            });
            await messagingService.ConnectAsync();
            await messagingService.SubscribeAsync("#");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing MQTT client: {ex.Message}");
            return;
        }
    }

    public async Task AddAsync(string key, object value)
    {
        var ts = DateTimeOffset.Now;

        try
        {
            _ = cache.GetOrAdd(key, value);
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<Dictionary<string, object>> GetAsync(params string[] keys)
    {
        var items = new Dictionary<string, object>();

        foreach (var key in keys)
        {
            var item = cache.GetOrAdd<MqttRetainedMessageModel>(key, (key, defaultValue) =>
            {
                return defaultValue ?? default;
            }, default);
            if (item is not null)
            {
                items.Add(key, item);
            }
        }

        return items;
    }

    public async Task<string[]> GetKeysAsync()
    {
        return cache.Keys.ToArray();
    }
}
