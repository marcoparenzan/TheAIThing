using System;
using System.Collections.Generic;

namespace KernelPlaygroundApp.Models;

public partial class RealmClient
{
    public string RealmId { get; set; } = null!;

    public string? RealmName { get; set; }

    public string? RealmClientId { get; set; }

    public string? RealmClientSecret { get; set; }

    public string? Authority { get; set; }
}
