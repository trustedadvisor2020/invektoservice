using System.Text.Json;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;

namespace Invekto.WhatsAppAnalytics.Services;

/// <summary>
/// Nightly batch classification job. Runs at configured time (default 02:00).
/// Supports two modes:
/// - Config-only: process tenants from appsettings NightlyBatch:Tenants
/// - AutoDiscovery: query WaClient.Management for all active tenants, merge with config overrides
/// </summary>
public sealed class NightlyBatchJob : BackgroundService
{
    private readonly BatchClassificationService _batchService;
    private readonly ConversationOutcomeRepository _outcomeRepo;
    private readonly MssqlReaderService? _mssqlReader;
    private readonly JsonLinesLogger _logger;
    private readonly NightlyBatchConfig _config;

    public NightlyBatchJob(
        BatchClassificationService batchService,
        ConversationOutcomeRepository outcomeRepo,
        JsonLinesLogger logger,
        NightlyBatchConfig config,
        MssqlReaderService? mssqlReader = null)
    {
        _batchService = batchService;
        _outcomeRepo = outcomeRepo;
        _logger = logger;
        _config = config;
        _mssqlReader = mssqlReader;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.SystemInfo("[NightlyBatch] Disabled via config");
            return;
        }

        _logger.SystemInfo($"[NightlyBatch] Started, run hour={_config.RunHour:D2}:00, " +
            $"autoDiscovery={_config.AutoDiscovery}, {_config.Tenants.Count} config tenants");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = new DateTime(now.Year, now.Month, now.Day, _config.RunHour, 0, 0);
            if (nextRun <= now) nextRun = nextRun.AddDays(1);

            var delay = nextRun - now;
            _logger.SystemInfo($"[NightlyBatch] Next run at {nextRun:yyyy-MM-dd HH:mm}, waiting {delay.TotalHours:F1}h");

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) { break; }

            _logger.StepInfo("[NightlyBatch] Starting nightly run", "nightly");

            var tenants = await ResolveTenantListAsync(stoppingToken);

            foreach (var tenant in tenants)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    var jobId = await _outcomeRepo.CreateBatchJobAsync(
                        tenant.TenantId,
                        tenant.Database,
                        tenant.InstanceId,
                        tenant.Sector,
                        "nightly",
                        _config.LookbackDays,
                        stoppingToken);

                    _batchService.Enqueue(new BatchProcessJob
                    {
                        BatchJobId = jobId,
                        TenantId = tenant.TenantId,
                        DatabaseName = tenant.Database,
                        InstanceId = tenant.InstanceId,
                        Sector = tenant.Sector,
                        LookbackDays = _config.LookbackDays,
                        MaxThreads = _config.MaxThreadsPerTenant
                    });

                    _logger.StepInfo($"[NightlyBatch] Enqueued tenant {tenant.TenantId} ({tenant.Database}), job={jobId}", "nightly");
                }
                catch (Exception ex)
                {
                    _logger.SystemError($"[NightlyBatch] Failed to enqueue tenant {tenant.TenantId}: {ex.Message}");
                }
            }

            _logger.StepInfo($"[NightlyBatch] Enqueued {tenants.Count} tenants", "nightly");
        }
    }

    /// <summary>
    /// Resolve the final tenant list: auto-discovered tenants merged with config overrides.
    /// Config tenants (matched by DatabaseName) override auto-discovered ones for TenantId/Sector.
    /// </summary>
    private async Task<List<NightlyTenantConfig>> ResolveTenantListAsync(CancellationToken ct)
    {
        if (!_config.AutoDiscovery || _mssqlReader == null)
            return _config.Tenants;

        List<DiscoveredTenant> discovered;
        try
        {
            discovered = await _mssqlReader.DiscoverTenantsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.SystemError($"[NightlyBatch] Auto-discovery failed, falling back to config: {ex.Message}");
            return _config.Tenants;
        }

        // Build lookup of config overrides by DatabaseName (case-insensitive)
        var configByDb = new Dictionary<string, NightlyTenantConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in _config.Tenants)
            configByDb.TryAdd(t.Database, t);

        var result = new List<NightlyTenantConfig>(discovered.Count);

        foreach (var d in discovered)
        {
            if (configByDb.TryGetValue(d.DatabaseName, out var configOverride))
            {
                // Config tenant takes priority (preserves custom TenantId, Sector, InstanceId)
                result.Add(configOverride);
            }
            else
            {
                // Auto-discovered: use CompanyId as TenantId, sector defaults to "genel"
                result.Add(new NightlyTenantConfig
                {
                    TenantId = d.CompanyId,
                    Database = d.DatabaseName,
                    InstanceId = null, // process all instances
                    Sector = "genel"
                });
            }
        }

        // Add config tenants that weren't in discovery (e.g., custom DBs not in Management)
        foreach (var t in _config.Tenants)
        {
            if (!discovered.Any(d => d.DatabaseName.Equals(t.Database, StringComparison.OrdinalIgnoreCase)))
                result.Add(t);
        }

        _logger.SystemInfo($"[NightlyBatch] Resolved {result.Count} tenants ({discovered.Count} discovered, {_config.Tenants.Count} config overrides)");
        return result;
    }
}

/// <summary>
/// Configuration for nightly batch job (from appsettings).
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
