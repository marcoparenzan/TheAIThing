using KernelPlaygroundApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using OpenIdConnectLib;
using OpenIdSimulator;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenIdClient;

public static class OpenIdClientExtensions
{
    public static IEndpointRouteBuilder MapOpenIClientEndpoints(this IEndpointRouteBuilder app)
    {
        // Manual OIDC endpoints
        // GET /login or /login/{realm}
        // Starts OIDC auth code (PKCE) by redirecting to the realm's authorization endpoint.
        app.MapGet("/login/{realm?}", async (
            [FromServices] ITenantConfig gc,
            HttpContext http,
            string? realm = null) =>
        {
            var tenantContext = gc.TenantContext;
            var cfg = tenantContext.GetRealmConfig(realm, gc.RealmClientId);

            var resolvedRealm = http.ResolveRealm(realm);
            var meta = await Tools.GetTenantMetadataAsync(gc.HttpClient, gc.MemoryCache, cfg.Authority);

            var redirectUri = Tools.BuildAbsoluteReturnUrl(http.Request, "/signin-oidc");
            var state = Tools.Base64Url(RandomNumberGenerator.GetBytes(32));
            var nonce = Tools.Base64Url(RandomNumberGenerator.GetBytes(16));
            var codeVerifier = Tools.Base64Url(RandomNumberGenerator.GetBytes(64));
            var codeChallenge = Tools.Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

            gc.MemoryCache.Set("realmName", resolvedRealm);
            // cache transient state -> verifier/nonce/realm/redirectUri (5 min)
            gc.MemoryCache.Set("oidc.state." + state, new OidcAuthState(resolvedRealm, codeVerifier, nonce, redirectUri), TimeSpan.FromMinutes(5));

            // Persist realm hint in a cookie (helps with logout if user navigates away)
            http.Response.Cookies.Append(gc.RealmClaimName, resolvedRealm, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = http.Request.IsHttps, Expires = DateTimeOffset.UtcNow.AddMinutes(10) });

            var query = new QueryString()
                .Add("client_id", cfg.RealmClientId)
                .Add("redirect_uri", redirectUri)
                .Add("response_type", "code")
                .Add("scope", "openid profile email roles")
                .Add("state", state)
                .Add("nonce", nonce)
                .Add("code_challenge", codeChallenge)
                .Add("code_challenge_method", "S256");

            var authorizeUrl = $"{meta.AuthorizationEndpoint}{query}";
            return Results.Redirect(authorizeUrl);
        });

        // GET /signin-oidc
        // Handles OIDC callback; exchanges code, validates id_token, signs in cookie.
        app.MapGet("/signin-oidc", async (
            [FromServices] ITenantConfig gc,
            HttpContext http) =>
        {
            var request = http.Request;
            var code = request.Query["code"].ToString();
            var state = request.Query["state"].ToString();
            var error = request.Query["error"].ToString();
            if (!string.IsNullOrEmpty(error)) return Results.BadRequest("OIDC error: " + error);
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state)) return Results.BadRequest("Missing code/state.");

            if (!gc.MemoryCache.TryGetValue<OidcAuthState>("oidc.state." + state, out var authState))
                return Results.BadRequest("Invalid state.");
            gc.MemoryCache.Remove("oidc.state." + state);

            var kc = gc.TenantContext;
            var realmName = gc.MemoryCache.Get<string>("realmName");
            var cfg = kc.GetRealmConfig(realmName, gc.RealmClientId);

            var meta = await Tools.GetTenantMetadataAsync(gc.HttpClient, gc.MemoryCache, cfg.Authority);

