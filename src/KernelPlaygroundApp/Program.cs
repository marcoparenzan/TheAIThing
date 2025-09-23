using ChatLib;
using KernelPlaygroundApp.Components;
using KernelPlaygroundApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using MudBlazor.Services;
using OpenAPILib;
using OpenIdClient;
using OpenIdConnectLib;
using OpenIdSimulator;
using PowerBIEmbeddingLib;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ToolsLib;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.None);
builder.Services.AddSingleton<HttpHandler>();
builder.Services.AddTransient<OpenApiContext>();

builder.Services.AddKeyedTransient("MyKernel", (Func<IServiceProvider, object?, Kernel>)((sp, key) =>
{
    //return KernelLib.IoTScenario.Build(sp);
    return KernelLib.PowerBIScenario.Build(sp);
}));

builder.Services.AddScoped<ITenantConfig, TenantConfig>();

// After builder is created:
builder.Services.AddOpenIdSimulator(builder.Configuration);

// Optionally configure Issuer/Audience in appsettings.json or here:
builder.Configuration["OpenIdSimulator:Issuer"] = builder.Configuration["OpenIdSimulator:Issuer"] ?? "http://localhost";
builder.Configuration["OpenIdSimulator:Audience"] = builder.Configuration["OpenIdSimulator:Audience"] ?? "blazor-client";

//builder.Services.AddKeyedScoped<ITenantContext, KeycloakContext>(TenantConfig.AuthDbNameFor(builder.Configuration), (sp, key) =>
//{
//    var optionsBuilder = new DbContextOptionsBuilder<KeycloakContext>();
//    var connStr = builder.Configuration.GetConnectionString($"{key}ConnectionString");
//    optionsBuilder.UseSqlServer(connStr);
//    return new KeycloakContext(optionsBuilder.Options);
//});

builder.Services.AddKeyedScoped<ITenantContext, JsonContext>(TenantConfig.AuthDbNameFor(builder.Configuration));

#region Identity Config (Manual OIDC + Cookie)

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

#endregion

builder.Services.AddTransient<PowerBIEmbeddingJsInterop>();
builder.Services.AddTransient<ChatJsInterop>();
builder.Services.AddMudServices();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddProblemDetails();

var app = builder.Build();

// Add BEFORE UseAuthentication and before anything that reads Scheme/Host
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.UseAuthentication();
app.UseAuthorization();

// Map the endpoints
app.MapOpenIdSimulatorEndpoints();
app.MapOpenIClientEndpoints();

app.Run();
