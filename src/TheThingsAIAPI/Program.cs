using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using System.ComponentModel;
using TheAIThingAPI.Components;
using TheAIThingAPI.Services;
using UnifiedNamespaceLib.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHybridCache();
builder.Services.AddKeyedSingleton<UnifiedNamespaceLib.Services.IRetainedMessageService, UnifiedNamespaceLib.Services.InMemoryRetainedMessageService>("retain0");
builder.Services.AddKeyedTransient<UnifiedNamespaceLib.Services.IWorkerService, TheAIThingAPI.Services.OpcUAService>("opcua1");
builder.Services.AddKeyedTransient<UnifiedNamespaceLib.Services.IWorkerService, UnifiedNamespaceLib.Services.Workers.MqttBrokerService>("mqttbroker");
builder.Services.AddKeyedTransient<UnifiedNamespaceLib.Services.IWorkerService, UnifiedNamespaceLib.Services.Workers.TimerServices>("timers");
builder.Services.AddKeyedTransient<UnifiedNamespaceLib.Services.IWorkerService, UnifiedNamespaceLib.Services.Workers.TimeSeriesService>("timeseries");
builder.Services.AddKeyedTransient<UnifiedNamespaceLib.Services.IMessagingService, UnifiedNamespaceLib.Services.MqttClientService>("mqttclient-opcua1");
builder.Services.AddKeyedTransient<UnifiedNamespaceLib.Services.IMessagingService, UnifiedNamespaceLib.Services.MqttClientService>("mqttclient-timers");
builder.Services.AddKeyedTransient<UnifiedNamespaceLib.Services.IMessagingService, UnifiedNamespaceLib.Services.MqttClientService>("mqttclient-timeseries");
builder.Services.AddSingleton<ToolsLib.HttpHandler>();

builder.Services.AddHostedService<WorkerServicesManager>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add CORS services
builder.Services.AddCors();

// Add services to the container.
builder.Services.AddValidation();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure CORS middleware
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseHsts();
app.MapOpenApi();
app.MapScalarApiReference();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var retainService = app.Services.GetKeyedService<IRetainedMessageService>("retain0");

var v1 = app.MapGroup("/api/v1")
    .WithOpenApi()
    .WithSummary("API v1 endpoints")
    .WithDescription("This group contains API v1 endpoints for various services.");

v1.MapGet("oraesatta", () => DateTimeOffset.Now)
    .WithSummary("Current time endpoint")
    .WithDescription("Returns the current server timestamp");

v1.MapGet("elencodevices", () => new string[] { "a", "b" })
    .WithSummary("ElencoDevices")
    .WithDescription("Ritorna la lista dei dispositivi");

v1.MapGet("cercacontenuti", async (
    [FromServices] ToolsLib.HttpHandler httpHandler,
    [FromQuery]
    [Description("The url of the website to discover")]
    string url
) =>
{
    return await httpHandler.CercaInHttpAsync(url);
})
.WithSummary("Cerca contenuti")
.WithDescription("Cerca contenuti nel web specificando un url");
;


app.Run();
