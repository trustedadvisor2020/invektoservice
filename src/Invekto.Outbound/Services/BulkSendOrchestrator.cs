using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Outbound.Services;

/// <summary>
/// FEAT-OBI Phase 0 — CSV bulk send orchestration.
///
/// Thin parent layer over the existing BroadcastOrchestrator:
///   preview  -> parse + normalize + dedup + immutable snapshot (no send)
///   confirm  -> chunk snapshot into &lt;=ChunkSize broadcasts + dispatch (idempotent)
///   status   -> aggregate child broadcast delivery counters
///
/// Safety rails (Codex 2026-06-03): feature-flag + allowlist + hard-cap (re-checked
/// on confirm), recipient snapshot, campaign_id idempotency, chunking, opt-out/consent
/// reuse via BroadcastOrchestrator, tenant_id scoping everywhere. Register as singleton.
/// </summary>
public sealed class BulkSendOrchestrator
{
    private readonly BulkSendRepository _bulkRepo;
    private readonly OutboundRepository _repository;
    private readonly BroadcastOrchestrator _broadcast;
    private readonly CsvRecipientParser _parser;
    private readonly DataListRepository _dataListRepo;
    private readonly BulkSendOptions _options;
    private readonly ContactListOptions _contactListOptions;
    private readonly JsonLinesLogger _logger;

    public BulkSendOrchestrator(
        BulkSendRepository bulkRepo,
        OutboundRepository repository,
        BroadcastOrchestrator broadcast,
        CsvRecipientParser parser,
        DataListRepository dataListRepo,
        BulkSendOptions options,
        ContactListOptions contactListOptions,
        JsonLinesLogger logger)
    {
        _bulkRepo = bulkRepo;
        _repository = repository;
        _broadcast = broadcast;
        _parser = parser;
        _dataListRepo = dataListRepo;
        _options = options;
        _contactListOptions = contactListOptions;
        _logger = logger;
    }

    // Clamp to (0, 1000]: the reused BroadcastOrchestrator hard-rejects >1000 recipients,
    // so an over-configured ChunkSize must never reach it (Codex CQ6/CQ8).
    private const int BroadcastMaxRecipients = 1000;
    private int EffectiveChunkSize =>
        (_options.ChunkSize <= 0 || _options.ChunkSize > BroadcastMaxRecipients)
            ? BroadcastMaxRecipients
            : _options.ChunkSize;

