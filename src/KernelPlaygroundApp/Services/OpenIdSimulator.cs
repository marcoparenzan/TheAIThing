using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace OpenIdSimulator;

public sealed class OpenIdOptions
{
    // Ensure this matches your app origin to avoid mismatches in redirects/validation.
    public string Issuer { get; set; } = "https://localhost:7172";
    // Must match the client_id used by your app.
    public string Audience { get; set; } = "tenantapp";
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}

internal sealed class RsaKeyService
{
    public RSA Rsa { get; }
    public string KeyId { get; }

    public RsaKeyService()
    {
        Rsa = RSA.Create(2048);
        KeyId = Guid.NewGuid().ToString("N");
    }

    public JsonWebKey GetJwk()
    {
        var parameters = Rsa.ExportParameters(false);
        return new JsonWebKey
        {
            Kty = "RSA",
            Kid = KeyId,
            Use = "sig",
            Alg = SecurityAlgorithms.RsaSha256,
            N = Base64UrlEncode(parameters.Modulus!),
            E = Base64UrlEncode(parameters.Exponent!)
        };
    }

    private static string Base64UrlEncode(byte[] input)
        => Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

internal sealed class ClientDefinition
{
    public required string ClientId { get; init; }
    public required HashSet<string> RedirectUris { get; init; }
    public string[] AllowedScopes { get; init; } = new[] { "openid", "profile", "email", "roles", "offline_access" };
}

internal static class InMemoryStores
{
    internal sealed record AuthCodeItem(
        string Code,
        string ClientId,
        string RedirectUri,
        string? CodeChallenge,
        string? CodeChallengeMethod,
        string Subject,
        string Scope,
        string? Nonce,
        DateTimeOffset ExpiresAt
    );

    public static readonly ConcurrentDictionary<string, AuthCodeItem> Codes = new();
    public static readonly Dictionary<string, ClientDefinition> Clients = new(StringComparer.Ordinal)
    {
        ["tenantapp"] = new ClientDefinition
        {
            ClientId = "tenantapp",
            RedirectUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "https://localhost:7172/signin-oidc"
            },
            AllowedScopes = new[] { "openid", "profile", "email", "roles" }
        }
    };
}

