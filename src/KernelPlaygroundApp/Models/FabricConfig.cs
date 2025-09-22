using System;
using System.Collections.Generic;

namespace KernelPlaygroundApp.Models;

public partial class FabricConfig
{
    public string RealmId { get; set; } = null!;

    public string RealmClientId { get; set; } = null!;

    public string? TenantId { get; set; }

    public string? Location { get; set; }

    public string? ClientName { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string? Resource { get; set; }

    public string? WorkspaceId { get; set; }

    public string? WorkspaceName { get; set; }

    public string? SubscriptionId { get; set; }
}
