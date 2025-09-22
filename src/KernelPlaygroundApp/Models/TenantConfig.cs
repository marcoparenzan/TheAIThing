using KernelPlaygroundApp.Models.Keycloak;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace KernelPlaygroundApp.Models;

public class TenantConfig : ITenantConfig
{
    public static string AuthDbNameFor(IConfiguration config) => config["AuthDbName"] ?? "AuthDb";
    public static string RealmClientIdFor(IConfiguration config) => config["RealmClientId"] ?? "tenantapp";
    public static string RealmClaimNameFor(IConfiguration config) => config["RealmClaimName"] ?? "realm";

    public TenantConfig(IConfiguration config, AuthenticationStateProvider asp, IServiceProvider sp, IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
    {
        this.asp = asp;
        this.sp = sp;
        this.httpClientFactory = httpClientFactory;
        this.memoryCache = memoryCache;

        TenantContextName = AuthDbNameFor(config);
        RealmClientId = RealmClientIdFor(config);
        RealmClaimName = RealmClaimNameFor(config);
    }

    private AuthenticationStateProvider asp;
    private IServiceProvider sp;
    private IHttpClientFactory httpClientFactory;
    private IMemoryCache memoryCache;

    // temp
    private HttpClient httpClient;
    private AuthenticationState authenticationState;
    private ITenantContext tenantContext;

    public string TenantContextName { get; private set; }
    public string RealmClientId { get; private set; }
    public string RealmClaimName { get; private set; }

    public ITenantContext TenantContext
    {
        get
        {
            if (tenantContext is not null) return tenantContext;
            tenantContext = sp.GetKeyedService<ITenantContext>(TenantContextName);
            return tenantContext;
        }
    }

    public AuthenticationState AuthenticationState
    {
        get
        {
            if (authenticationState is not null) return authenticationState;
            authenticationState = asp.GetAuthenticationStateAsync().GetAwaiter().GetResult();
            return authenticationState;
        }
    }

    public string RealmName
    {
        get
        {
            var user = AuthenticationState.User;
            var realmClaim = user.Claims.Single(xx => xx.Type == RealmClaimName);
            var realmName = realmClaim.Value;
            return realmName;
        }
    }

    public FabricApp GetFabricApp()
    {
        var app = TenantContext.GetFabricApp(RealmName, RealmClientId);
        return app;
    }

    public HttpClient HttpClient
    {
        get
        {
            if (httpClient is not null) return httpClient;
            httpClient = httpClientFactory.CreateClient();
            return httpClient;
        }
    }

    public IMemoryCache MemoryCache
    {
        get
        {
            return memoryCache;
        }
    }
}
