using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Outbound.Services;

/// <summary>
/// FEATURE B (automatic status-pull) — background sweep that makes the manual "Durumu Yenile" PULL
/// run on its own. For each allowlisted, WapCRM-configured tenant it finds the recent run-managed
/// projects with still-pending, wamid-bearing recipients and re-pulls their LIVE cxapi message-status
/// via <see cref="ProjectsService.RefreshRunStatusAsync"/> (the SAME monotonic, idempotent apply path
/// the ack webhook uses). This is the receipt path that does NOT depend on the partner's webhook PUSH
/// being wired: delivered/read surface in the projects list as soon as cxapi has them.
///
/// Operational shape mirrors <see cref="CxapiWebhookReconcileJob"/>/<c>InmaOptOutSyncJob</c>: timer-based
/// <see cref="IHostedService"/>, interlocked overlap guard, graceful shutdown, ONLY typed catches (no bare
/// catch(Exception)), one tenant's (and one project's) failure never blocks the rest.
///
/// Safety:
/// - Production-safe default OFF (<see cref="ProjectStatusPullOptions.Enabled"/>); the hosted service does
///   not even schedule a sweep until enabled via config.
/// - Vendor calls stay gated by the SAME <see cref="CxapiSendOptions"/> allowlist as send: a tenant the
///   sweep visits but that is not CxapiSend-allowed makes ZERO vendor calls (RefreshRunStatusAsync no-ops).
/// - Bounded fan-out: only runs newer than <see cref="ProjectStatusPullOptions.MaxRunAgeDays"/> are pulled
///   (an old run whose receipts never arrive is not pulled forever), and the per-project pull is itself
///   capped (StatusPullCap) inside the service.
/// - Single-instance assumption: Outbound runs as ONE NSSM service in prod; the apply path is idempotent +
///   monotonic so even a duplicate pull is harmless.
/// </summary>
public sealed class ProjectStatusPullJob : IHostedService, IDisposable
{
    private readonly OutboundRepository _outboundRepo;
    private readonly ProjectsRepository _projectsRepo;
    private readonly ProjectsService _projectsService;
    private readonly CxapiSendOptions _cxapiOptions;
    private readonly ProjectStatusPullOptions _options;
    private readonly JsonLinesLogger _logger;

    private Timer? _timer;
    private int _isProcessing;
    private CancellationTokenSource? _cts;

