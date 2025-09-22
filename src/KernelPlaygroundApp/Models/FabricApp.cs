using AzureApplicationLib;
using FabricLib;
using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using KernelPlaygroundApp.Components.Pages;

namespace KernelPlaygroundApp.Models;

public partial class FabricApp : PowerBiApplication
{
    public string WorkspaceId { get; set; }

    public FabricApp(AzureApplicationConfig config = null) : base(config)
    {
    }

    public async Task<AuthenticationResult> AuthenticateAsync()
    {
        return await ConfidentialAuthenticateAsync();
    }

    public async Task<Microsoft.PowerBI.Api.Models.Reports> GetReportsInGroupAsync()
    {
        var reports = await PowerBiClient.Reports.GetReportsInGroupAsync(Guid.Parse(WorkspaceId));
        return reports;
    }

    public async Task<Report> GetReportInGroupAsync(string reportId)
    {
        var report = await PowerBiClient.Reports.GetReportInGroupAsync(Guid.Parse(WorkspaceId), Guid.Parse(reportId));
        return report;
    }

    public async Task<string> GenerateTokenAsync(string reportId, string accessLevel = null)
    {
        var generateTokenRequestParameters = 
            new GenerateTokenRequest(
                accessLevel: accessLevel ?? "view"
            );
        var tokenResponse = await PowerBiClient.Reports.GenerateTokenAsync(
            Guid.Parse(WorkspaceId),
            Guid.Parse(reportId),
            generateTokenRequestParameters);
        return tokenResponse.Token;
    }
}