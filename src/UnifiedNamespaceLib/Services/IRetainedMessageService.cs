using UnifiedNamespaceLib.Models;

namespace UnifiedNamespaceLib.Services;
 
public interface IRetainedMessageService: IService
{
    Task AddAsync(string key, object value);
    Task<Dictionary<string, object>> GetAsync(params string[] keys);
    Task<string[]> GetKeysAsync();
}