    // ------------------------------------------------------------------
    // PREVIEW
    // ------------------------------------------------------------------
    public async Task<(BulkSendPreviewResponse? response, string? errorCode, string? errorMessage)>
        PreviewAsync(int tenantId, BulkSendPreviewRequest request, CancellationToken ct = default)
    {
        if (!_options.IsTenantAllowed(tenantId))
            return (null, ErrorCodes.BulkSendDisabled, "Bulk send is not enabled for this tenant");

        if (string.IsNullOrWhiteSpace(request.CampaignId) || request.TemplateId <= 0)
            return (null, ErrorCodes.BulkSendInvalidPayload, "campaign_id and template_id are required");

        if (string.IsNullOrWhiteSpace(request.CsvText))
            return (null, ErrorCodes.BulkSendInvalidPayload, "csv_text is required");

        // Early input-size guard — reject oversized payloads before full parse (Codex CQ6).
        if (request.CsvText.Length > _options.MaxInputChars)
            return (null, ErrorCodes.BulkSendInvalidPayload,
                $"Input too large ({request.CsvText.Length} chars > {_options.MaxInputChars}); split into smaller batches");

        // Template must exist/active — early operator feedback (Codex §4F)
        var template = await _repository.GetTemplateByIdAsync(tenantId, request.TemplateId, ct);
        if (template == null)
            return (null, ErrorCodes.OutboundTemplateNotFound, $"Template {request.TemplateId} not found or inactive");

        // Reject if this campaign is already past preview (idempotency guard)
        var existing = await _bulkRepo.GetJobAsync(tenantId, request.CampaignId, ct);
        if (existing != null && existing.Status != "preview_ready")
            return (null, ErrorCodes.BulkSendAlreadyConfirmed,
                $"Campaign '{request.CampaignId}' already {existing.Status}; use a new campaign_id");

        var parsed = _parser.ParseCsv(request.CsvText, request.VariableColumns, _options.MaxInputRows);

        if (parsed.LimitExceeded)
            return (null, ErrorCodes.BulkSendInvalidPayload,
                $"Input exceeds {_options.MaxInputRows} rows; split into smaller batches");

        if (parsed.Valid.Count == 0)
            return (null, ErrorCodes.BulkSendNoValidRecipients,
                $"No sendable phones after normalize/dedup (input={parsed.TotalInput}, invalid={parsed.Invalid}, duplicate={parsed.Duplicate})");

        if (parsed.Valid.Count > _options.MaxRecipientsPerCampaign)
            return (null, ErrorCodes.BulkSendCapExceeded,
                $"Valid recipients {parsed.Valid.Count} exceed pilot cap {_options.MaxRecipientsPerCampaign}");

        var jobId = await _bulkRepo.CreatePreviewJobAsync(
            tenantId, request.CampaignId, request.TemplateId, request.Lang,
            _options.MaxRecipientsPerCampaign,
            parsed.TotalInput, parsed.Valid.Count, parsed.Duplicate, parsed.Invalid,
            parsed.Valid, ct);

        _logger.SystemInfo(
            $"Bulk preview: tenant={tenantId}, campaign={request.CampaignId}, job={jobId}, " +
            $"input={parsed.TotalInput}, valid={parsed.Valid.Count}, dup={parsed.Duplicate}, invalid={parsed.Invalid}");

        return (new BulkSendPreviewResponse
        {
            CampaignId = request.CampaignId,
            Status = "preview_ready",
            HardCap = _options.MaxRecipientsPerCampaign,
            TotalInput = parsed.TotalInput,
            TotalValid = parsed.Valid.Count,
            TotalDuplicate = parsed.Duplicate,
            TotalInvalid = parsed.Invalid,
            Sample = parsed.Valid.Take(_options.PreviewSampleSize).Select(r => r.Phone).ToList(),
            InvalidSamples = parsed.InvalidSamples
        }, null, null);
    }

