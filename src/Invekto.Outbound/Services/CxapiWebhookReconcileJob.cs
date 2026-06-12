using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Inma;
using Invekto.Shared.Logging;

namespace Invekto.Outbound.Services;

/// <summary>
/// Feature A — cxapi message-webhook reconciliation.
///
/// Ensures every allowlisted, WapCRM-configured tenant's cxapi MESSAGES webhook points at our
/// delivery-ack ingress <c>{PublicBaseUrl}/api/v1/webhook/event?companyId={tenant_id}</c> so
/// delivery/read acks flow. There is no programmatic "config write" hook (wapcrm config is written
/// by manual SQL), so this is a reconciliation SWEEP, not a write-triggered action: it runs at
/// startup and on a slow interval, fully reconciling every tick (NO confirmed-cache — that would
/// mask drift if the webhook is changed externally; with a tiny tenant set the read each tick is
/// negligible).
///
/// Pattern: Timer-based <see cref="IHostedService"/>, identical operational shape to
/// <see cref="InmaOptOutSyncJob"/> — interlocked overlap guard, graceful shutdown, ONLY typed
/// catches (no bare catch(Exception)), one tenant's failure never blocks the others.
///
/// Safety:
/// - Production-safe default OFF (<see cref="CxapiWebhookReconcileOptions.Enabled"/>); rollout scope
///   is an explicit allowlist (or AllowAllConfigured), not merely "has a secret".
/// - NEVER touches the separate HMAC-signed /api/webhook-settings/events channel — only /messages.
/// - A FOREIGN (non-Invekto) webhook is left UNTOUCHED and warn-logged (INV-OB-092): never clobber a
///   customer's own integration. "Ours" is matched on host+path+companyId so a future PublicBaseUrl
///   migration does not self-classify the old URL as foreign.
/// - Single-instance assumption: Outbound runs as ONE NSSM service in prod, so no cross-process
///   advisory lock is needed; duplicate idempotent SETs would be harmless anyway.
/// </summary>
public sealed class CxapiWebhookReconcileJob : IHostedService, IDisposable
{
    private readonly OutboundRepository _repository;
    private readonly WapCrmWebhookSettingsClient _client;
    private readonly CxapiWebhookReconcileOptions _options;
    private readonly JsonLinesLogger _logger;

    private const string IngressPath = "/api/v1/webhook/event";

    private Timer? _timer;
    private int _isProcessing;
    private CancellationTokenSource? _cts;

