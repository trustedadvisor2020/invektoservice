namespace Invekto.WhatsAppAnalytics.Services;

/// <summary>
/// G7 Faz 5: Extracted from the old NightlyBatchJob BackgroundService so the config
/// types survive alongside the new Hangfire job class in Services/Jobs/.
/// </summary>
public sealed class NightlyBatchConfig
{
    public bool Enabled { get; set; }
    public int RunHour { get; set; } = 2;
    public int LookbackDays { get; set; } = 7;
    public int MaxThreadsPerTenant { get; set; } = 500;
    public bool AutoDiscovery { get; set; }
    public List<NightlyTenantConfig> Tenants { get; set; } = new();
}

public sealed class NightlyTenantConfig
{
    public int TenantId { get; set; }
    public string Database { get; set; } = "";
    public int? InstanceId { get; set; }
    public string? Sector { get; set; }
}
