using System.Text.Json;
using Hangfire;
using Invekto.Backend.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Backend.Services.MetaLeadgen;

/// <summary>
/// Paket B-META: orchestrates the public webhook POST path.
/// Sequence (designed for Meta's 3s ack target):
///   1. Tenant-exists + is_active gate (<see cref="ErrorCodes.MetaTenantUnknown"/>)
///   2. Config load (app_secret extraction — no secret reaches logs)
///   3. Signature verify via <see cref="MetaLeadgenSignatureValidator"/>
///   4. Payload parse -> extract leadgen_id / form_id / page_id
///   5. INSERT meta_leadgen_events audit row (UNIQUE dedup anchor)
///   6. Hangfire enqueue <c>MetaLeadgenIntakeJob</c> on the <c>automation</c>
///      queue for the async Graph API + intake hop
///
/// Fail-loud audit discipline: signature mismatch + tenant unknown + config
/// missing ALL write an audit row (with http_status=401/404/500 + error_code)
/// so an attacker probing the endpoint leaves fingerprints. Only the happy
/// path and duplicate-of-a-happy-path do NOT enqueue a Hangfire job.
/// </summary>
public sealed class MetaLeadgenWebhookService
{
    private readonly MetaLeadgenConfigRepository _configRepo;
    private readonly MetaLeadgenEventRepository _eventRepo;
    private readonly TenantRegistryRepository _tenantRegistry;
    private readonly IBackgroundJobClient _jobs;
    private readonly JsonLinesLogger _logger;

    public MetaLeadgenWebhookService(
        MetaLeadgenConfigRepository configRepo,
        MetaLeadgenEventRepository eventRepo,
        TenantRegistryRepository tenantRegistry,
        IBackgroundJobClient jobs,
        JsonLinesLogger logger)
    {
        _configRepo = configRepo;
        _eventRepo = eventRepo;
        _tenantRegistry = tenantRegistry;
        _jobs = jobs;
        _logger = logger;
    }

