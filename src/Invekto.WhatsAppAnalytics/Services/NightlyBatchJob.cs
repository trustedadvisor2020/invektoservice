using System.Text.Json;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;

namespace Invekto.WhatsAppAnalytics.Services;

/// <summary>
/// Nightly batch classification job. Runs at configured time (default 02:00).
/// For each configured tenant: enqueue batch classification for new conversations.
/// </summary>
public sealed class NightlyBatchJob : BackgroundService
{
    private readonly BatchClassificationService _batchService;
    private readonly ConversationOutcomeRepository _outcomeRepo;
    private readonly JsonLinesLogger _logger;
    private readonly NightlyBatchConfig _config;

    public NightlyBatchJob(
        BatchClassificationService batchService,
        ConversationOutcomeRepository outcomeRepo,
        JsonLinesLogger logger,
        NightlyBatchConfig config)
    {
        _batchService = batchService;
        _outcomeRepo = outcomeRepo;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.SystemInfo("[NightlyBatch] Disabled via config");
            return;
        }

        _logger.SystemInfo($"[NightlyBatch] Started, run hour={_config.RunHour:D2}:00, {_config.Tenants.Count} tenants configured");

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

            foreach (var tenant in _config.Tenants)
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

            _logger.StepInfo($"[NightlyBatch] Enqueued {_config.Tenants.Count} tenants", "nightly");
        }
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
    public List<NightlyTenantConfig> Tenants { get; set; } = new();
}

public sealed class NightlyTenantConfig
{
    public int TenantId { get; set; }
    public string Database { get; set; } = "";
    public int? InstanceId { get; set; }
    public string? Sector { get; set; }
}
