using AzureApplicationLib;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace KernelPlaygroundApp.Models;

public partial class JsonContext : ITenantContext
{
    // Wired constants for the local OpenID simulator (keep in sync with your simulator config)
    private const string LocalIssuer = "http://localhost:5154"; // if you run on a specific port, e.g. http://localhost:5230, update here
    private const string LocalClientId = "blazor-client";
    private static readonly string[] LocalScopes = new[] { "openid", "profile", "email", "api", "offline_access" };
    private const string DevUsername = "admin";
    private const string DevPassword = "password";

    public RealmConfig GetRealmConfig(string realmName, string realmClientId)
    {
        // These constants must match the simulator's Issuer and the Blazor client id
        var config = new RealmConfig
        {
            RealmId = "local-dev",
            RealmName = string.IsNullOrWhiteSpace(realmName) ? "local" : realmName,
            Authority = LocalIssuer,
            RealmClientId = string.IsNullOrWhiteSpace(realmClientId) ? LocalClientId : realmClientId,
            RealmClientSecret = null // no client secret for the local simulator
        };
        return config;
    }

    public FabricApp GetFabricApp(string realmName, string realmClientId)
    {
        var json = File.ReadAllText(@"D:\Configurations\TheAIThing\FabricApp.json");
        var app = JsonSerializer.Deserialize<FabricApp>(json);
        return app;
    }
}