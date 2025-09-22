using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace KernelPlaygroundApp.Models;
public interface ITenantConfig
{
    AuthenticationState AuthenticationState { get; }
    HttpClient HttpClient { get; }
    IMemoryCache MemoryCache { get; }
    string RealmClaimName { get; }
    string RealmClientId { get; }
    string RealmName { get; }
    ITenantContext TenantContext { get; }

    FabricApp GetFabricApp();
}