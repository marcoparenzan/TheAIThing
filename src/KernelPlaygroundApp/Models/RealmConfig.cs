namespace KernelPlaygroundApp.Models;

public class RealmConfig
{
    public string RealmId { get; set; } = null!;

    public string? RealmName { get; set; }

    public string? RealmClientId { get; set; }

    public string? RealmClientSecret { get; set; }

    public string? Authority { get; set; }
}
