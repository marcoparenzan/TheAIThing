using Azure.Identity;
using Microsoft.Identity.Client;
using System.Text.Json.Serialization;

namespace AzureApplicationLib;

public partial class AzureApplicationConfig
{
    public string AdminConsentUrl => $"https://login.microsoftonline.com/{TenantId}/adminconsent?client_id={ClientId}";

    public static readonly AzureApplicationConfig Empty = new AzureApplicationConfig();

    public bool IsAdmin { get => this == Empty; }

    public string Location { get; set; }

    public string SubscriptionId { get; set; }


    public string TenantId { get; set; }

    public string ClientName { get; set; }
    public string ClientId { get; set; }

    public string ClientSecret { get; set; }
    public string ClientSecretName { get; set; }
    public DateTimeOffset? ClientSecretExpiration { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    string authority;

    public string Authority
    {
        get
        {
            return authority ?? $"https://login.microsoftonline.com/{TenantId}/v2.0";
        }
        set
        {
            authority = value;

        }
    }

    public string Resource { get; init; }

    string[] scopes;

    public string[] Scopes
    {
        get
        {
            return scopes ?? [$"{Resource}/.default"];
        }
        set
        {
            scopes = value;

        }
    }

    public ClientSecretCredential CreateClientSecretCredential()
    {
        var credentials = new ClientSecretCredential(
            TenantId,
            ClientId,
            ClientSecret
        );
        return credentials;
    }

    public async Task<AuthenticationResult> PublicAuthenticateAsync()
    {
        var app = PublicClientApplicationBuilder.Create(ClientId)
          .WithTenantId(TenantId)
          .WithAuthority(Authority)
          .Build();

        //var accessTokenRequestBuilder = app.AcquireTokenForClient(scopes);
        var accessTokenRequestBuilder = app.AcquireTokenByUsernamePassword(Scopes, Username, Password);
        var ar = await accessTokenRequestBuilder.ExecuteAsync();

        return ar;
    }

    public async Task<AuthenticationResult> ConfidentialAuthenticateAsync()
    {
        var app = ConfidentialClientApplicationBuilder.Create(ClientId)
          .WithTenantId(TenantId)
          .WithClientSecret(ClientSecret)
          .WithAuthority(Authority)
          .Build();

        var accessTokenRequestBuilder = app.AcquireTokenForClient(Scopes);
        var ar = await accessTokenRequestBuilder.ExecuteAsync();

        return ar;
    }
}