    public CxapiWebhookReconcileJob(
        OutboundRepository repository,
        WapCrmWebhookSettingsClient client,
        CxapiWebhookReconcileOptions options,
        JsonLinesLogger logger)
    {
        _repository = repository;
        _client = client;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.SystemInfo("CxapiWebhookReconcileJob disabled (CxapiWebhookReconcile:Enabled=false) — no reconciliation will run.");
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger.SystemInfo(
            $"CxapiWebhookReconcileJob starting (allowAll={_options.AllowAllConfigured}, allowlist=[{string.Join(",", _options.AllowedTenantIds)}], intervalMs={_options.IntervalMs}, publicBase={_options.PublicBaseUrl})");

        // First sweep shortly after boot (let DI/DB settle), then on the slow interval.
        _timer = new Timer(Tick, null, _options.StartupDelayMs, _options.IntervalMs);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("CxapiWebhookReconcileJob stopping (graceful shutdown)");
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

            var tenants = await _repository.GetWapCrmConfiguredTenantsAsync(ct);
            var targets = tenants.Where(t => _options.IsTenantAllowed(t.TenantId)).ToList();
            if (targets.Count == 0) return;

            foreach (var tenant in targets)
            {
                if (ct.IsCancellationRequested) break;
                await ReconcileTenantAsync(tenant, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during graceful shutdown; kept observable.
            _logger.SystemInfo("CxapiWebhookReconcileJob tick cancelled (shutdown in progress)");
        }
        catch (Npgsql.NpgsqlException ex)
        {
            // DB unavailable — drop this tick; next one retries.
            _logger.SystemError($"[{ErrorCodes.CxapiWebhookReconcileCallFailed}] CxapiWebhookReconcileJob tick DB error: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }

    /// <summary>
    /// Reconcile a single tenant. GET current → decide → (maybe) SET. Each tenant is isolated in its
    /// own typed try/catch so one tenant's transport/timeout fault never aborts the sweep.
    /// </summary>
    private async Task ReconcileTenantAsync(WapCrmConfiguredTenant tenant, CancellationToken ct)
    {
        var desiredUrl = BuildWebhookUrl(_options.PublicBaseUrl, tenant.TenantId);

        try
        {
            var read = await _client.GetMessageWebhookAsync(tenant.SecretKey, tenant.TenantId, ct);

            switch (read.Outcome)
            {
                case WapCrmWebhookOutcome.IpDenied:
                    _logger.SystemError(
                        $"[{ErrorCodes.CxapiWebhookIpNotWhitelisted}] cxapi webhook reconcile: 509 IP not whitelisted (read): tenant={tenant.TenantId}, requestId={read.ProviderRequestId}");
                    return;
                case WapCrmWebhookOutcome.Ok:
                    break; // fall through to decision logic
                default:
                    LogCallFailure("read", tenant.TenantId, read.Outcome, read.HttpStatusCode, read.ProviderStatusCode, read.ProviderRequestId, read.Message);
                    return;
            }

            var current = read.CurrentWebhookUrl?.Trim();

            if (string.IsNullOrEmpty(current))
            {
                await SetAsync(tenant, desiredUrl, "empty", ct);
                return;
            }

            if (IsOwnedWebhook(current, tenant.TenantId))
            {
                // Already ours and pointing at the right URL + active → nothing to do.
                if (string.Equals(current, desiredUrl, StringComparison.OrdinalIgnoreCase) && read.IsActive)
                    return;
                // Ours but inactive, or on an old/owned host (PublicBaseUrl migration) → normalize.
                await SetAsync(tenant, desiredUrl, read.IsActive ? "owned-host-migrate" : "owned-inactive", ct);
                return;
            }

            // Foreign URL — do NOT clobber. Log host ONLY (the URL may carry tokens).
            _logger.SystemWarn(
                $"[{ErrorCodes.CxapiWebhookForeignUrl}] cxapi webhook reconcile: tenant={tenant.TenantId} has a FOREIGN messages webhook (host={SafeHost(current)}) — left untouched; ACKs will not reach Invekto until an operator redirects it.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // shutdown — let the Tick handler observe it
        }
        catch (OperationCanceledException)
        {
            // Defensive: the client maps its own timeout to Outcome.Timeout, but a CTS edge could
            // still surface here. Treat as a transient call failure for this tenant only.
            LogCallFailure("read", tenant.TenantId, WapCrmWebhookOutcome.Timeout, 0, null, null, null);
        }
        catch (HttpRequestException ex)
        {
            LogCallFailure("read", tenant.TenantId, WapCrmWebhookOutcome.TransportError, 0, null, null, ex.Message);
        }
    }

    private async Task SetAsync(WapCrmConfiguredTenant tenant, string desiredUrl, string reason, CancellationToken ct)
    {
        var write = await _client.SetMessageWebhookAsync(tenant.SecretKey, desiredUrl, tenant.TenantId, ct);

        switch (write.Outcome)
        {
            case WapCrmWebhookOutcome.Ok:
                _logger.SystemInfo(
                    $"cxapi webhook reconcile: SET messages webhook for tenant={tenant.TenantId} (reason={reason}, requestId={write.ProviderRequestId})");
                return;
            case WapCrmWebhookOutcome.IpDenied:
                _logger.SystemError(
                    $"[{ErrorCodes.CxapiWebhookIpNotWhitelisted}] cxapi webhook reconcile: 509 IP not whitelisted (set): tenant={tenant.TenantId}, requestId={write.ProviderRequestId}");
                return;
            default:
                LogCallFailure("set", tenant.TenantId, write.Outcome, write.HttpStatusCode, write.ProviderStatusCode, write.ProviderRequestId, write.Message);
                return;
        }
    }

    private void LogCallFailure(string op, int tenantId, WapCrmWebhookOutcome outcome, int http, string? providerStatusCode, string? requestId, string? message)
    {
        _logger.SystemWarn(
            $"[{ErrorCodes.CxapiWebhookReconcileCallFailed}] cxapi webhook reconcile {op} failed: tenant={tenantId}, outcome={outcome}, http={http}, providerStatus={providerStatusCode}, requestId={requestId}, message={message}");
    }

    /// <summary>
    /// Build the delivery-ack ingress URL. companyId == our internal tenant_id (a positive int, so no
    /// query-escaping concern). PublicBaseUrl is trimmed of a trailing slash to avoid '//api/...'.
    /// </summary>
    private static string BuildWebhookUrl(string publicBaseUrl, int tenantId)
        => $"{publicBaseUrl.TrimEnd('/')}{IngressPath}?companyId={tenantId}";

    /// <summary>
    /// "Ours" iff the URL parses, its host is one of our owned hosts, its path is the ingress path,
    /// and its companyId query equals this tenant. Host-set match (not exact-string) so a later
    /// PublicBaseUrl host change does not classify the previously-Invekto URL as foreign.
    /// </summary>
    private bool IsOwnedWebhook(string url, int tenantId)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var ownedHosts = OwnedHosts();
        if (!ownedHosts.Contains(uri.Host))
            return false;

        if (!string.Equals(uri.AbsolutePath.TrimEnd('/'), IngressPath, StringComparison.OrdinalIgnoreCase))
            return false;

        return TryGetCompanyId(uri.Query, out var cid) && cid == tenantId;
    }

    /// <summary>
    /// Parse the <c>companyId</c> int from a raw query string ("?companyId=5050&amp;x=y"). Self-contained
    /// (no System.Web dependency); companyId is always a plain int so no URL-decoding is required.
    /// </summary>
    private static bool TryGetCompanyId(string query, out int companyId)
    {
        companyId = 0;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx <= 0) continue;
            if (!string.Equals(pair[..idx], "companyId", StringComparison.OrdinalIgnoreCase)) continue;
            return int.TryParse(pair[(idx + 1)..], out companyId);
        }
        return false;
    }

    /// <summary>The set of hosts we consider Invekto-owned. Defaults to the PublicBaseUrl host when not configured.</summary>
    private HashSet<string> OwnedHosts()
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in _options.OwnedWebhookHosts)
        {
            if (!string.IsNullOrWhiteSpace(h))
                hosts.Add(h.Trim());
        }
        if (Uri.TryCreate(_options.PublicBaseUrl, UriKind.Absolute, out var baseUri))
            hosts.Add(baseUri.Host);
        return hosts;
    }