public static class OpenIdSimulatorExtensions
{
    public static IServiceCollection AddOpenIdSimulator(this IServiceCollection services, IConfiguration config)
    {
        var opts = new OpenIdOptions();
        config.GetSection("OpenIdSimulator").Bind(opts);

        services.AddSingleton(opts);
        services.AddSingleton<RsaKeyService>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;

                using var sp = services.BuildServiceProvider();
                var keySvc = sp.GetRequiredService<RsaKeyService>();
                var openId = sp.GetRequiredService<OpenIdOptions>();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(keySvc.Rsa) { KeyId = keySvc.KeyId },
                    ValidateIssuer = true,
                    ValidIssuer = openId.Issuer,
                    ValidateAudience = true,
                    ValidAudience = openId.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(5)
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static IEndpointRouteBuilder MapOpenIdSimulatorEndpoints(this IEndpointRouteBuilder app)
    {
        var openId = app.ServiceProvider.GetRequiredService<OpenIdOptions>();
        var rsa = app.ServiceProvider.GetRequiredService<RsaKeyService>();

        // Discovery
        app.MapGet("/.well-known/openid-configuration", (HttpContext ctx) =>
        {
            var issuer = openId.Issuer.TrimEnd('/');
            var discovery = new
            {
                issuer,
                authorization_endpoint = $"{issuer}/connect/authorize",
                token_endpoint = $"{issuer}/connect/token",
                userinfo_endpoint = $"{issuer}/userinfo",
                jwks_uri = $"{issuer}/.well-known/jwks.json",
                end_session_endpoint = $"{issuer}/protocol/openid-connect/logout",
                response_types_supported = new[] { "code", "token", "id_token" },
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { "RS256" },
                token_endpoint_auth_methods_supported = new[] { "none", "client_secret_basic" },
                scopes_supported = new[] { "openid", "profile", "email", "roles", "offline_access" },
                claims_supported = new[] { "sub", "name", "email" },
                code_challenge_methods_supported = new[] { "plain", "S256" }
            };

            return Results.Json(discovery);
        });

        // JWKS
        app.MapGet("/.well-known/jwks.json", (HttpContext ctx) =>
        {
            var jwk = rsa.GetJwk();
            var jwks = new
            {
                keys = new[] { new { kty = jwk.Kty, kid = jwk.Kid, use = jwk.Use, alg = jwk.Alg, n = jwk.N, e = jwk.E } }
            };
            return Results.Json(jwks);
        });

        // ===== Authorization endpoint (GET redirects to Blazor Login.razor, POST authenticates + issues code) =====

        static IResult LoginPage(HttpRequest req, string? error = null)
        {
            // Redirect to the Blazor page /login and pass the full authorize URL (+ optional error)
            var self = $"{req.Scheme}://{req.Host}{req.Path}{req.QueryString}";
            var qs = new QueryString().Add("authorize", self);
            if (!string.IsNullOrEmpty(error))
                qs = qs.Add("error", error);

            return Results.Redirect("/login" + qs);
        }

        app.MapMethods("/connect/authorize", new[] { "GET", "POST" }, async (HttpContext ctx) =>
        {
            var req = ctx.Request;
            var isPost = HttpMethods.IsPost(req.Method);

            // Parse OIDC params from query
            var q = req.Query;
            var clientId = q["client_id"].ToString();
            var redirectUri = q["redirect_uri"].ToString();
            var responseType = q["response_type"].ToString();
            var scope = q["scope"].ToString();
            var state = q["state"].ToString();
            var nonce = q["nonce"].ToString();
            var codeChallenge = q["code_challenge"].ToString();
            var codeChallengeMethod = q["code_challenge_method"].ToString();

            if (!string.Equals(responseType, "code", StringComparison.Ordinal))
                return Results.BadRequest(new { error = "unsupported_response_type" });

            if (!InMemoryStores.Clients.TryGetValue(clientId, out var client))
                return Results.BadRequest(new { error = "unauthorized_client" });

            if (!client.RedirectUris.Contains(redirectUri))
                return Results.BadRequest(new { error = "invalid_request", error_description = "redirect_uri not registered" });

            if (isPost)
            {
                if (!ctx.Request.HasFormContentType)
                    return LoginPage(req, "Invalid form");

                var form = await ctx.Request.ReadFormAsync();
                var username = form["username"].ToString();
                var password = form["password"].ToString();

                var valid = (username, password) switch
                {
                    ("admin", "password") => true,
                    ("user", "password") => true,
                    _ => false
                };
                if (!valid)
                    return LoginPage(req, "Invalid credentials");

                // Issue authorization code (10 min)
                var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_');

                var item = new InMemoryStores.AuthCodeItem(
                    Code: code,
                    ClientId: clientId,
                    RedirectUri: redirectUri,
                    CodeChallenge: string.IsNullOrWhiteSpace(codeChallenge) ? null : codeChallenge,
                    CodeChallengeMethod: string.IsNullOrWhiteSpace(codeChallengeMethod) ? null : codeChallengeMethod,
                    Subject: username,
                    Scope: string.IsNullOrWhiteSpace(scope) ? string.Join(' ', client.AllowedScopes) : scope,
                    Nonce: string.IsNullOrWhiteSpace(nonce) ? null : nonce,
                    ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10)
                );

                InMemoryStores.Codes[code] = item;

                var uri = new UriBuilder(redirectUri);
                var qs = new QueryString().Add("code", code);
                if (!string.IsNullOrEmpty(state)) qs = qs.Add("state", state);
                uri.Query = qs.Value?.TrimStart('?');
                return Results.Redirect(uri.ToString());
            }

            // GET -> use the Blazor Login page
            return LoginPage(req);
        });

        // ===== Token endpoint: supports password and authorization_code (with PKCE) =====
        app.MapPost("/connect/token", async (HttpContext ctx) =>
        {
            if (!ctx.Request.HasFormContentType)
                return Results.BadRequest(new { error = "invalid_request", error_description = "Expected form content type." });

            var form = await ctx.Request.ReadFormAsync();
            var grantType = form["grant_type"].ToString();

            if (string.Equals(grantType, "authorization_code", StringComparison.Ordinal))
            {
                var code = form["code"].ToString();
                var redirectUri = form["redirect_uri"].ToString();
                var clientId = form["client_id"].ToString();
                var codeVerifier = form["code_verifier"].ToString();

                if (!InMemoryStores.Codes.TryRemove(code, out var authCode))
                    return Results.BadRequest(new { error = "invalid_grant" });

                if (authCode.ExpiresAt < DateTimeOffset.UtcNow)
                    return Results.BadRequest(new { error = "invalid_grant", error_description = "code expired" });

                if (!string.Equals(authCode.ClientId, clientId, StringComparison.Ordinal))
                    return Results.BadRequest(new { error = "invalid_grant" });

                if (!string.Equals(authCode.RedirectUri, redirectUri, StringComparison.Ordinal))
                    return Results.BadRequest(new { error = "invalid_grant" });

                // Validate PKCE if present
                if (!string.IsNullOrEmpty(authCode.CodeChallenge))
                {
                    if (string.IsNullOrEmpty(codeVerifier))
                        return Results.BadRequest(new { error = "invalid_grant", error_description = "code_verifier required" });

                    var derived = authCode.CodeChallengeMethod?.ToUpperInvariant() == "S256"
                        ? Base64UrlSha256(codeVerifier)
                        : codeVerifier;

                    if (!string.Equals(derived, authCode.CodeChallenge, StringComparison.Ordinal))
                        return Results.BadRequest(new { error = "invalid_grant", error_description = "PKCE validation failed" });
                }

                var tokens = IssueTokens(openId, rsa, authCode.Subject, authCode.Scope, authCode.Nonce);
                return Results.Json(tokens);
            }

            if (!string.Equals(grantType, "password", StringComparison.Ordinal))
                return Results.BadRequest(new { error = "unsupported_grant_type" });

            // password grant (dev only)
            var username = form["username"].ToString();
            var password = form["password"].ToString();
            var scopePwd = form["scope"].ToString();
            var validPwd = (username, password) switch
            {
                ("admin", "password") => true,
                ("user", "password") => true,
                _ => false
            };

            if (!validPwd)
                return Results.Unauthorized();

            var tokensPwd = IssueTokens(openId, rsa, username, string.IsNullOrWhiteSpace(scopePwd) ? "openid profile email roles" : scopePwd, nonce: null);
            return Results.Json(tokensPwd);
        });

        // UserInfo
        app.MapGet("/userinfo", [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] (ClaimsPrincipal user) =>
        {
            var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.Identity?.Name ?? "unknown";
            var name = user.FindFirstValue(JwtRegisteredClaimNames.Name) ?? sub;
            var email = user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? $"{sub}@local";

            return Results.Json(new { sub, name, email });
        });

        // RP-initiated logout (Keycloak-style path) + alias
        app.MapMethods("/protocol/openid-connect/logout", new[] { "GET", "POST" }, LogoutHandler(openId, rsa));
        app.MapMethods("/connect/logout", new[] { "GET", "POST" }, LogoutHandler(openId, rsa));

        return app;

        static string Base64UrlSha256(string input)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        static object IssueTokens(OpenIdOptions openId, RsaKeyService rsa, string subject, string scope, string? nonce)
        {
            var now = DateTimeOffset.UtcNow;
            var baseClaims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, subject),
                new(JwtRegisteredClaimNames.UniqueName, subject),
                new(JwtRegisteredClaimNames.Name, subject == "admin" ? "Administrator" : "Sample User"),
                new(JwtRegisteredClaimNames.Email, $"{subject}@local")
            };
            if (!string.IsNullOrWhiteSpace(nonce))
                baseClaims.Add(new Claim(JwtRegisteredClaimNames.Nonce, nonce));

