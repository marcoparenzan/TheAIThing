
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnifiedNamespaceLib.Models;

namespace UnifiedNamespaceLib.Services;

public class InMemoryRetainedMessageService : IRetainedMessageService
{
    private Models.Database.ValueType[] valueTypes;
    private Models.Database.ValueType jsonValueType;
    private HybridCache hybridCache;

    public InMemoryRetainedMessageService(IConfiguration config, IServiceProvider sp, ILogger<EFRetainedMessageService> logger)
    {
        this.jsonValueType = new Models.Database.ValueType
        {
            Id = 1,
            Name = "Json"
        };
        this.valueTypes = [jsonValueType];
        this.hybridCache = sp.GetService<HybridCache>();
    }

    public async Task AddAsync(MqttRetainedMessageModel value)
    {
        var ts = DateTimeOffset.Now;

        try
        {
            await hybridCache.SetAsync(value.Topic, value);
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<MqttRetainedMessageModel[]> GetAsync(params string[] keys)
    {
        var items = new List<MqttRetainedMessageModel>();

        foreach (var key in keys)
        {
            var item = await hybridCache.GetOrCreateAsync<MqttRetainedMessageModel>(key, async ct =>
            {
                return default;
            });
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return items.ToArray();
    }
}