    public ProjectStatusPullJob(
        OutboundRepository outboundRepo,
        ProjectsRepository projectsRepo,
        ProjectsService projectsService,
        CxapiSendOptions cxapiOptions,
        ProjectStatusPullOptions options,
        JsonLinesLogger logger)
    {
        _outboundRepo = outboundRepo;
        _projectsRepo = projectsRepo;
        _projectsService = projectsService;
        _cxapiOptions = cxapiOptions;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.SystemInfo("ProjectStatusPullJob disabled (ProjectStatusPull:Enabled=false) — no automatic status-pull will run.");
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger.SystemInfo(
            $"ProjectStatusPullJob starting (intervalMs={_options.IntervalMs}, startupDelayMs={_options.StartupDelayMs}, maxRunAgeDays={_options.MaxRunAgeDays}, cxapiAllowlist=[{string.Join(",", _cxapiOptions.AllowedTenantIds)}])");

        _timer = new Timer(Tick, null, _options.StartupDelayMs, _options.IntervalMs);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("ProjectStatusPullJob stopping (graceful shutdown)");
        _timer?.Change(Timeout.Infinite, 0);
        _cts?.Cancel();

        var waited = 0;
        while (Interlocked.CompareExchange(ref _isProcessing, 0, 0) == 1 && waited < 300)
        {
            await Task.Delay(100, cancellationToken);
            waited++;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _cts?.Dispose();
    }

    private async void Tick(object? state)
    {
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0) return;

        try
        {
            var ct = _cts?.Token ?? CancellationToken.None;
            if (ct.IsCancellationRequested) return;

            // Tenant set: WapCRM-configured AND CxapiSend-allowed (the per-tenant vendor gate). A tenant
            // outside the CxapiSend allowlist would no-op inside RefreshRunStatusAsync anyway; filtering
            // here avoids the wasted candidate query + service round-trips.
            var configured = await _outboundRepo.GetWapCrmConfiguredTenantsAsync(ct);
            var tenantIds = configured
                .Select(t => t.TenantId)
                .Where(_cxapiOptions.IsTenantAllowed)
                .Distinct()
                .ToList();
            if (tenantIds.Count == 0) return;

            var totalChecked = 0;
            var totalUpdated = 0;
            foreach (var tenantId in tenantIds)
            {
                if (ct.IsCancellationRequested) break;
                var (checkedCount, updated) = await PullTenantAsync(tenantId, ct);
                totalChecked += checkedCount;
                totalUpdated += updated;
            }

            if (totalChecked > 0)
                _logger.SystemInfo($"ProjectStatusPullJob tick done: tenants={tenantIds.Count}, checked={totalChecked}, updated={totalUpdated}");
        }
        catch (OperationCanceledException)
        {
            _logger.SystemInfo("ProjectStatusPullJob tick cancelled (shutdown in progress)");
        }
        catch (Npgsql.NpgsqlException ex)
        {
            // DB unavailable for the tenant enumeration — drop this tick; the next one retries.
            _logger.SystemError($"[{ErrorCodes.ProjectStatusPullDbError}] ProjectStatusPullJob tick DB error: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }

    /// <summary>
    /// Pull every recent, pending-bearing project of one tenant. Each project is isolated in its own typed
    /// try/catch so one project's DB fault never aborts the tenant sweep. RefreshRunStatusAsync returns a
    /// typed (result, errorCode, message) tuple (it does NOT throw for vendor/DB faults) — a non-null
    /// errorCode is logged best-effort and the sweep moves on.
    /// </summary>
    private async Task<(int Checked, int Updated)> PullTenantAsync(int tenantId, CancellationToken ct)
    {
        List<long> projectIds;
        try
        {
            projectIds = await _projectsRepo.GetProjectIdsNeedingStatusPullAsync(tenantId, _options.MaxRunAgeDays, ct);
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemError($"[{ErrorCodes.ProjectStatusPullDbError}] ProjectStatusPullJob candidate query failed (tenant={tenantId}): {ex.Message}");
            return (0, 0);
        }

        var checkedCount = 0;
        var updated = 0;
        foreach (var projectId in projectIds)
        {
            if (ct.IsCancellationRequested) break;
            var (result, errorCode, message) = await _projectsService.RefreshRunStatusAsync(tenantId, projectId, null, ct);
            if (errorCode != null)
            {
                _logger.SystemWarn($"ProjectStatusPullJob pull skipped: tenant={tenantId}, project={projectId}, code={errorCode}, message={message}");
                continue;
            }
            if (result != null)
            {
                checkedCount += result.Checked;
                updated += result.Updated;
            }
        }
        return (checkedCount, updated);
    }
}

/// <summary>
/// Configuration for <see cref="ProjectStatusPullJob"/>, bound from the <c>ProjectStatusPull</c> section.
/// Production-safe default: DISABLED. Per-tenant vendor scope is the existing CxapiSend allowlist (NOT a
/// second list to keep in sync) — this section only controls whether the sweep runs and how often.
/// </summary>
public sealed class ProjectStatusPullOptions
{
    public const string SectionName = "ProjectStatusPull";

    /// <summary>Master kill-switch. Default false = the hosted service never schedules a sweep.</summary>
    public bool Enabled { get; set; }

    /// <summary>Sweep interval (ms). Default 2 minutes.</summary>
    public int IntervalMs { get; set; } = 120_000;

    /// <summary>Delay before the first sweep after startup (ms) — let DI/DB settle.</summary>
    public int StartupDelayMs { get; set; } = 30_000;

    /// <summary>Only runs newer than this are auto-pulled (bounds the fan-out; old runs whose receipts never arrive are skipped).</summary>
    public int MaxRunAgeDays { get; set; } = 14;
}