    // ------------------------------------------------------------------
    // PREVIEW FROM LIST (FEAT-OBI Phase 1A — snapshot a contact list into a send)
    // ------------------------------------------------------------------
    public async Task<(BulkSendPreviewResponse? response, string? errorCode, string? errorMessage)>
        PreviewFromListAsync(int tenantId, PreviewFromListRequest request, CancellationToken ct = default)
    {
        // This path spans BOTH features: sending requires bulk-send enabled AND it reads contact-list
        // data, so the contact-list gate must also pass (the two allowlists are intentionally separate).
        if (!_options.IsTenantAllowed(tenantId))
            return (null, ErrorCodes.BulkSendDisabled, "Bulk send is not enabled for this tenant");
        if (!_contactListOptions.IsTenantAllowed(tenantId))
            return (null, ErrorCodes.ContactListDisabled, "Contact lists are not enabled for this tenant");

        if (string.IsNullOrWhiteSpace(request.CampaignId) || request.TemplateId <= 0 || request.ListId <= 0)
            return (null, ErrorCodes.BulkSendInvalidPayload, "campaign_id, template_id and list_id are required");

        // Every DB call below maps an NpgsqlException (server OR connection/transient) to the
        // actionable INV-OB-053 503, so no preview DB failure escapes as an opaque 500.
        try
        {
            var template = await _repository.GetTemplateByIdAsync(tenantId, request.TemplateId, ct);
            if (template == null)
                return (null, ErrorCodes.OutboundTemplateNotFound, $"Template {request.TemplateId} not found or inactive");

            // Idempotency guard — same as the CSV preview path.
            var existing = await _bulkRepo.GetJobAsync(tenantId, request.CampaignId, ct);
            if (existing != null && existing.Status != "preview_ready")
                return (null, ErrorCodes.BulkSendAlreadyConfirmed,
                    $"Campaign '{request.CampaignId}' already {existing.Status}; use a new campaign_id");

            // List must exist, be ready, and have sendable recipients within the cap.
            var list = await _dataListRepo.GetAsync(tenantId, request.ListId, ct);
            if (list == null)
                return (null, ErrorCodes.ContactListNotFound, $"List {request.ListId} not found");
            if (list.Status != "ready")
                return (null, ErrorCodes.ContactListNotReady, $"List '{list.Name}' is {list.Status}");
            if (list.SendableCount == 0)
                return (null, ErrorCodes.ContactListNoSendable, "List has no sendable recipients");
            if (list.SendableCount > _options.MaxRecipientsPerCampaign)
                return (null, ErrorCodes.ContactListNoSendable,
                    $"List has {list.SendableCount} sendable recipients, over the pilot cap {_options.MaxRecipientsPerCampaign}; reduce the list or raise the cap");

            // The repo re-validates (ready + not deleted + sendable within cap) under a FOR UPDATE lock,
            // so the snapshot is atomic against a concurrent import; the checks above are a fast-fail only.
            var (jobId, snapshotted, snapErrorCode) = await _bulkRepo.CreatePreviewJobFromListAsync(
                tenantId, request.CampaignId, request.TemplateId, request.Lang,
                _options.MaxRecipientsPerCampaign, request.ListId, ct);

            if (snapErrorCode != null)
            {
                var msg = snapErrorCode switch
                {
                    ErrorCodes.ContactListNotFound => $"List {request.ListId} not found",
                    ErrorCodes.ContactListNotReady => $"List '{list.Name}' is not ready (changed during preview)",
                    ErrorCodes.ContactListDbError => "Database error while building the preview; nothing was sent. Please retry.",
                    _ => "List has no sendable recipients within the cap (changed during preview)"
                };
                return (null, snapErrorCode, msg);
            }
            if (snapshotted == 0)
                return (null, ErrorCodes.ContactListNoSendable, "No sendable recipients were snapshotted");

            var sample = await _bulkRepo.GetRecipientPhonesSampleAsync(tenantId, jobId, _options.PreviewSampleSize, ct);

            _logger.SystemInfo(
                $"Bulk preview-from-list: tenant={tenantId}, campaign={request.CampaignId}, job={jobId}, " +
                $"list={request.ListId}, snapshotted={snapshotted}");

            return (new BulkSendPreviewResponse
            {
                CampaignId = request.CampaignId,
                Status = "preview_ready",
                HardCap = _options.MaxRecipientsPerCampaign,
                TotalInput = snapshotted,
                TotalValid = snapshotted,
                TotalDuplicate = 0,
                TotalInvalid = 0,
                Sample = sample,
                InvalidSamples = new List<string>()
            }, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError(
                $"preview-from-list DB failure: tenant={tenantId}, campaign={request.CampaignId}, list={request.ListId}: {ex.Message}");
            return (null, ErrorCodes.ContactListDbError,
                "Database error while building the preview; nothing was sent. Please retry.");
        }
    }

    // ------------------------------------------------------------------
    // CONFIRM (idempotent dispatch)
    // ------------------------------------------------------------------
    public async Task<(BulkSendStatusResponse? response, string? errorCode, string? errorMessage)>
        ConfirmAsync(int tenantId, string campaignId, CancellationToken ct = default)
    {
        if (!_options.IsTenantAllowed(tenantId))
            return (null, ErrorCodes.BulkSendDisabled, "Bulk send is not enabled for this tenant");

        var job = await _bulkRepo.GetJobAsync(tenantId, campaignId, ct);
        if (job == null)
            return (null, ErrorCodes.BulkSendJobNotFound, $"No preview found for campaign '{campaignId}'");

        // Idempotent: a campaign already dispatched returns its current status, never re-sends.
        if (job.Status != "preview_ready")
            return (await BuildStatusAsync(job, ct), null, null);

        // Re-enforce the hard cap at confirm time (config may have been lowered after preview).
        var effectiveCap = Math.Min(job.HardCap, _options.MaxRecipientsPerCampaign);
        if (job.TotalValid > effectiveCap)
            return (null, ErrorCodes.BulkSendCapExceeded,
                $"Snapshot {job.TotalValid} exceeds current cap {effectiveCap}; re-preview with a smaller list");

        // Atomic claim — only one confirm wins the preview_ready -> confirming transition.
        if (!await _bulkRepo.TryClaimForConfirmAsync(tenantId, job.Id, ct))
        {
            var reclaimed = await _bulkRepo.GetJobAsync(tenantId, campaignId, ct);
            if (reclaimed != null) return (await BuildStatusAsync(reclaimed, ct), null, null);
            return (null, ErrorCodes.BulkSendJobNotFound, "Campaign disappeared mid-confirm");
        }

        var broadcastIds = new List<Guid>();
        var totalQueued = 0;
        var totalSkippedOptout = 0;
        var totalSkippedConsent = 0;
        var anyDispatchFailure = false;

        // EVERYTHING after the atomic claim runs inside this guarded region so the lifecycle MUST
        // always resolve (snapshot load + dispatch + finalize). The realistic post-claim failures
        // (request cancellation, DB error) are caught BY TYPE and returned as an actionable
        // INV-OB-045. The `finalized` flag + finally is a non-catch backstop for any OTHER exception
        // type: it persists accumulated state so the job is never stranded in 'confirming', then the
        // original exception propagates unmasked (Codex CQ1/CQ2/CQ9/CQ12). No broad catch here.
        var finalized = false;
        try
        {
            var recipients = await _bulkRepo.GetRecipientsAsync(tenantId, job.Id, ct);
            if (recipients.Count == 0)
            {
                await _bulkRepo.UpdateStatusAsync(tenantId, job.Id, "failed", setCompleted: true, CancellationToken.None);
                finalized = true;
                return (null, ErrorCodes.BulkSendNoValidRecipients, "Snapshot is empty");
            }

            foreach (var chunk in Chunk(recipients, EffectiveChunkSize))
            {
                // Content source per job (chk_bulk_message_source guarantees EXACTLY ONE):
                //   TemplateId set -> template path (CSV/list + a project's gallery_template)
                //   InlineMessageText set -> inline free text (a project's free_text run, SS-A path)
                // Both are pattern-bound to non-nullable locals so neither branch needs a null-forgiving (!).
                BroadcastSendRequest req;
                if (job.TemplateId is int templateId)
                {
                    req = new BroadcastSendRequest { TemplateId = templateId, Lang = job.Lang, Recipients = chunk };
                }
                else if (!string.IsNullOrEmpty(job.InlineMessageText))
                {
                    req = new BroadcastSendRequest { MessageText = job.InlineMessageText, Lang = job.Lang, Recipients = chunk };
                }
                else
                {
                    // Unreachable under the DB CHECK; never dispatch empty content — treat as a chunk failure.
                    anyDispatchFailure = true;
                    _logger.SystemError(
                        $"[{ErrorCodes.BulkSendDispatchFailed}] Bulk job {job.Id} has neither template_id nor inline_message_text");
                    continue;
                }

                // A project run sends from the project's own Cloud API instance (ProjectInstanceId, null for
                // CSV/list jobs -> tenant default). Consumed only on the cxapi route (gated off in prod).
                var (resp, errCode, errMsg) = await _broadcast.CreateBroadcastAsync(tenantId, req, ct, job.ProjectInstanceId);
                if (resp == null)
                {
                    // A chunk with zero sendable recipients (all opt-out/consent-filtered) is not
                    // a hard failure; a template/payload error is.
                    if (errCode == ErrorCodes.OutboundInvalidBroadcastPayload)
                    {
                        _logger.SystemWarn(
                            $"Bulk chunk produced no sendable recipients: campaign={campaignId}, reason={errMsg}");
                        continue;
                    }
                    anyDispatchFailure = true;
                    _logger.SystemError(
                        $"[{ErrorCodes.BulkSendDispatchFailed}] Bulk chunk dispatch failed: " +
                        $"campaign={campaignId}, code={errCode}, msg={errMsg}");
                    continue;
                }

                broadcastIds.Add(resp.BroadcastId);
                totalQueued += resp.Queued;
                totalSkippedOptout += resp.SkippedOptout;
                totalSkippedConsent += resp.SkippedConsent;
            }

            // Normal completion. Messages are in flight -> 'sending'. dispatch_error persists so the
            // terminal roll-up reports completed_with_errors even when a failed chunk left no broadcast
            // row to aggregate (Codex CQ2/CV4). CancellationToken.None: the campaign is already
            // committed, the bookkeeping write must not be abandoned on client disconnect.
            var finalStatus = broadcastIds.Count > 0 ? "sending" : "failed";
            await _bulkRepo.FinalizeDispatchAsync(
                tenantId, job.Id, broadcastIds, totalQueued, totalSkippedOptout, totalSkippedConsent,
                dispatchError: anyDispatchFailure || broadcastIds.Count == 0, status: finalStatus,
                CancellationToken.None);
            if (broadcastIds.Count == 0)
                await _bulkRepo.UpdateStatusAsync(tenantId, job.Id, "failed", setCompleted: true, CancellationToken.None);
            finalized = true;
        }
        catch (OperationCanceledException)
        {
            finalized = true; // recovery handled here; skip the finally backstop
            await PersistInterruptedAsync(tenantId, job.Id, campaignId, broadcastIds, totalQueued, totalSkippedOptout, totalSkippedConsent);
            return (null, ErrorCodes.BulkSendDispatchFailed,
                $"Dispatch interrupted after {broadcastIds.Count} broadcast(s); check campaign status");
        }
        catch (NpgsqlException ex)
        {
            finalized = true; // recovery handled here; skip the finally backstop
            _logger.SystemError(
                $"[{ErrorCodes.BulkSendDispatchFailed}] Bulk confirm DB error post-claim: " +
                $"campaign={campaignId}, dispatched={broadcastIds.Count}, error={ex.Message}");
            await PersistInterruptedAsync(tenantId, job.Id, campaignId, broadcastIds, totalQueued, totalSkippedOptout, totalSkippedConsent);
            return (null, ErrorCodes.BulkSendDispatchFailed,
                $"Dispatch interrupted after {broadcastIds.Count} broadcast(s); check campaign status");
        }
        finally
        {
            // Backstop for any unexpected exception type the typed catches did not handle: persist
            // accumulated state so the job is never stranded in 'confirming'; the exception propagates.
            if (!finalized)
                await PersistInterruptedAsync(tenantId, job.Id, campaignId, broadcastIds, totalQueued, totalSkippedOptout, totalSkippedConsent);
        }

        if (broadcastIds.Count == 0)
            return (null, ErrorCodes.BulkSendDispatchFailed,
                "No broadcasts created (all recipients filtered or dispatch failed)");

        _logger.SystemInfo(
            $"Bulk dispatched: tenant={tenantId}, campaign={campaignId}, broadcasts={broadcastIds.Count}, " +
            $"queued={totalQueued}, skipped_optout={totalSkippedOptout}, skipped_consent={totalSkippedConsent}, " +
            $"dispatch_failures={anyDispatchFailure}");

        var reloaded = await _bulkRepo.GetJobAsync(tenantId, campaignId, CancellationToken.None);
        if (reloaded == null)
            return (null, ErrorCodes.BulkSendJobNotFound, "Campaign disappeared after dispatch");
        return (await BuildStatusAsync(reloaded, CancellationToken.None), null, null);
    }

    // ------------------------------------------------------------------
    // STATUS (aggregate child broadcasts)
    // ------------------------------------------------------------------
    public async Task<(BulkSendStatusResponse? response, string? errorCode)>
        StatusAsync(int tenantId, string campaignId, CancellationToken ct = default)
    {
        var job = await _bulkRepo.GetJobAsync(tenantId, campaignId, ct);
        if (job == null)
            return (null, ErrorCodes.BulkSendJobNotFound);
        return (await BuildStatusAsync(job, ct), null);
    }

    private async Task<BulkSendStatusResponse> BuildStatusAsync(
        BulkSendRepository.JobRecord job, CancellationToken ct)
    {
        var resp = new BulkSendStatusResponse
        {
            CampaignId = job.CampaignId,
            Status = job.Status,
            TotalValid = job.TotalValid,
            TotalQueued = job.TotalQueued,
            TotalSkippedOptout = job.TotalSkippedOptout,
            TotalSkippedConsent = job.TotalSkippedConsent,
            BroadcastCount = job.BroadcastIds.Length,
            CreatedAt = job.CreatedAt,
            ConfirmedAt = job.ConfirmedAt,
            CompletedAt = job.CompletedAt
        };

        if (job.BroadcastIds.Length == 0)
        {
            // SELF-HEAL: a 'confirming' job with no child broadcasts whose claim is stale means a
            // post-claim failure happened AND its recovery write didn't land (e.g. DB was briefly
            // unreachable). Now that we can read/write, resolve it to 'failed' so it is never
            // permanently stranded in 'confirming'. The staleness gate avoids racing a confirm that
            // is still mid-dispatch.
            if (job.Status == "confirming" && job.ConfirmedAt.HasValue &&
                (DateTime.UtcNow - job.ConfirmedAt.Value) > TimeSpan.FromMinutes(_options.ConfirmingStaleMinutes))
            {
                await _bulkRepo.UpdateStatusAsync(job.TenantId, job.Id, "failed", setCompleted: true, ct);
                resp.Status = "failed";
                resp.CompletedAt ??= DateTime.UtcNow;
            }
            return resp;
        }

        // Single aggregate query across all child broadcasts (no N+1).
        var agg = await _bulkRepo.AggregateBroadcastStatusAsync(job.TenantId, job.BroadcastIds, ct);
        resp.Sent = agg.Sent;
        resp.Delivered = agg.Delivered;
        resp.Read = agg.Read;
        resp.Failed = agg.Failed;

        // Roll forward 'sending' AND self-heal a stranded 'confirming' that actually dispatched
        // (its FinalizeDispatchAsync status write didn't land). Idempotent — runs on each status read.
        if (job.Status is "sending" or "confirming")
        {
            if (agg.AllTerminal)
            {
                var terminal = (agg.Failed > 0 || job.DispatchError) ? "completed_with_errors" : "completed";
                await _bulkRepo.UpdateStatusAsync(job.TenantId, job.Id, terminal, setCompleted: true, ct);
                resp.Status = terminal;
                resp.CompletedAt ??= DateTime.UtcNow;
            }
            else if (job.Status == "confirming")
            {
                // Dispatched but still in flight — heal 'confirming' -> 'sending' so the lifecycle resolves.
                await _bulkRepo.UpdateStatusAsync(job.TenantId, job.Id, "sending", setCompleted: false, ct);
                resp.Status = "sending";
            }
        }

        return resp;
    }

    /// <summary>
    /// Best-effort lifecycle recovery after a post-claim failure. Persists accumulated state
    /// (status 'sending' if anything dispatched, else 'failed') with a non-cancellable token.
    /// Guards its own DB writes so a recovery failure can never mask the original exception;
    /// a failed recovery is logged so the stranded 'confirming' is visible to ops (Codex CQ1/CQ2).
    /// </summary>
    private async Task PersistInterruptedAsync(
        int tenantId, long jobId, string campaignId, IReadOnlyList<Guid> broadcastIds,
        int totalQueued, int totalSkippedOptout, int totalSkippedConsent)
    {
        try
        {
            var recoverStatus = broadcastIds.Count > 0 ? "sending" : "failed";
            await _bulkRepo.FinalizeDispatchAsync(
                tenantId, jobId, broadcastIds, totalQueued, totalSkippedOptout, totalSkippedConsent,
                dispatchError: true, status: recoverStatus, CancellationToken.None);
            if (broadcastIds.Count == 0)
                await _bulkRepo.UpdateStatusAsync(tenantId, jobId, "failed", setCompleted: true, CancellationToken.None);
            _logger.SystemError(
                $"[{ErrorCodes.BulkSendDispatchFailed}] Bulk confirm interrupted post-claim: " +
                $"campaign={campaignId}, job={jobId}, dispatched={broadcastIds.Count}");
        }
        catch (NpgsqlException recEx)
        {
            // Cleanup boundary: the recovery writes only touch Postgres, so NpgsqlException is the
            // realistic failure here. Swallow + log (never rethrow) so a recovery-write failure can
            // never mask the original post-claim exception; the log flags the stranded 'confirming'
            // for ops attention.
            _logger.SystemError(
                $"[{ErrorCodes.BulkSendDispatchFailed}] Bulk confirm RECOVERY WRITE FAILED " +
                $"(job left in 'confirming', ops attention required): campaign={campaignId}, job={jobId}, error={recEx.Message}");
        }
    }

    private static IEnumerable<List<BroadcastRecipient>> Chunk(
        List<BroadcastRecipient> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }
}
