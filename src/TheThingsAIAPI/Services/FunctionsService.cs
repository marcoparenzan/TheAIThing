using UnifiedNamespaceLib.Services;

namespace TheAIThingAPI.Services;

public class FunctionsService([FromKeyedServices("retain0")] IRetainedMessageService retain)
{
    internal async Task<string[]> ElencoDevicesAsync()
    {
        var allKeys = await retain.GetKeysAsync();
        return allKeys.Where(xx => xx.StartsWith("devices/") || xx.StartsWith("devices/")).ToArray();
    }

    internal async Task<object> ValoreDevice(string deviceId)
    {
        return await retain.GetAsync(deviceId);
    }
}