    public async Task<WebhookOutcome> ProcessWebhookAsync(
        int tenantId,
        byte[] rawBody,
        string? signatureHeader,
        CancellationToken ct)
    {
        // 1. Tenant guard — single PK lookup.
        bool tenantExists;
        try
        {
            tenantExists = await _tenantRegistry.TenantExistsAsync(tenantId, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgenWebhook: tenant exist DB error tenant={tenantId}: {ex.Message}");
            return WebhookOutcome.Error(503, ErrorCodes.DatabaseConnectionFailed);
        }
        if (!tenantExists)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.MetaTenantUnknown}] MetaLeadgenWebhook: tenant not in registry tenant={tenantId}");
            return WebhookOutcome.Rejected(404, ErrorCodes.MetaTenantUnknown);
        }

        // 2. Config load.
        Shared.Contracts.MetaLeadgen.MetaLeadgenConfigDto config;
        try
        {
            (config, _) = await _configRepo.GetConfigAsync(tenantId, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgenWebhook: config DB error tenant={tenantId}: {ex.Message}");
            return WebhookOutcome.Error(503, ErrorCodes.DatabaseConnectionFailed);
        }
        catch (JsonException ex)
        {
            // State-corruption of tenant_settings.meta_leadgen_config JSONB — an
            // operator must reseed via Dashboard PUT before further webhooks can
            // succeed. Mapped to GeneralValidation (not MetaFieldDataParseFailed
            // which semantically covers Graph API response field_data) so ops
            // dashboards distinguish "stored config bad" from "upstream Meta
            // payload bad".
            _logger.SystemWarn(
                $"[{ErrorCodes.GeneralValidation}] MetaLeadgenWebhook: stored config JSON corrupt tenant={tenantId} — " +
                $"Operator: SELECT meta_leadgen_config::text FROM tenant_settings WHERE tenant_id={tenantId}; " +
                $"inspect and PUT /api/v1/tenant-settings/meta-leadgen to repair. parse_err={ex.Message}");
            return WebhookOutcome.Error(500, ErrorCodes.GeneralValidation);
        }

        // 3. Signature verify. Missing app_secret collapses to "invalid signature"
        //    from the caller's perspective — don't leak configuration state.
        var signatureOk = MetaLeadgenSignatureValidator.Verify(
            config.AppSecret, rawBody, signatureHeader);

        // Payload-parse attempts best-effort even on signature fail so we can
        // audit leadgen_id alongside the 401 row (forensic signal for ops).
        var (leadgenId, formId, pageId, rawJson) = TryParsePayload(rawBody);

        if (!signatureOk)
        {
            // Sentinel when leadgen_id is absent/unparseable: guid ensures
            // repeated attacker probes each get their own audit row rather
            // than colliding on the UNIQUE (tenant_id, leadgen_id) anchor.
            var auditId = leadgenId ?? $"signature-invalid-{Guid.NewGuid():N}";
            await AuditInsertAsync(
                tenantId, auditId,
                formId, pageId,
                httpStatus: 401,
                errorCode: ErrorCodes.MetaSignatureInvalid,
                rawPayloadJson: rawJson,
                ct);
            _logger.SystemWarn(
                $"[{ErrorCodes.MetaSignatureInvalid}] MetaLeadgenWebhook: signature mismatch tenant={tenantId} leadgen={leadgenId ?? "(none)"}");
            return WebhookOutcome.Rejected(401, ErrorCodes.MetaSignatureInvalid);
        }

        // Post-signature-valid: require a leadgen_id for the idempotency anchor.
        // An empty array (Meta "entry":[{}]) is technically valid spec-wise but
        // cannot be persisted with our UNIQUE (tenant_id, leadgen_id) model —
        // we accept (200 to Meta) + audit it with INV-META-004.
        if (string.IsNullOrEmpty(leadgenId))
        {
            await AuditInsertAsync(
                tenantId, $"empty-payload-{Guid.NewGuid():N}",
                formId, pageId,
                httpStatus: 200,
                errorCode: ErrorCodes.MetaFieldDataParseFailed,
                rawPayloadJson: rawJson,
                ct);
            _logger.SystemWarn(
                $"[{ErrorCodes.MetaFieldDataParseFailed}] MetaLeadgenWebhook: payload without leadgen_id tenant={tenantId}");
            return WebhookOutcome.Accepted();
        }

        // 4. Audit row INSERT. UNIQUE conflict => Meta retry (dup); return 200
        //    without re-enqueueing the Hangfire job.
        InsertEventOutcome audit;
        try
        {
            audit = await _eventRepo.InsertEventAsync(
                tenantId, leadgenId, formId, pageId,
                httpStatus: 200,
                errorCode: null,
                rawPayloadJson: rawJson,
                ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgenWebhook: event insert DB error tenant={tenantId} leadgen={leadgenId}: {ex.Message}");
            return WebhookOutcome.Error(503, ErrorCodes.DatabaseConnectionFailed);
        }

        if (audit.IsUnknownFailure)
        {
            _logger.SystemError(
                $"[{ErrorCodes.GeneralUnknown}] MetaLeadgenWebhook: insert conflict but re-SELECT empty tenant={tenantId} leadgen={leadgenId}");
            return WebhookOutcome.Error(500, ErrorCodes.GeneralUnknown);
        }

        if (audit.IsDuplicate)
        {
            // Retry enqueue whenever the prior event did not reach a completed
            // intake (lead_id NULL) or has an explicit INV-JOB-001 marker. This
            // single condition covers three dedup paths safely:
            //   (1) ExistingErrorCode = INV-JOB-001: prior enqueue failure left
            //       an explicit marker.
            //   (2) HasLinkedLead = false AND ExistingErrorCode = null: the
            //       combined-failure edge case where BOTH the Hangfire enqueue
            //       AND the INV-JOB-001 marker UPDATE failed, leaving the row
            //       looking healthy (http_status=200) but intake never completed.
            //       Without this guard a well-behaved Meta retry would be
            //       misclassified as a healthy duplicate and silently dropped.
            //   (3) HasLinkedLead = true, ExistingErrorCode = null: genuine
            //       dedup of a processed event — skip enqueue, return 200.
            // Re-enqueue is safe under (1) and (2) because LeadIntakeService's
            // phone-based UPSERT is idempotent and Hangfire dedup prevents
            // double-processing of an in-flight job for the same leadgen_id.
            var needsRetry = audit.ExistingErrorCode == ErrorCodes.JobStorageConnectionFailed
                             || !audit.HasLinkedLead;
            if (needsRetry)
            {
                var retryReason = audit.ExistingErrorCode == ErrorCodes.JobStorageConnectionFailed
                    ? "previous attempt failed under INV-JOB-001"
                    : "stuck row (no lead_id, no error_code — combined enqueue + UpdateStatus failure edge case)";
                var retryOutcome = await TryEnqueueIntakeJobAsync(tenantId, leadgenId, audit.EventId, ct).ConfigureAwait(false);
                if (retryOutcome.Enqueued)
                {
                    _logger.StepInfo(
                        $"MetaLeadgenWebhook: duplicate leadgen re-enqueued ({retryReason}) tenant={tenantId} leadgen={leadgenId} eventId={audit.EventId}",
                        requestId: leadgenId);
                    return WebhookOutcome.Accepted();
                }
                _logger.SystemWarn(
                    $"[{ErrorCodes.JobStorageConnectionFailed}] MetaLeadgenWebhook: duplicate re-enqueue ALSO failed tenant={tenantId} leadgen={leadgenId} eventId={audit.EventId} retry_reason={retryReason} — return 500 so Meta retries again");
                return WebhookOutcome.Error(500, ErrorCodes.JobStorageConnectionFailed);
            }

            _logger.StepInfo(
                $"MetaLeadgenWebhook: duplicate leadgen (Meta retry, event already processed lead_id={audit.EventId}) tenant={tenantId} leadgen={leadgenId} eventId={audit.EventId} — skip enqueue",
                requestId: leadgenId);
            return WebhookOutcome.Accepted();
        }

        // 5. Fresh INSERT — try enqueue. Durability contract: if enqueue fails
        //    we REFUSE to return 200 to Meta (would be a silent drop — Meta
        //    stops retrying, no subsequent call re-triggers enqueue since the
        //    UNIQUE anchor short-circuits). Instead we mark the audit row with
        //    INV-JOB-001 so the *next* Meta retry hits the duplicate branch
        //    above and re-attempts enqueue from a recovered Hangfire pool.
        var freshOutcome = await TryEnqueueIntakeJobAsync(tenantId, leadgenId, audit.EventId, ct).ConfigureAwait(false);
        if (!freshOutcome.Enqueued)
        {
            return WebhookOutcome.Error(500, ErrorCodes.JobStorageConnectionFailed);
        }

        _logger.StepInfo(
            $"MetaLeadgenWebhook: accepted tenant={tenantId} leadgen={leadgenId} eventId={audit.EventId}",
            requestId: leadgenId);
        return WebhookOutcome.Accepted();
    }

    /// <summary>
    /// Enqueue the Hangfire intake job + update the audit row's error_code
    /// based on the outcome. Centralizes the durability contract so both the
    /// fresh-INSERT path and the duplicate-retry recovery path share one
    /// implementation (and one set of typed catches).
    ///
    /// Returns <c>Enqueued=true</c> on success; the audit row is left with
    /// <c>http_status=200, error_code=NULL</c> so a subsequent Meta retry is
    /// a normal no-op dedup.
    ///
    /// Returns <c>Enqueued=false</c> on any Hangfire transport failure; the
    /// audit row is stamped <c>http_status=500, error_code=INV-JOB-001</c> so
    /// the next retry can recover. If even the status update fails (DB outage
    /// during Hangfire outage), we log and continue — the caller returns 500
    /// to Meta either way and Meta's retry loop is our durability safety net.
    /// </summary>
    private async Task<EnqueueAttempt> TryEnqueueIntakeJobAsync(
        int tenantId, string leadgenId, long eventId, CancellationToken ct)
    {
        string? failureCode = null;
        string? failureDetail = null;
        try
        {
            _jobs.Enqueue<Invekto.Automation.Services.Jobs.MetaLeadgenIntakeJob>(
                job => job.ExecuteAsync(tenantId, leadgenId, eventId, CancellationToken.None));
        }
        catch (BackgroundJobClientException ex)
        {
            failureCode = ErrorCodes.JobStorageConnectionFailed;
            failureDetail = $"rejected: {ex.Message}";
        }
        catch (NpgsqlException ex)
        {
            failureCode = ErrorCodes.JobStorageConnectionFailed;
            failureDetail = $"storage DB error: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            failureCode = ErrorCodes.JobHandlerUnresolved;
            failureDetail = $"misconfigured: {ex.Message}";
        }

        if (failureCode is null)
        {
            // Clear any prior INV-JOB-001 marker on success (covers the retry path).
            try
            {
                await _eventRepo.UpdateStatusAsync(eventId, tenantId, httpStatus: 200, errorCode: null, ct).ConfigureAwait(false);
            }
            catch (NpgsqlException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgenWebhook: status clear after enqueue-success failed tenant={tenantId} leadgen={leadgenId} eventId={eventId}: {ex.Message} (non-fatal; job is already on the queue)");
            }
            return new EnqueueAttempt(true);
        }

        _logger.SystemWarn(
            $"[{failureCode}] MetaLeadgenWebhook: Hangfire enqueue failed tenant={tenantId} leadgen={leadgenId} eventId={eventId}: {failureDetail}");
        try
        {
            await _eventRepo.UpdateStatusAsync(eventId, tenantId, httpStatus: 500, errorCode: failureCode, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgenWebhook: audit status update ALSO failed tenant={tenantId} leadgen={leadgenId} eventId={eventId}: {ex.Message} (Meta retry will re-enter the INSERT path; the UNIQUE conflict + absent error_code is a latent edge case — ops should inspect if this line fires)");
        }
        return new EnqueueAttempt(false);
    }

    /// <summary>
    /// Best-effort audit insert for non-happy paths. Failures are logged but
    /// never throw back to the webhook handler — the ingest layer cannot
    /// afford DB noise to obscure the caller-facing 401/403/200 response.
    /// </summary>
    private async Task AuditInsertAsync(
        int tenantId, string leadgenIdOrSentinel,
        string? formId, string? pageId,
        short httpStatus, string? errorCode, string? rawPayloadJson,
        CancellationToken ct)
    {
        try
        {
            await _eventRepo.InsertEventAsync(
                tenantId, leadgenIdOrSentinel, formId, pageId,
                httpStatus, errorCode, rawPayloadJson, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgenWebhook: audit INSERT secondary fail tenant={tenantId} ec={errorCode}: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts leadgen_id / form_id / page_id from the canonical Meta webhook
    /// shape <c>{ entry: [ { changes: [ { value: { leadgen_id, form_id, page_id } } ] } ] }</c>.
    /// Returns (null, null, null, original-raw-json) on any parse failure —
    /// caller decides how to audit. rawJson is the utf8 decode of the body
    /// for JSONB storage (meta_leadgen_events.raw_payload). Null-valued
    /// components stay null so the repository columns correctly reflect absence.
    /// </summary>
    private (string? LeadgenId, string? FormId, string? PageId, string? RawJson) TryParsePayload(byte[] rawBody)
    {
        if (rawBody is null || rawBody.Length == 0)
            return (null, null, null, null);

        string raw;
        try
        {
            raw = System.Text.Encoding.UTF8.GetString(rawBody);
        }
        catch (ArgumentException)
        {
            return (null, null, null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (!root.TryGetProperty("entry", out var entryEl) ||
                entryEl.ValueKind != JsonValueKind.Array)
                return (null, null, null, raw);

            foreach (var entry in entryEl.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changesEl) ||
                    changesEl.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var change in changesEl.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var valueEl) ||
                        valueEl.ValueKind != JsonValueKind.Object)
                        continue;

                    string? lg = valueEl.TryGetProperty("leadgen_id", out var lgEl) ? Stringify(lgEl) : null;
                    string? fid = valueEl.TryGetProperty("form_id", out var fidEl) ? Stringify(fidEl) : null;
                    string? pid = valueEl.TryGetProperty("page_id", out var pidEl) ? Stringify(pidEl) : null;

                    if (!string.IsNullOrEmpty(lg))
                        return (lg, fid, pid, raw);
                }
            }
            return (null, null, null, raw);
        }
        catch (JsonException)
        {
            return (null, null, null, raw);
        }
    }

    private static string? Stringify(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var n) ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : el.ToString(),
        _ => null
    };
}

/// <summary>Result of <see cref="MetaLeadgenWebhookService.TryEnqueueIntakeJobAsync"/>.</summary>
internal readonly record struct EnqueueAttempt(bool Enqueued);

/// <summary>
/// Webhook result carrier. Accepted => 200 OK. Rejected => specific 4xx with
/// INV-META-* code surfaced in the body envelope. Error => 5xx, caller maps
/// to ErrorResponse.Create.
/// </summary>
public sealed class WebhookOutcome
{
    public int StatusCode { get; init; }
    public string? ErrorCode { get; init; }
    public bool IsAccepted { get; init; }

    public static WebhookOutcome Accepted() => new() { StatusCode = 200, IsAccepted = true };
    public static WebhookOutcome Rejected(int status, string errorCode) => new() { StatusCode = status, ErrorCode = errorCode, IsAccepted = false };
    public static WebhookOutcome Error(int status, string errorCode) => new() { StatusCode = status, ErrorCode = errorCode, IsAccepted = false };
}
