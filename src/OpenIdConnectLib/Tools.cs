using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json;

namespace OpenIdConnectLib;

public static class Tools
{
    public static string ResolveRealm(this HttpContext http, string? realm)
    {
        if (!string.IsNullOrWhiteSpace(realm)) return realm;

        // Try cookie set at login
        if (http.Request.Cookies.TryGetValue("realm", out var r) && !string.IsNullOrWhiteSpace(r))
            return r;

        // Try route value or query
        //if (http.Request.RouteValues.TryGetValue("realm", out var rv) && rv is string rs && !string.IsNullOrWhiteSpace(rs))
        //    return rs;

        var q = http.Request.Query["realm"].ToString();
        if (!string.IsNullOrWhiteSpace(q)) return q;

        return "Landing";
    }

    public static string BuildAbsoluteReturnUrl(HttpRequest request, string path)
    {
        var scheme = request.Scheme;
        var host = request.Host.Value;
        return $"{scheme}://{host}{path}";
    }

    public static string Base64Url(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static async Task<OidcMetadata> GetTenantMetadataAsync(HttpClient client, IMemoryCache cache, string authority)
    {
        var cacheKey = "oidc.meta." + authority;
        if (cache.TryGetValue<OidcMetadata>(cacheKey, out var meta))
            return meta;

        var metaUrl = $"{authority}/.well-known/openid-configuration";
        using var resp = await client.GetAsync(metaUrl);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        meta = new OidcMetadata
        (
            Issuer: root.GetProperty("issuer").GetString()!,
            AuthorizationEndpoint: root.GetProperty("authorization_endpoint").GetString()!,
            TokenEndpoint: root.GetProperty("token_endpoint").GetString()!,
            UserInfoEndpoint: root.GetProperty("userinfo_endpoint").GetString()!,
            JwksUri: root.GetProperty("jwks_uri").GetString()!
        );

        cache.Set(cacheKey, meta, TimeSpan.FromMinutes(10));
        return meta;
    }

    public static async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(HttpClient client, IMemoryCache cache, string jwksUri)
    {
        var cacheKey = "oidc.jwks." + jwksUri;
        if (cache.TryGetValue<IEnumerable<SecurityKey>>(cacheKey, out var keys))
            return keys;

        using var resp = await client.GetAsync(jwksUri);
        resp.EnsureSuccessStatusCode();
        var jwksJson = await resp.Content.ReadAsStringAsync();
        var jwks = new JsonWebKeySet(jwksJson);
        keys = jwks.Keys.Select(k => (SecurityKey)k).ToArray();

        cache.Set(cacheKey, keys, TimeSpan.FromMinutes(30));
        return keys;
    }

    public static void AddKeycloakUserInfoClaims(ClaimsIdentity identity, JsonElement userInfo, string clientId)
    {
        if (userInfo.TryGetProperty("preferred_username", out var username))
        {
            identity.AddClaim(new Claim(ClaimTypes.Name, username.GetString() ?? username.ToString()));
        }

        if (userInfo.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String)
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, email.GetString()!));
        }

        if (userInfo.TryGetProperty("realm_access", out var realmAccess)
            && realmAccess.ValueKind == JsonValueKind.Object
            && realmAccess.TryGetProperty("roles", out var roles)
            && roles.ValueKind == JsonValueKind.Array)
        {
            foreach (var role in roles.EnumerateArray())
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role.GetString() ?? role.ToString()));
            }
        }

        if (userInfo.TryGetProperty("resource_access", out var resourceAccess)
            && resourceAccess.ValueKind == JsonValueKind.Object
            && resourceAccess.TryGetProperty(clientId, out var client)
            && client.ValueKind == JsonValueKind.Object
            && client.TryGetProperty("roles", out var clientRoles)
            && clientRoles.ValueKind == JsonValueKind.Array)
        {
            foreach (var role in clientRoles.EnumerateArray())
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role.GetString() ?? role.ToString()));
            }
        }
    }
}
