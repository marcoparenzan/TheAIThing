
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using UnifiedNamespaceLib.Models;

namespace UnifiedNamespaceLib.Services;

public class InMemoryRetainedMessageService([ServiceKey] string serviceKey, IConfiguration config, IServiceProvider sp, ILogger<InMemoryRetainedMessageService> logger) : IRetainedMessageService
{
    private Dictionary<string, object> cache = new();

    public async Task AddAsync(string key, object value)
    {
        var ts = DateTimeOffset.Now;

        try
        {
            if (cache.ContainsKey(key))
            {
                cache[key] = value;
            }
            else
            {
                cache.Add(key, value);
            }
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
            
            var item = cache[key];
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
