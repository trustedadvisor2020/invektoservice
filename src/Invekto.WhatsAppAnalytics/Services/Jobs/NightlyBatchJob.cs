using Hangfire;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;
using Microsoft.Data.SqlClient;

namespace Invekto.WhatsAppAnalytics.Services.Jobs;

/// <summary>
/// G7 Faz 5: Hangfire recurring job replacing the old NightlyBatchJob BackgroundService.
/// Resolves the tenant list (config-only OR auto-discovered + config overrides) and
/// enqueues each tenant to <see cref="BatchClassificationService"/>.
///
/// Queue: <c>waanalytics</c>. Recurring id: <c>waanalytics:nightly-batch</c>
/// (cron <c>0 {RunHour} * * *</c>, built at startup from <see cref="NightlyBatchConfig.RunHour"/>).
/// Config <c>Enabled=false</c> short-circuits the handler.
/// </summary>
[Queue("waanalytics")]
[DisableConcurrentExecution(timeoutInSeconds: 3600)]
public sealed class NightlyBatchJob
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

    public async Task RunAsync(CancellationToken ct = default)
    {
        if (!_config.Enabled)
        {
            _logger.SystemInfo("[NightlyBatch] Disabled via config — skipping run");
            return;
        }

        try
        {
            _logger.StepInfo("[NightlyBatch] Starting nightly run", "nightly");

            var tenants = await ResolveTenantListAsync(ct);

            foreach (var tenant in tenants)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var jobId = await _outcomeRepo.CreateBatchJobAsync(
                        tenant.TenantId,
                        tenant.Database,
                        tenant.InstanceId,
                        tenant.Sector,
                        "nightly",
                        _config.LookbackDays,
                        ct);

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

                    _logger.StepInfo(
                        $"[NightlyBatch] Enqueued tenant {tenant.TenantId} ({tenant.Database}), job={jobId}",
                        "nightly");
                }
                catch (SqlException ex)
                {
                    _logger.SystemError(
                        $"[{ErrorCodes.WADiscoveryFailed}] [NightlyBatch] Failed to enqueue tenant {tenant.TenantId} (SQL): {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.SystemError(
                        $"[{ErrorCodes.WADiscoveryFailed}] [NightlyBatch] Failed to enqueue tenant {tenant.TenantId}: {ex.Message}");
                }
            }

            _logger.StepInfo($"[NightlyBatch] Enqueued {tenants.Count} tenants", "nightly");
        }
        catch (OperationCanceledException)
        {
            _logger.SystemInfo("[NightlyBatch] Cancelled (graceful shutdown)");
        }
        // Other exceptions bubble to Hangfire AutomaticRetry + INV-JOB-005.
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
        catch (SqlException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.WADiscoveryFailed}] [NightlyBatch] Auto-discovery SQL error, falling back to config: {ex.Message}");
            return _config.Tenants;
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.WADiscoveryFailed}] [NightlyBatch] Auto-discovery connection error, falling back to config: {ex.Message}");
            return _config.Tenants;
        }

        var configByDb = new Dictionary<string, NightlyTenantConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in _config.Tenants)
            configByDb.TryAdd(t.Database, t);

        var result = new List<NightlyTenantConfig>(discovered.Count);

        foreach (var d in discovered)
        {
            if (configByDb.TryGetValue(d.DatabaseName, out var configOverride))
            {
                result.Add(configOverride);
            }
            else
            {
                result.Add(new NightlyTenantConfig
                {
                    TenantId = d.CompanyId,
                    Database = d.DatabaseName,
                    InstanceId = null,
                    Sector = "genel"
                });
            }
        }

        foreach (var t in _config.Tenants)
        {
            if (!discovered.Any(d => d.DatabaseName.Equals(t.Database, StringComparison.OrdinalIgnoreCase)))
                result.Add(t);
        }

        _logger.SystemInfo(
            $"[NightlyBatch] Resolved {result.Count} tenants ({discovered.Count} discovered, {_config.Tenants.Count} config overrides)");
        return result;
    }
}