            // Exchange code for tokens
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = cfg.RealmClientId,
                ["client_secret"] = cfg.RealmClientSecret,
                ["code"] = code,
                ["redirect_uri"] = authState.RedirectUri,
                ["code_verifier"] = authState.CodeVerifier
            };
            using var tokenResp = await gc.HttpClient.PostAsync(meta.TokenEndpoint, new FormUrlEncodedContent(form));
            if (!tokenResp.IsSuccessStatusCode)
            {
                var body = await tokenResp.Content.ReadAsStringAsync();
                return Results.BadRequest("Token endpoint error: " + body);
            }
            using var tokenDoc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync());
            var root = tokenDoc.RootElement;

            var idToken = root.TryGetProperty("id_token", out var idt) ? idt.GetString() : null;
            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            if (string.IsNullOrWhiteSpace(idToken)) return Results.BadRequest("Missing id_token.");

            // Validate ID token (issuer, audience, lifetime, signature)
            var keys = await Tools.GetSigningKeysAsync(gc.HttpClient, gc.MemoryCache, meta.JwksUri);
            var tokenHandler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = meta.Issuer,
                ValidateAudience = true,
                ValidAudience = cfg.RealmClientId,
                ValidateLifetime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys
            };

            ClaimsPrincipal principal;
            SecurityToken validatedToken;
            try
            {
                principal = tokenHandler.ValidateToken(idToken, parameters, out validatedToken);
            }
            catch (Exception ex)
            {
                return Results.BadRequest("Invalid id_token: " + ex.Message);
            }

            var identity = (ClaimsIdentity)principal.Identity!;
            // Attach realm
            identity.AddClaim(new Claim(gc.RealmClaimName, authState.Realm));

            // Optional: enrich from UserInfo (to get Keycloak roles if not in id_token)
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, meta.UserInfoEndpoint);
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    using var uiResp = await gc.HttpClient.SendAsync(req);
                    if (uiResp.IsSuccessStatusCode)
                    {
                        using var uiDoc = JsonDocument.Parse(await uiResp.Content.ReadAsStringAsync());
                        Tools.AddKeycloakUserInfoClaims(identity, uiDoc.RootElement, cfg.RealmClientId);
                    }
                }
                catch { /* best-effort enrichment */ }
            }

            // Persist id_token for logout:
            // 1) Add a compact session id and cache the id_token until token expiry.
            // 2) Also store id_token as a claim to survive app restarts (at the cost of cookie size).
            var jwt = (JwtSecurityToken)validatedToken;
            var expiresUtc = jwt.ValidTo; // UTC
            var ttl = expiresUtc > DateTime.UtcNow ? expiresUtc - DateTime.UtcNow : TimeSpan.FromHours(1);

            var sid = Tools.Base64Url(RandomNumberGenerator.GetBytes(32));
            identity.AddClaim(new Claim("sid", sid));
            identity.AddClaim(new Claim("id_token", idToken)); // fallback if cache is lost

            gc.MemoryCache.Set("oidc.idtoken." + sid, idToken, ttl);
            http.Response.Cookies.Append("sid", sid, new CookieOptions
            {
                HttpOnly = true,
                Secure = http.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = new DateTimeOffset(expiresUtc)
            });

            // Sign-in cookie
            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true, RedirectUri = "/" });

            return Results.Redirect("/");
        });

        // GET /logout or /logout/{realm}
        // Signs out cookie and redirects to realm end-session endpoint (Keycloak logout)
        app.MapGet("/logout/{realm?}", async (
            [FromServices] ITenantConfig gc,
            HttpContext http,
            string? realm) =>
        {
            var kc = gc.TenantContext;
            var cfg = kc.GetRealmConfig(realm, gc.RealmClientId);

            var resolvedRealm = http.ResolveRealm(realm);
            var postLogoutRedirect = Tools.BuildAbsoluteReturnUrl(http.Request, "/");

            // Retrieve id_token for id_token_hint
            string? sid = null;
            http.Request.Cookies.TryGetValue("sid", out sid);

            string? idToken = null;
            if (!string.IsNullOrEmpty(sid))
            {
                gc.MemoryCache.TryGetValue<string>("oidc.idtoken." + sid, out idToken);
            }
            // Fallback from claim if cache was missed (e.g., app recycle)
            idToken ??= http.User.FindFirst("id_token")?.Value;

            // Sign out local cookie
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Clean up local cookies/cache
            if (!string.IsNullOrEmpty(sid))
            {
                gc.MemoryCache.Remove("oidc.idtoken." + sid);
                http.Response.Cookies.Delete("sid");
            }
            http.Response.Cookies.Delete(gc.RealmClaimName);

            // Build end-session URL with id_token_hint (required by Keycloak) and client_id
            var endSession = $"{cfg.Authority}/protocol/openid-connect/logout?post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirect)}&client_id={Uri.EscapeDataString(cfg.RealmClientId)}";
            if (!string.IsNullOrEmpty(idToken))
            {
                endSession += $"&id_token_hint={Uri.EscapeDataString(idToken)}";
            }

            return Results.Redirect(endSession);
        });


        return app;
    }
}
