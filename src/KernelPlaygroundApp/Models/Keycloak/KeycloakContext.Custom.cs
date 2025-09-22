using AzureApplicationLib;

namespace KernelPlaygroundApp.Models.Keycloak;

public partial class KeycloakContext
{
    public RealmConfig GetRealmConfig(string realmName, string realmClientId)
    {
        var realmClient = this.RealmClients.SingleOrDefault(r => r.RealmName == realmName && r.RealmClientId == realmClientId);
        if (realmClient == null)
        {
            throw new Exception($"Realm '{realmName}' not found.");
        }

        var config = new RealmConfig
        {
            RealmId = realmClient.RealmId,
            RealmName = realmClient.RealmName,
            Authority = realmClient.Authority,
            RealmClientId = realmClient.RealmClientId,
            RealmClientSecret = realmClient.RealmClientSecret
        };
        return config;
    }

    public FabricApp GetFabricApp(string realmName, string realmClientId)
    {
        var realm = this.RealmClients.SingleOrDefault(xx => xx.RealmName == realmName && xx.RealmClientId == realmClientId);
        var fabricConfig = this.FabricConfigs.SingleOrDefault(r => r.RealmId == realm.RealmId && r.RealmClientId == realmClientId);
        if (fabricConfig == null)
        {
            throw new Exception($"Realm '{realmName}' not found.");
        }

        var config = new AzureApplicationConfig
        {
            ClientId = fabricConfig.ClientId,
            ClientSecret = fabricConfig.ClientSecret,
            Resource = fabricConfig.Resource,
            TenantId = fabricConfig.TenantId.ToString(),
            SubscriptionId = fabricConfig.SubscriptionId?.ToString(),
            Location = fabricConfig.Location,
            ClientName = fabricConfig.ClientName,
        };

        var app = new FabricApp(config);
        app.WorkspaceId = fabricConfig.WorkspaceId;
        return app;
    }
}