            var creds = new SigningCredentials(new RsaSecurityKey(rsa.Rsa) { KeyId = rsa.KeyId }, SecurityAlgorithms.RsaSha256);

            string CreateJwt(string aud, IEnumerable<Claim> extraClaims)
            {
                var jwt = new JwtSecurityToken(
                    issuer: openId.Issuer,
                    audience: aud,
                    claims: baseClaims.Concat(extraClaims),
                    notBefore: now.UtcDateTime,
                    expires: now.AddMinutes(openId.AccessTokenLifetimeMinutes).UtcDateTime,
                    signingCredentials: creds);
                return new JwtSecurityTokenHandler().WriteToken(jwt);
            }

            var accessClaims = new[] { new Claim("scope", scope) };
            var accessToken = CreateJwt(openId.Audience, accessClaims);

            var idClaims = new[] { new Claim("auth_time", now.ToUnixTimeSeconds().ToString()) };
            var idToken = CreateJwt(openId.Audience, idClaims);

            return new
            {
                access_token = accessToken,
                token_type = "Bearer",
                expires_in = (int)TimeSpan.FromMinutes(openId.AccessTokenLifetimeMinutes).TotalSeconds,
                scope = scope,
                id_token = idToken
            };
        }
    }

    private static Func<HttpContext, Task<IResult>> LogoutHandler(OpenIdOptions openId, RsaKeyService rsa)
        => async (HttpContext ctx) =>
        {
            var postLogoutRedirectUri = ctx.Request.Method == "POST"
                ? (await ctx.Request.ReadFormAsync())["post_logout_redirect_uri"].ToString()
                : ctx.Request.Query["post_logout_redirect_uri"].ToString();

            var idTokenHint = ctx.Request.Method == "POST"
                ? (await ctx.Request.ReadFormAsync())["id_token_hint"].ToString()
                : ctx.Request.Query["id_token_hint"].ToString();

            var clientId = ctx.Request.Method == "POST"
                ? (await ctx.Request.ReadFormAsync())["client_id"].ToString()
                : ctx.Request.Query["client_id"].ToString();

            if (!string.IsNullOrWhiteSpace(idTokenHint))
            {
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var tvp = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new RsaSecurityKey(rsa.Rsa) { KeyId = rsa.KeyId },
                        ValidateIssuer = true,
                        ValidIssuer = openId.Issuer,
                        ValidateAudience = true,
                        ValidAudience = string.IsNullOrWhiteSpace(clientId) ? openId.Audience : clientId,
                        ValidateLifetime = false
                    };
                    handler.ValidateToken(idTokenHint, tvp, out _);
                }
                catch { /* ignore in simulator */ }
            }

            ctx.Response.Cookies.Delete("oidc_sim_user");

            var fallback = "/";
            var destination = SafeRedirect(postLogoutRedirectUri, openId) ?? fallback;

            return Results.Redirect(destination);
        };

    private static string? SafeRedirect(string? uri, OpenIdOptions opts)
    {
        if (string.IsNullOrWhiteSpace(uri)) return null;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var target)) return null;
        if (!Uri.TryCreate(opts.Issuer, UriKind.Absolute, out var issuer)) return null;

        return string.Equals(target.Scheme, issuer.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(target.Authority, issuer.Authority, StringComparison.OrdinalIgnoreCase)
            ? uri
            : null;
    }
}