using Microsoft.EntityFrameworkCore;

namespace KernelPlaygroundApp.Models;

public interface ITenantContext
{
    FabricApp GetFabricApp(string realmName, string realmClientId);
    RealmConfig GetRealmConfig(string realmName, string realmClientId);
}