    /// <summary>Host of a URL for logging — never the full URL (it may carry tokens). Falls back to a redaction marker.</summary>
    private static string SafeHost(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "(unparseable)";
}

/// <summary>
/// Feature A configuration, bound from the <c>CxapiWebhookReconcile</c> section. Production-safe
/// default: DISABLED with an empty allowlist (fully inert).
/// </summary>
public sealed class CxapiWebhookReconcileOptions
{
    public const string SectionName = "CxapiWebhookReconcile";

    /// <summary>Master kill-switch. Default false = the hosted service never schedules a sweep.</summary>
    public bool Enabled { get; set; }

    /// <summary>When true, reconcile EVERY WapCRM-configured tenant (hands-off mode). Default false = only <see cref="AllowedTenantIds"/>.</summary>
    public bool AllowAllConfigured { get; set; }

    /// <summary>Explicit rollout allowlist (used when <see cref="AllowAllConfigured"/> is false). Empty = no tenant reconciled.</summary>
    public List<int> AllowedTenantIds { get; set; } = new();

    /// <summary>Public base URL of our delivery-ack ingress. The webhook is set to {PublicBaseUrl}/api/v1/webhook/event?companyId={tenant_id}.</summary>
    public string PublicBaseUrl { get; set; } = "https://services.invekto.com";

    /// <summary>Extra hosts considered Invekto-owned (besides the PublicBaseUrl host) — e.g. to support a host migration without flagging the old URL as foreign.</summary>
    public List<string> OwnedWebhookHosts { get; set; } = new();

    /// <summary>cxapi base URL for the webhook-settings calls.</summary>
    public string BaseUrl { get; set; } = "https://cxapi.wapcrm.net";

    /// <summary>Sweep interval (ms). Default 10 minutes — reads are cheap and the tenant set is tiny.</summary>
    public int IntervalMs { get; set; } = 600_000;

    /// <summary>Delay before the first sweep after startup (ms).</summary>
    public int StartupDelayMs { get; set; } = 15_000;

    /// <summary>Per-request timeout (ms) enforced by the client's linked CTS.</summary>
    public int TimeoutMs { get; set; } = 5_000;

    /// <summary>Finite HttpClient.Timeout backstop (ms) — must be &gt; <see cref="TimeoutMs"/> so the per-request CTS fires first.</summary>
    public int HttpClientBackstopMs { get; set; } = 15_000;

    public bool IsTenantAllowed(int tenantId) => Enabled && (AllowAllConfigured || AllowedTenantIds.Contains(tenantId));
}
