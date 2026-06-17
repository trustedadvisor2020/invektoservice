using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Inma;
using Invekto.Shared.Contracts.Inma.Dtos;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Outbound.Services;

/// <summary>
/// FEAT-PROJELER (PKT-14) slice S2 — projects CRUD orchestration (gating + validation),
/// delegating persistence to <see cref="ProjectsRepository"/>. Metadata-only: a project
/// groups a target set under one name; runs + send config + counters are wired later (PR-4).
/// Every DB failure is a typed NpgsqlException catch mapped to INV-OB-071 (503, retryable).
/// Register as singleton.
/// </summary>
public sealed class ProjectsService
{
    private readonly ProjectsRepository _repo;
    private readonly ProjectsOptions _options;
    // FEAT-PROJELER send-exec SS-C: a project run IS a bulk_send_job(project_id). Reuse the bulk machinery —
    // _bulkRepo for the project snapshot + job lookup, _bulkOrch.ConfirmAsync for the (shared) dispatch,
    // _bulkOptions for the send gate + cap. The project domain (gate/eligibility/lifecycle) stays here.
    private readonly BulkSendRepository _bulkRepo;
    private readonly BulkSendOrchestrator _bulkOrch;
    private readonly BulkSendOptions _bulkOptions;
    // PR-4 (HSM run): the CxapiSend allowlist gate (checked BEFORE any vendor call — preview AND
    // confirm), the live cxapi template catalog (ownership/required-param validation, hard-fail)
    // and the WapCRM creds reader (same shared-Postgres read the send path uses, PR-3a precedent).
    private readonly CxapiSendOptions _cxapiOptions;
    private readonly WapCrmTemplateClient _templateClient;
    // FEATURE B (status-pull): the cxapi send client also carries the message-status pull
    // (POST /api/message-status). Same pooled HttpClient + per-request secret pattern as send.
    private readonly WapCrmSendClient _wapCrmSendClient;
    private readonly OutboundRepository _outboundRepo;
    // GR-8 (test send): the SAME inline-text broadcast path every real broadcast uses — test send
    // is a thin wrapper around it (opt-out/consent filters, KVKK disclaimer, route decision all
    // identical; a parallel "test" code path that skips filtering is deliberately NOT built).
    private readonly BroadcastOrchestrator _broadcastOrch;
    private readonly PhoneNormalizer _phoneNormalizer;
    private readonly JsonLinesLogger _logger;

    public ProjectsService(
        ProjectsRepository repo, ProjectsOptions options,
        BulkSendRepository bulkRepo, BulkSendOrchestrator bulkOrch, BulkSendOptions bulkOptions,
        CxapiSendOptions cxapiOptions, WapCrmTemplateClient templateClient, WapCrmSendClient wapCrmSendClient,
        OutboundRepository outboundRepo,
        BroadcastOrchestrator broadcastOrch, PhoneNormalizer phoneNormalizer,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _options = options;
        _bulkRepo = bulkRepo;
        _bulkOrch = bulkOrch;
        _bulkOptions = bulkOptions;
        _cxapiOptions = cxapiOptions;
        _templateClient = templateClient;
        _wapCrmSendClient = wapCrmSendClient;
        _outboundRepo = outboundRepo;
        _broadcastOrch = broadcastOrch;
        _phoneNormalizer = phoneNormalizer;
        _logger = logger;
    }

    private bool Allowed(int tenantId) => _options.IsTenantAllowed(tenantId);

    // ------------------------------------------------------------------
    // Read
    // ------------------------------------------------------------------
    // includeArchived (GR-9): the dashboard's Arşivli filter also lists soft-deleted projects;
    // default false preserves the original list shape for every other caller.
    public async Task<(List<ProjectSummary>? projects, string? errorCode)> ListAsync(
        int tenantId, CancellationToken ct, bool includeArchived = false)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled);
        try
        {
            // Refresh the denormalized roll-up (counters + lifecycle status) from the live broadcasts BEFORE
            // reading, so a fire-and-forget run (operator closed the send modal) is reflected here — without it
            // the list shows the frozen MarkRunning snapshot (status='running', counters 0) until some other
            // endpoint recomputes. Set-based + change-guarded: a steady-state load updates 0 rows.
            await _repo.RecomputeTenantRollupsAsync(tenantId, ct);
            return (await _repo.ListAsync(tenantId, ct, includeArchived), null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"projects list failed (tenant={tenantId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError);
        }
    }

    public async Task<(ProjectDetail? project, string? errorCode, string? message)> GetAsync(
        int tenantId, long projectId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projects not enabled for this tenant");
        try
        {
            var detail = await _repo.GetAsync(tenantId, projectId, ct);
            if (detail == null) return (null, ErrorCodes.ProjectNotFound, $"Project {projectId} not found");
            return (detail, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project get failed (tenant={tenantId}, project={projectId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError, "Project could not be loaded due to a database error; please retry.");
        }
    }

    // ------------------------------------------------------------------
    // Write
    // ------------------------------------------------------------------
    public async Task<(ProjectDetail? project, string? errorCode, string? message)> CreateAsync(
        int tenantId, int createdBy, CreateProjectRequest request, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projects not enabled for this tenant");

        // Inline name validation: string.IsNullOrWhiteSpace flows non-null so no null-forgiving (!) is needed.
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return (null, ErrorCodes.ProjectInvalidPayload, "Project name is required");
        if (name.Length > _options.MaxNameLength)
            return (null, ErrorCodes.ProjectInvalidPayload, $"Project name exceeds {_options.MaxNameLength} characters");

        var (desc, descErr, descMsg) = NormalizeDescription(request.Description);
        if (descErr != null) return (null, descErr, descMsg);

        var (targetIds, targetErr, targetMsg) = NormalizeTargets(request.TargetListIds ?? new List<long>());
        if (targetErr != null) return (null, targetErr, targetMsg);

        var (sendConfig, _, cfgErr, cfgMsg) = BuildSendConfig(
            request.TemplateKind, request.InstanceId, request.WaTemplateId, request.TemplateLanguage, request.ParamMapping,
            request.ContentMode, request.OutboundTemplateId, request.PlainTextBody);
        if (cfgErr != null) return (null, cfgErr, cfgMsg);

        try
        {
            var (chErr, chMsg) = await ValidateSendConfigOwnershipAsync(tenantId, sendConfig, ct);
            if (chErr != null) return (null, chErr, chMsg);

            var result = await _repo.CreateAsync(tenantId, createdBy, name, desc, targetIds, sendConfig, ct);
            if (result.NameConflict) return (null, ErrorCodes.ProjectNameConflict, $"A project named '{name}' already exists");
            if (result.InvalidTargets) return (null, ErrorCodes.ProjectInvalidTarget, "One or more selected lists do not exist for this account");
            // Detail is read in-tx and is non-null on success; a null here means the write did not commit.
            if (result.Detail == null) return (null, ErrorCodes.ProjectDbError, "Project could not be created due to a database error; please retry.");

            _logger.SystemInfo($"project created: tenant={tenantId}, project={result.ProjectId}, targets={targetIds.Length}");
            return (result.Detail, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project create failed (tenant={tenantId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError, "Project could not be created due to a database error; please retry.");
        }
    }

    public async Task<(ProjectDetail? project, string? errorCode, string? message)> UpdateAsync(
        int tenantId, long projectId, UpdateProjectRequest request, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projects not enabled for this tenant");

        // Partial update: validate name/description only when supplied.
        string? name = null, desc = null;
        if (request.Name != null)
        {
            var trimmed = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return (null, ErrorCodes.ProjectInvalidPayload, "Project name cannot be blank");
            if (trimmed.Length > _options.MaxNameLength)
                return (null, ErrorCodes.ProjectInvalidPayload, $"Project name exceeds {_options.MaxNameLength} characters");
            name = trimmed;
        }
        if (request.Description != null)
        {
            var trimmed = request.Description.Trim();
            if (trimmed.Length > _options.MaxDescriptionLength)
                return (null, ErrorCodes.ProjectInvalidPayload, $"Description exceeds {_options.MaxDescriptionLength} characters");
            desc = trimmed; // empty string clears the visible description text
        }

        long[]? targetIds = null;
        if (request.TargetListIds != null)
        {
            var (ids, targetErr, targetMsg) = NormalizeTargets(request.TargetListIds);
            if (targetErr != null) return (null, targetErr, targetMsg);
            targetIds = ids;
        }

        var (sendConfig, setConfig, cfgErr, cfgMsg) = BuildSendConfig(
            request.TemplateKind, request.InstanceId, request.WaTemplateId, request.TemplateLanguage, request.ParamMapping,
            request.ContentMode, request.OutboundTemplateId, request.PlainTextBody);
        if (cfgErr != null) return (null, cfgErr, cfgMsg);

        if (name == null && desc == null && targetIds == null && !setConfig)
            return (null, ErrorCodes.ProjectInvalidPayload, "No changes supplied");

        try
        {
            var (chErr, chMsg) = await ValidateSendConfigOwnershipAsync(tenantId, sendConfig, ct);
            if (chErr != null) return (null, chErr, chMsg);

            var result = await _repo.UpdateAsync(tenantId, projectId, name, desc, targetIds, sendConfig, setConfig, ct);
            if (result.NameConflict) return (null, ErrorCodes.ProjectNameConflict, "Another project already uses that name");
            if (!result.Found) return (null, ErrorCodes.ProjectNotFound, $"Project {projectId} not found");
            if (result.InvalidTargets) return (null, ErrorCodes.ProjectInvalidTarget, "One or more selected lists do not exist for this account");
            // Detail is read in-tx and is non-null on success; a null here means the write did not commit.
            if (result.Detail == null) return (null, ErrorCodes.ProjectDbError, "Project could not be updated due to a database error; please retry.");

            _logger.SystemInfo($"project updated: tenant={tenantId}, project={projectId}, targetsReplaced={targetIds != null}");
            return (result.Detail, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project update failed (tenant={tenantId}, project={projectId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError, "Project could not be updated due to a database error; please retry.");
        }
    }

    public async Task<(bool ok, string? errorCode)> ArchiveAsync(int tenantId, long projectId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (false, ErrorCodes.ProjectDisabled);
        try
        {
            var ok = await _repo.ArchiveAsync(tenantId, projectId, ct);
            if (ok) _logger.SystemInfo($"project archived: tenant={tenantId}, project={projectId}");
            return ok ? (true, null) : (false, ErrorCodes.ProjectNotFound);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project archive failed (tenant={tenantId}, project={projectId}): {ex.Message}");
            return (false, ErrorCodes.ProjectDbError);
        }
    }

    // ------------------------------------------------------------------
    // Run dispatch (FEAT-PROJELER send-exec SS-C) — a run is a bulk_send_job(project_id),
    // reusing the bulk preview -> confirm -> status machinery.
    // ------------------------------------------------------------------
    /// <summary>
    /// Preview a project run: snapshot the DISTINCT sendable audience across the project's target lists and
    /// return the count + a sample. Dual-gated (Projects feature + BulkSend send capability) so it stays
    /// inert until a tenant is allowlisted for sending. Dispatches PLAIN-TEXT content only (SS-C): an HSM
    /// (wapcrm_template) project is REJECTED (INV-OB-076, PR-4), a content-less project is rejected
    /// (INV-OB-074), a target-less project is rejected (INV-OB-075). The frontend supplies one campaign_id
    /// per Gönder flow (idempotency key); a campaign already confirmed cannot be re-previewed.
    /// </summary>
    public async Task<(BulkSendPreviewResponse? response, string? errorCode, string? message)> PreviewSendAsync(
        int tenantId, long projectId, string campaignId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projeler bu hesap için etkin değil.");
        if (!_bulkOptions.IsTenantAllowed(tenantId)) return (null, ErrorCodes.BulkSendDisabled, "Gönderim bu hesap için etkin değil.");
        if (string.IsNullOrWhiteSpace(campaignId)) return (null, ErrorCodes.ProjectInvalidPayload, "campaign_id zorunlu.");

        try
        {
            var detail = await _repo.GetAsync(tenantId, projectId, ct);
            if (detail == null) return (null, ErrorCodes.ProjectNotFound, $"Proje {projectId} bulunamadı.");

            // One active run per project (SS-D): refuse a NEW send while a run is in flight or paused — the
            // operator must complete, resume or cancel it first. (Re-previewing the SAME campaign stays
            // idempotent: a draft/completed/cancelled project passes here, and the campaign-already-confirmed
            // guard below returns the existing preview.)
            if (detail.Project.Status is ProjectStatuses.Running or ProjectStatuses.Paused)
                return (null, ErrorCodes.ProjectRunInProgress,
                    "Bu projede aktif bir gönderim var. Önce onu tamamlayın, sürdürün veya iptal edin.");

            // Eligibility — TWO content families (Q decision 2026-06-10, PR-4):
            //   plain_text  -> templateId/inlineText carriers (SS-C path, unchanged)
            //   wapcrm_template (HSM) -> validated against the LIVE cxapi catalog, snapshotted immutable.
            int? templateId = null;
            string? inlineText = null;
            BulkSendRepository.HsmSnapshotInput? hsmSnapshot = null;
            if (detail.Project.TemplateKind == ProjectTemplateKinds.WapcrmTemplate)
            {
                // hsm is null exactly when an error code was returned — bind non-null without (!).
                var (hsm, hsmErr, hsmMsg) = ResolveHsmContent(detail.Project);
                if (hsm is null)
                    return (null, hsmErr ?? ErrorCodes.ProjectInvalidSendConfig, hsmMsg ?? "Şablon yapılandırması geçersiz.");
                var (validated, valErr, valMsg) = await ValidateHsmRunAsync(tenantId, hsm, ct);
                if (valErr != null) return (null, valErr, valMsg);
                hsmSnapshot = new BulkSendRepository.HsmSnapshotInput
                {
                    InstanceId = hsm.InstanceId,
                    WaTemplateId = hsm.WaTemplateId,
                    TemplateLanguage = hsm.TemplateLanguage,
                    ParamMappingJson = hsm.ParamMappingJson,
                    TextParamColumns = hsm.TextParamColumns,
                    TextParamLiterals = hsm.TextParamLiterals,
                    RequiredParamKeys = validated.RequiredParamKeys
                };
            }
            else
            {
                var (tid, itext, eligErr, eligMsg) = ResolveSendContent(detail.Project);
                if (eligErr != null) return (null, eligErr, eligMsg);
                templateId = tid;
                inlineText = itext;
            }

            var listIds = detail.Targets.Select(t => t.DataListId).Distinct().ToArray();
            if (listIds.Length == 0) return (null, ErrorCodes.ProjectNoTargets, "Projenin hedef listesi yok. Önce en az bir liste ekleyin.");

            // Idempotency guard (same as the bulk paths): a confirmed campaign cannot be re-previewed.
            var existing = await _bulkRepo.GetJobAsync(tenantId, campaignId, ct);
            if (existing != null && existing.Status != "preview_ready")
                return (null, ErrorCodes.BulkSendAlreadyConfirmed, $"'{campaignId}' kampanyası zaten {existing.Status}; yeni bir gönderim başlatın.");

            var snap = await _bulkRepo.CreatePreviewJobFromProjectAsync(
                tenantId, campaignId, projectId, templateId, inlineText, listIds, _bulkOptions.MaxRecipientsPerCampaign, ct, hsmSnapshot);
            if (snap.ErrorCode != null)
            {
                var msg = snap.ErrorCode switch
                {
                    ErrorCodes.ProjectNoTargets => "Projenin hedef listesi yok. Önce en az bir liste ekleyin.",
                    ErrorCodes.ContactListNotFound => "Bir hedef liste bulunamadı (silinmiş olabilir). Liste seçimini güncelleyin.",
                    ErrorCodes.ContactListNotReady => "Bir hedef liste henüz hazır değil. İçe aktarımın bitmesini bekleyin.",
                    ErrorCodes.ContactListNoSendable => $"Hedef listelerde gönderilebilir alıcı yok veya üst sınır ({_bulkOptions.MaxRecipientsPerCampaign}) aşıldı.",
                    ErrorCodes.ContactListDbError => "Önizleme oluşturulurken veritabanı hatası; hiçbir şey gönderilmedi. Lütfen tekrar deneyin.",
                    _ => "Önizleme oluşturulamadı."
                };
                return (null, snap.ErrorCode, msg);
            }
            if (snap.Snapshotted == 0)
            {
                // HSM: every distinct recipient was excluded for a missing required param — tell the
                // operator WHICH params are empty instead of a generic "no recipients".
                if (snap.SkippedMissingParams > 0)
                    return (null, ErrorCodes.HsmRequiredParamUnmapped,
                        $"Tüm alıcılarda zorunlu şablon parametresi eksik ({FormatSkippedByParam(snap.SkippedParamsJson)}). Liste verisini tamamlayın.");
                return (null, ErrorCodes.ContactListNoSendable, "Gönderilebilir alıcı yok.");
            }

            var sample = await _bulkRepo.GetRecipientPhonesSampleAsync(tenantId, snap.JobId, _bulkOptions.PreviewSampleSize, ct);
            _logger.SystemInfo(
                $"project preview: tenant={tenantId}, project={projectId}, campaign={campaignId}, job={snap.JobId}, " +
                $"snapshotted={snap.Snapshotted}{(hsmSnapshot != null ? $", hsm={hsmSnapshot.WaTemplateId}, skipped_missing_params={snap.SkippedMissingParams}" : "")}");

            return (new BulkSendPreviewResponse
            {
                CampaignId = campaignId,
                Status = "preview_ready",
                HardCap = _bulkOptions.MaxRecipientsPerCampaign,
                TotalInput = snap.Snapshotted + snap.SkippedMissingParams,
                TotalValid = snap.Snapshotted,
                TotalDuplicate = 0,
                TotalInvalid = 0,
                Sample = sample,
                InvalidSamples = new List<string>(),
                SkippedParams = BuildSkippedParamsInfo(snap)
            }, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project preview failed (tenant={tenantId}, project={projectId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError, "Veritabanı hatası nedeniyle önizleme oluşturulamadı. Lütfen tekrar deneyin.");
        }
    }

    /// <summary>
    /// Confirm a previewed project run: dispatch via the shared bulk ConfirmAsync (idempotent, atomic claim),
    /// then mark the project 'running' when a dispatch actually happened. The campaign's job must belong to
    /// THIS project (defense: campaign_id is client-supplied). Re-confirming an already-finished run returns
    /// its status without re-marking running.
    /// </summary>
    public async Task<(BulkSendStatusResponse? response, string? errorCode, string? message)> ConfirmSendAsync(
        int tenantId, long projectId, string campaignId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projeler bu hesap için etkin değil.");
        // Same dual gate as preview — explicit here too so a preview_ready job cannot be confirmed after the
        // BulkSend allowlist is pulled (prod-inert invariant; BulkSendOrchestrator.ConfirmAsync also enforces it).
        if (!_bulkOptions.IsTenantAllowed(tenantId)) return (null, ErrorCodes.BulkSendDisabled, "Gönderim bu hesap için etkin değil.");
        if (string.IsNullOrWhiteSpace(campaignId)) return (null, ErrorCodes.ProjectInvalidPayload, "campaign_id zorunlu.");

        try
        {
            // RE-VALIDATE at confirm time (TOCTOU: the project may have been archived or its content
            // switched since preview). An archived/removed project loads as null -> reject BEFORE any
            // dispatch; a content-less project is rejected -> never silently dispatched.
            var detail = await _repo.GetAsync(tenantId, projectId, ct);
            if (detail == null) return (null, ErrorCodes.ProjectNotFound, $"Proje {projectId} bulunamadı veya arşivlenmiş.");

            var projectIsHsm = detail.Project.TemplateKind == ProjectTemplateKinds.WapcrmTemplate;
            if (!projectIsHsm)
            {
                var (_, _, eligErr, eligMsg) = ResolveSendContent(detail.Project);
                if (eligErr != null) return (null, eligErr, eligMsg);
            }

            var job = await _bulkRepo.GetJobAsync(tenantId, campaignId, ct);
            if (job == null) return (null, ErrorCodes.BulkSendJobNotFound, $"'{campaignId}' için önizleme bulunamadı.");
            if (job.ProjectId != projectId)
                return (null, ErrorCodes.ProjectInvalidPayload, "Kampanya bu projeye ait değil.");

            // PR-4: a FRESH dispatch must match the project's CURRENT content family — an HSM preview
            // cannot dispatch after the project was switched to plain text (or vice versa); the stale
            // snapshot would send content the operator no longer intends. Re-preview re-snapshots.
            var jobIsHsm = !string.IsNullOrEmpty(job.WaTemplateId);
            if (job.Status == "preview_ready" && jobIsHsm != projectIsHsm)
                return (null, ErrorCodes.ProjectInvalidPayload,
                    "Proje içeriği önizlemeden sonra değişti; yeniden önizleme alın.");

            // PR-4: HSM second live validation at confirm (Codex plan-consult TOCTOU finding): the
            // template may have been deleted/renamed or its requiredInputs changed since preview.
            // Validated against the JOB SNAPSHOT (immutable run config), not the live project row.
            // Only a fresh dispatch revalidates — an idempotent re-confirm of an already-dispatched
            // job just returns its live status below. Drift seconds AFTER this check is the accepted
            // residual: those sends fail per-message with provider 621/622 (persisted typed).
            if (job.Status == "preview_ready" && jobIsHsm)
            {
                // hsm is null exactly when an error code was returned — bind non-null without (!).
                var (hsm, hsmErr, hsmMsg) = ResolveHsmContentFromJob(job);
                if (hsm is null)
                    return (null, hsmErr ?? ErrorCodes.ProjectInvalidSendConfig, hsmMsg ?? "Önizleme kaydı geçersiz; yeniden önizleme alın.");
                var (_, valErr, valMsg) = await ValidateHsmRunAsync(tenantId, hsm, ct);
                if (valErr != null) return (null, valErr, valMsg);
            }

            // One active run per project (SS-D): a preview_ready job is a NEW dispatch — reject it while a run
            // is already in flight or paused (INV-OB-080); the operator must complete, resume or cancel that
            // run first. This targets a SECOND dispatch precisely and does NOT block the idempotent re-confirm
            // of an already-dispatched (sending/completed) job (job.Status != preview_ready), which must keep
            // returning its live status.
            if (job.Status == "preview_ready" && detail.Project.Status is ProjectStatuses.Running or ProjectStatuses.Paused)
                return (null, ErrorCodes.ProjectRunInProgress,
                    "Bu projede aktif bir gönderim var. Önce onu tamamlayın, sürdürün veya iptal edin.");

            // Lifecycle + ATOMIC archive gate for a FRESH dispatch: a preview_ready job is the only one that
            // will actually dispatch. Claim the project 'running' FIRST, gated on archived_at IS NULL — if it
            // affects 0 rows the project was archived since the reload above, so abort BEFORE _bulkOrch
            // dispatches (closes the reload->dispatch TOCTOU; nothing is sent for an archived project). An
            // already-confirmed/sending/completed job is an idempotent re-confirm: do NOT touch the project
            // status (must not reopen a finished run) and let ConfirmAsync return the live status.
            if (job.Status == "preview_ready" && !await _repo.SetRunningAsync(tenantId, projectId, ct))
                return (null, ErrorCodes.ProjectNotFound, "Proje arşivlenmiş; gönderim iptal edildi.");

            var (status, errCode, errMsg) = await _bulkOrch.ConfirmAsync(tenantId, campaignId, ct);
            if (errCode != null) return (null, errCode, errMsg);

            _logger.SystemInfo($"project confirm: tenant={tenantId}, project={projectId}, campaign={campaignId}, status={status?.Status}");
            return (status, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project confirm failed (tenant={tenantId}, project={projectId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError, "Veritabanı hatası nedeniyle gönderim onaylanamadı. Lütfen tekrar deneyin.");
        }
    }

    // ------------------------------------------------------------------
    // Test send (GR-8) — ONE plain-text message to ONE number from the project modal.
    // ------------------------------------------------------------------
    // Per-tenant fixed-window throttle: test send is a REAL send surface, so it is capped hard
    // (Codex plan-consult PR-001). In-memory is intentional — Outbound runs as a single NSSM
    // instance per deployment; a restart resetting the window is acceptable for an anti-abuse cap.
    private const int TestSendPerMinuteCap = 5;
    private static readonly ConcurrentDictionary<int, (long Window, int Count)> _testSendWindows = new();

    private static bool TestSendThrottled(int tenantId)
    {
        var window = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
        var entry = _testSendWindows.AddOrUpdate(
            tenantId,
            static (_, w) => (w, 1),
            static (_, cur, w) => cur.Window == w ? (cur.Window, cur.Count + 1) : (w, 1),
            window);
        return entry.Count > TestSendPerMinuteCap;
    }

    // HSM test bounds: a template's required text inputs are few; these caps only stop abuse.
    private const int TestSendMaxTemplateParams = 32;
    private const int TestSendMaxTemplateParamValueLength = 1024;

    /// <summary>
    /// Send ONE test message to ONE number ("Test Gönder" in the project modal; not bound to a
    /// saved project so create-mode content can be tested too). EXACTLY ONE content source:
    /// plain text (message_text) or an approved HSM template (wa_template_id + resolved params —
    /// the REAL template wire model per wapcrm-api-integration-guide §3.2; Q 2026-06-10).
    /// Same dual gate as a project run (Projects + BulkSend) keeps this surface inert for
    /// non-allowlisted tenants; then a per-tenant throttle, PhoneNormalizer validation and the
    /// SHARED BroadcastOrchestrator path — opt-out/consent filtering and the route decision are
    /// identical to real sends (an HSM test additionally requires the CxapiSend allowlist, which
    /// the orchestrator enforces with INV-OB-085; no parallel "test" code path). Every reject is
    /// a typed INV code; there is no silent no-op.
    /// </summary>
    public async Task<(BroadcastSendResponse? response, string? errorCode, string? message)> TestSendAsync(
        int tenantId, ProjectTestSendRequest request, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projeler bu hesap için etkin değil.");
        if (!_bulkOptions.IsTenantAllowed(tenantId)) return (null, ErrorCodes.BulkSendDisabled, "Gönderim bu hesap için etkin değil.");
        if (TestSendThrottled(tenantId))
            return (null, ErrorCodes.ProjectTestSendThrottled,
                $"Çok sık test gönderimi (dakikada en fazla {TestSendPerMinuteCap}). Biraz bekleyip tekrar deneyin.");

        var normalized = _phoneNormalizer.Normalize(request.Phone);
        if (normalized == null)
            return (null, ErrorCodes.ProjectTestSendInvalidPayload, "Test numarası geçersiz. Numarayı +90… formatında girin.");

        var text = request.MessageText?.Trim() ?? string.Empty;
        var slug = request.WaTemplateId?.Trim() ?? string.Empty;
        if (text.Length == 0 && slug.Length == 0)
            return (null, ErrorCodes.ProjectTestSendInvalidPayload, "Test içeriği boş. Önce bir şablon veya içerik seçin.");
        if (text.Length > 0 && slug.Length > 0)
            return (null, ErrorCodes.ProjectTestSendInvalidPayload, "Tek içerik kaynağı gönderin: düz metin VEYA onaylı şablon.");

        BroadcastSendRequest broadcastRequest;
        int? overrideInstanceId = null;
        if (slug.Length > 0)
        {
            // HSM template test — the wire carries template{templateId, parameters, headerMedia}.
            if (request.InstanceId is not int inst || inst <= 0)
                return (null, ErrorCodes.ProjectTestSendInvalidPayload, "Şablon testi için bir WhatsApp kanalı seçin.");
            if (!await _repo.IsCloudApiInstanceOwnedAsync(tenantId, inst, ct))
                return (null, ErrorCodes.ProjectTestSendInvalidPayload, "Seçilen kanal bu hesaba ait bir Cloud API hattı değil.");

            var paramCount = request.TemplateParams?.Count ?? 0;
            if (paramCount > TestSendMaxTemplateParams)
                return (null, ErrorCodes.ProjectTestSendInvalidPayload, $"Şablon parametre sayısı sınırı aşıyor (en fazla {TestSendMaxTemplateParams}).");
            if (request.TemplateParams != null)
            {
                foreach (var (key, value) in request.TemplateParams)
                {
                    if (string.IsNullOrWhiteSpace(key) || value == null || value.Length > TestSendMaxTemplateParamValueLength)
                        return (null, ErrorCodes.ProjectTestSendInvalidPayload, "Şablon parametreleri geçersiz (boş anahtar veya çok uzun değer).");
                }
            }

            var mediaUrl = request.TemplateHeaderMediaUrl?.Trim();
            if (!string.IsNullOrEmpty(mediaUrl)
                && (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var mediaUri)
                    || (mediaUri.Scheme != Uri.UriSchemeHttp && mediaUri.Scheme != Uri.UriSchemeHttps)))
                return (null, ErrorCodes.ProjectTestSendInvalidPayload, "Şablon medyası için herkese açık bir http(s) URL girin.");

            overrideInstanceId = inst;
            broadcastRequest = new BroadcastSendRequest
            {
                WaTemplateId = slug,
                TemplateHeaderMediaUrl = string.IsNullOrEmpty(mediaUrl) ? null : mediaUrl,
                Recipients = new List<BroadcastRecipient>
                {
                    new()
                    {
                        Phone = normalized,
                        Variables = request.TemplateParams is { Count: > 0 }
                            ? new Dictionary<string, string>(request.TemplateParams)
                            : null
                    }
                }
            };
        }
        else
        {
            if (text.Length > PlainTextBodyMaxLength)
                return (null, ErrorCodes.ProjectTestSendInvalidPayload, $"Test mesajı {PlainTextBodyMaxLength} karakteri aşıyor.");
            broadcastRequest = new BroadcastSendRequest
            {
                MessageText = text,
                Recipients = new List<BroadcastRecipient> { new() { Phone = normalized } }
            };
        }

        try
        {
            var (resp, errCode, errMsg) = await _broadcastOrch.CreateBroadcastAsync(
                tenantId, broadcastRequest, ct, overrideInstanceId);
            if (resp == null)
            {
                // Single recipient: "no valid recipients" from the orchestrator means the number
                // was filtered (opt-out / marketing consent) — name that cause for the operator.
                var msg = errCode == ErrorCodes.OutboundInvalidBroadcastPayload
                    ? "Mesaj gönderilmedi: numara filtrelendi (opt-out / pazarlama izni yok)."
                    : errMsg;
                _logger.SystemWarn($"project test-send rejected: tenant={tenantId}, phone={normalized}, kind={(slug.Length > 0 ? "hsm" : "plain")}, code={errCode}");
                return (null, errCode, msg);
            }

            // Audit line (Codex plan-consult PR-001): who tested, to where, with what outcome.
            _logger.SystemInfo(
                $"project test-send: tenant={tenantId}, phone={normalized}, kind={(slug.Length > 0 ? $"hsm:{slug}" : "plain")}, " +
                $"broadcast={resp.BroadcastId}, queued={resp.Queued}, skipped_optout={resp.SkippedOptout}, skipped_consent={resp.SkippedConsent}");
            return (resp, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project test-send failed (tenant={tenantId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError, "Veritabanı hatası nedeniyle test mesajı gönderilemedi. Lütfen tekrar deneyin.");
        }
    }

    /// <summary>
    /// Project run status: recompute the roll-up counters + lifecycle live from the project's runs
    /// (idempotent), then return the fresh project detail (counters + status the UI renders).
    /// </summary>
    public async Task<(ProjectDetail? detail, string? errorCode, string? message)> GetSendStatusAsync(
        int tenantId, long projectId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projeler bu hesap için etkin değil.");
        try
        {
            await _repo.RecomputeRollupAsync(tenantId, projectId, ct);
            var detail = await _repo.GetAsync(tenantId, projectId, ct);
            if (detail == null) return (null, ErrorCodes.ProjectNotFound, $"Proje {projectId} bulunamadı.");
            return (detail, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project status failed (tenant={tenantId}, project={projectId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError, "Veritabanı hatası nedeniyle durum okunamadı. Lütfen tekrar deneyin.");
        }
    }

    // ------------------------------------------------------------------
    // Rapor (delivery report) drawer — runs dropdown + paged recipient table + single-recipient resend.
    // Read paths are gated (Projects) + 404-ownership-probed so a foreign/archived project leaks nothing.
    // ------------------------------------------------------------------

    /// <summary>Report run dropdown: the project's runs (newest first) with their live partition counters.</summary>
    public async Task<(List<ProjectRunDto>? runs, string? errorCode, string? message)> GetReportRunsAsync(
        int tenantId, long projectId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projeler bu hesap için etkin değil.");
        try
        {
            var detail = await _repo.GetAsync(tenantId, projectId, ct); // 404 ownership probe
            if (detail == null) return (null, ErrorCodes.ProjectNotFound, $"Proje {projectId} bulunamadı.");
            return (await _repo.GetRunsAsync(tenantId, projectId, ct), null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project report runs failed (tenant={tenantId}, project={projectId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError, "Veritabanı hatası; lütfen tekrar deneyin.");
        }
    }

    /// <summary>Report recipient table: server-paged status rows, optional run (campaign_id) + phone-substring filters.</summary>
    public async Task<(ProjectRecipientsPage? page, string? errorCode, string? message)> GetReportRecipientsAsync(
        int tenantId, long projectId, string? campaignId, string? search, int page, int pageSize, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projeler bu hesap için etkin değil.");
        try
        {
            var detail = await _repo.GetAsync(tenantId, projectId, ct); // 404 ownership probe
            if (detail == null) return (null, ErrorCodes.ProjectNotFound, $"Proje {projectId} bulunamadı.");
            return (await _repo.GetRecipientsAsync(tenantId, projectId, campaignId, search, page, pageSize, ct), null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project report recipients failed (tenant={tenantId}, project={projectId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError, "Veritabanı hatası; lütfen tekrar deneyin.");
        }
    }

    /// <summary>
    /// Resend ONE undelivered recipient (failed/ambiguous) of a project: re-queue the message for a real
    /// re-send via its PRESERVED route (the worker picks it up — no new send-orchestration path), then
    /// recompute the project roll-up so its status reflects the re-opened run. Returns the fresh detail.
    /// INV-OB-090 if the message is not resend-eligible / not this project's (no existence leak).
    /// </summary>
    public async Task<(ProjectDetail? detail, string? errorCode, string? message)> ResendAsync(
        int tenantId, long projectId, long messageId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projeler bu hesap için etkin değil.");
        try
        {
            var broadcastId = await _repo.RequeueForResendAsync(tenantId, projectId, messageId, ct);
            if (broadcastId == null)
                return (null, ErrorCodes.ProjectResendNotEligible,
                    "Bu mesaj yeniden gönderilemez (yalnız başarısız/belirsiz durumdakiler yeniden gönderilebilir).");

            await _repo.RecomputeRollupAsync(tenantId, projectId, ct);
            var detail = await _repo.GetAsync(tenantId, projectId, ct);
            if (detail == null) return (null, ErrorCodes.ProjectNotFound, $"Proje {projectId} bulunamadı.");
            _logger.SystemInfo(
                $"project resend re-queued: tenant={tenantId}, project={projectId}, message={messageId}, broadcast={broadcastId}");
            return (detail, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project resend failed (tenant={tenantId}, project={projectId}, message={messageId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError, "Veritabanı hatası; lütfen tekrar deneyin.");
        }
    }

    /// <summary>
    /// Bulk resend: re-queue EVERY undelivered (failed/ambiguous) recipient of a project in one transaction,
    /// each via its PRESERVED route (the worker re-sends — no new send-orchestration path), then recompute the
    /// roll-up so the project status reflects the re-opened runs. Returns how many rows were re-queued; 0 is a
    /// successful no-op (nothing was eligible), NOT an error. Same eligibility/ownership scoping as the
    /// single-recipient <see cref="ResendAsync"/> (bulk_send_jobs.project_id), so nothing foreign is touched.
    /// </summary>
    public async Task<(ProjectResendBulkResultDto? result, string? errorCode, string? message)> ResendAllAsync(
        int tenantId, long projectId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projeler bu hesap için etkin değil.");
        try
        {
            var detail = await _repo.GetAsync(tenantId, projectId, ct); // 404 ownership probe
            if (detail == null) return (null, ErrorCodes.ProjectNotFound, $"Proje {projectId} bulunamadı.");

            var requeued = await _repo.RequeueAllForResendAsync(tenantId, projectId, ct);
            if (requeued > 0)
                await _repo.RecomputeRollupAsync(tenantId, projectId, ct);

            _logger.SystemInfo(
                $"project bulk resend re-queued: tenant={tenantId}, project={projectId}, requeued={requeued}");
            return (new ProjectResendBulkResultDto { Requeued = requeued }, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project bulk resend failed (tenant={tenantId}, project={projectId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError, "Veritabanı hatası; lütfen tekrar deneyin.");
        }
    }

    /// <summary>FEATURE B (status-pull): cap on the number of wamids one "Durumu Yenile" pulls a live status for.</summary>
    private const int StatusPullCap = 200;

    /// <summary>FEATURE B (batch status-pull): wamids per /api/message-status call (provider cap is 100).</summary>
    private const int MessageStatusBatchSize = 100;

    /// <summary>
    /// FEATURE B (status-pull): for up to <see cref="StatusPullCap"/> of a project's wamid-bearing, non-terminal
    /// recipients (optionally scoped to one run), pull the LIVE cxapi message-status, map it via
    /// <c>WapCrmAckMapping.MapStatus</c>, and apply it through the idempotent, monotonic
    /// <c>ApplyDeliveryStatusAsync</c> (the SAME path the ack webhook uses). The ack webhook stays the primary
    /// mechanism; this is a manual complement ("Durumu Yenile"). Gated by the SAME CxapiSend allowlist as send
    /// (INV-OB-094 when off / no creds — ZERO vendor calls, P0-3 inert). Best-effort per message: a
    /// transport/timeout/no-status pull is simply not counted. Returns {checked, updated}; recomputes the
    /// roll-up only when something advanced.
    /// </summary>
    public async Task<(ProjectStatusPullResultDto? result, string? errorCode, string? message)> RefreshRunStatusAsync(
        int tenantId, long projectId, string? campaignId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projeler bu hesap için etkin değil.");

        // Vendor-call gate: same allowlist as the send path (P0-3 inert for non-allowlisted tenants).
        if (!_cxapiOptions.IsTenantAllowed(tenantId))
            return (null, ErrorCodes.ProjectStatusPullNotEnabled, "Canlı durum sorgulama bu hesapta henüz açık değil.");

        List<(long MessageId, string ExternalMessageId, int? InstanceId)> pending;
        int? defaultInstanceId;
        string secretKey; // captured non-null below (the guard's IsNullOrWhiteSpace narrows it)
        try
        {
            var detail = await _repo.GetAsync(tenantId, projectId, ct); // 404 ownership probe
            if (detail == null) return (null, ErrorCodes.ProjectNotFound, $"Proje {projectId} bulunamadı.");

            var wap = await _outboundRepo.GetWapCrmSettingsAsync(tenantId, ct);
            if (wap == null || string.IsNullOrWhiteSpace(wap.SecretKey) || wap.UserId <= 0)
                return (null, ErrorCodes.ProjectStatusPullNotEnabled,
                    "WapCRM bağlantı ayarları eksik; canlı durum sorgulanamıyor.");
            secretKey = wap.SecretKey;            // non-null here (IsNullOrWhiteSpace guard above)
            defaultInstanceId = wap.InstanceId;   // tenant default instance (fallback when a row has none)

            pending = await _repo.GetPendingForStatusPullAsync(tenantId, projectId, campaignId, StatusPullCap, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project status-pull read failed (tenant={tenantId}, project={projectId}): {ex.Message}");
            return (null, ErrorCodes.ProjectStatusPullDbError, "Veritabanı hatası; lütfen tekrar deneyin.");
        }

        var updated = 0;
        var checkedCount = 0;

        // Group the pending wamids by their RESOLVED instance (the wamid's own captured instance, else the
        // tenant default) — /api/message-status is per-instance and batched (≤100 ids/call). Rows with no
        // resolvable instance are skipped (cannot be queried). One batch pull replaces N singular calls.
        var byInstance = new Dictionary<int, List<string>>();
        foreach (var row in pending)
        {
            var instanceId = row.InstanceId is int iid && iid > 0 ? iid : defaultInstanceId ?? 0;
            if (instanceId <= 0) continue;
            if (!byInstance.TryGetValue(instanceId, out var wamids))
            {
                wamids = new List<string>();
                byInstance[instanceId] = wamids;
            }
            wamids.Add(row.ExternalMessageId);
        }

        foreach (var (instanceId, wamids) in byInstance)
        {
            for (var offset = 0; offset < wamids.Count; offset += MessageStatusBatchSize)
            {
                ct.ThrowIfCancellationRequested();
                var chunk = wamids.GetRange(offset, Math.Min(MessageStatusBatchSize, wamids.Count - offset));
                checkedCount += chunk.Count;

                var pull = await _wapCrmSendClient.GetMessageStatusesAsync(instanceId, chunk, secretKey, tenantId, ct);
                if (!pull.Ok) continue; // whole chunk failed (transport/timeout/rejection) — best-effort, re-pullable

                foreach (var entry in pull.Messages)
                {
                    // found:false / no numeric status (Pending/Deleted/unknown) → nothing to apply (ack-path parity).
                    if (!entry.Found || entry.MessageStatus is not int rawStatus) continue;
                    var mapped = WapCrmAckMapping.MapStatus(rawStatus);
                    if (mapped == null) continue; // 0=Pending / 5=Deleted / unknown → ignore (same as the ack path)

                    try
                    {
                        var apply = await _outboundRepo.ApplyDeliveryStatusAsync(tenantId, entry.MessageId, mapped, failedReason: null, ct: ct);
                        if (apply.Applied) updated++;
                    }
                    catch (NpgsqlException ex)
                    {
                        _logger.SystemError($"project status-pull apply failed (tenant={tenantId}, project={projectId}, message={entry.MessageId}): {ex.Message}");
                        return (null, ErrorCodes.ProjectStatusPullDbError, "Veritabanı hatası; lütfen tekrar deneyin.");
                    }
                }
            }
        }

        if (updated > 0)
        {
            try { await _repo.RecomputeRollupAsync(tenantId, projectId, ct); }
            catch (NpgsqlException ex)
            {
                // The statuses ARE applied (idempotent); only the roll-up refresh failed. Surface a DB error so
                // the operator retries — a re-pull re-applies nothing new but does refresh the roll-up.
                _logger.SystemError($"project status-pull rollup failed (tenant={tenantId}, project={projectId}): {ex.Message}");
                return (null, ErrorCodes.ProjectStatusPullDbError, "Durumlar güncellendi ancak özet yenilenemedi; tekrar deneyin.");
            }
        }

        _logger.SystemInfo(
            $"project status-pull done: tenant={tenantId}, project={projectId}, campaign={campaignId ?? "-"}, checked={checkedCount}, updated={updated}");
        return (new ProjectStatusPullResultDto { Checked = checkedCount, Updated = updated }, null, null);
    }

    // ------------------------------------------------------------------
    // Run lifecycle — pause / resume / cancel (SS-D). Each is gated (Projects) + atomic in the repo;
    // on success the rollup is recomputed so the returned ProjectDetail reflects the new status/counters.
    // ------------------------------------------------------------------
    /// <summary>Pause a running project's send (queued -> paused; in-flight not recalled). INV-OB-077 if not running.</summary>
    public Task<(ProjectDetail? detail, string? errorCode, string? message)> PauseSendAsync(int tenantId, long projectId, CancellationToken ct)
        => RunLifecycleAsync(tenantId, projectId, _repo.PauseRunAsync,
            ErrorCodes.ProjectRunNotPausable, "Bu proje çalışmıyor; duraklatılamaz.", "pause", ct);

    /// <summary>Resume a paused project's send (paused -> queued). INV-OB-078 if not paused.</summary>
    public Task<(ProjectDetail? detail, string? errorCode, string? message)> ResumeSendAsync(int tenantId, long projectId, CancellationToken ct)
        => RunLifecycleAsync(tenantId, projectId, _repo.ResumeRunAsync,
            ErrorCodes.ProjectRunNotResumable, "Bu proje duraklatılmış değil; sürdürülemez.", "resume", ct);

    /// <summary>Cancel a running/paused project's send (remaining queued+paused -> cancelled). INV-OB-079 if neither.</summary>
    public Task<(ProjectDetail? detail, string? errorCode, string? message)> CancelSendAsync(int tenantId, long projectId, CancellationToken ct)
        => RunLifecycleAsync(tenantId, projectId, _repo.CancelRunAsync,
            ErrorCodes.ProjectRunNotCancellable, "Bu proje çalışmıyor veya duraklatılmış değil; iptal edilemez.", "cancel", ct);

    /// <summary>
    /// Shared pause/resume/cancel flow: gate -> run the atomic repo op -> map its typed result (not-found ->
    /// INV-OB-068, wrong-state -> the op's conflict code, both never a silent no-op) -> on success recompute
    /// the rollup and return the fresh ProjectDetail (the same shape GetSendStatusAsync returns). Any DB
    /// transport error is a typed INV-OB-071 (503, retry-safe; the repo op is a single rolled-back transaction).
    /// </summary>
    private async Task<(ProjectDetail? detail, string? errorCode, string? message)> RunLifecycleAsync(
        int tenantId, long projectId,
        Func<int, long, CancellationToken, Task<ProjectsRepository.RunLifecycleResult>> op,
        string stateConflictCode, string stateConflictMessage, string opLabel, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled, "Projeler bu hesap için etkin değil.");
        try
        {
            var result = await op(tenantId, projectId, ct);
            if (!result.Found) return (null, ErrorCodes.ProjectNotFound, $"Proje {projectId} bulunamadı.");
            if (result.StateConflict) return (null, stateConflictCode, stateConflictMessage);

            await _repo.RecomputeRollupAsync(tenantId, projectId, ct);
            var detail = await _repo.GetAsync(tenantId, projectId, ct);
            if (detail == null) return (null, ErrorCodes.ProjectNotFound, $"Proje {projectId} bulunamadı.");

            _logger.SystemInfo($"project {opLabel}: tenant={tenantId}, project={projectId}, affected={result.AffectedMessages}");
            return (detail, null, null);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"project {opLabel} failed (tenant={tenantId}, project={projectId}): {ex.Message}");
            return (null, ErrorCodes.ProjectDbError, "Veritabanı hatası nedeniyle işlem tamamlanamadı. Lütfen tekrar deneyin.");
        }
    }

    /// <summary>
    /// Resolve a project's dispatchable plain_text content into a bulk job's content carrier — EXACTLY ONE of
    /// (templateId for gallery_template, inlineText for free_text). An HSM (wapcrm_template) project is
    /// rejected (PR-4); a non-plain_text or content-less project is rejected. Never silently defaults.
    /// </summary>
    private static (int? templateId, string? inlineText, string? errorCode, string? message) ResolveSendContent(ProjectSummary p)
    {
        if (p.TemplateKind == ProjectTemplateKinds.WapcrmTemplate)
            return (null, null, ErrorCodes.ProjectHsmSendNotSupported, "Onaylı şablon (HSM) gönderimi henüz aktif değil. Bu özellik yakında gelecek.");
        if (p.TemplateKind != ProjectTemplateKinds.PlainText)
            return (null, null, ErrorCodes.ProjectNoContent, "Projede gönderilecek içerik tanımlı değil (galeri şablonu veya serbest metin seçin).");

        if (p.ContentMode == ProjectContentModes.GalleryTemplate)
        {
            if (p.OutboundTemplateId is int tid && tid > 0)
                return (tid, null, null, null);
            return (null, null, ErrorCodes.ProjectNoContent, "Galeri şablonu seçilmemiş.");
        }
        if (p.ContentMode == ProjectContentModes.FreeText)
        {
            var body = p.PlainTextBody?.Trim();
            if (!string.IsNullOrEmpty(body))
                return (null, body, null, null);
            return (null, null, ErrorCodes.ProjectNoContent, "Serbest metin boş.");
        }
        return (null, null, ErrorCodes.ProjectNoContent, "Projede gönderilecek içerik tanımlı değil (galeri şablonu veya serbest metin seçin).");
    }

    // ------------------------------------------------------------------
    // PR-4 — HSM (approved-template) run content + live-catalog validation
    // ------------------------------------------------------------------

    /// <summary>An HSM run's parsed config: snapshot fields + the operator's param bindings.</summary>
    private sealed class HsmRunContent
    {
        public required int InstanceId { get; init; }
        public required string WaTemplateId { get; init; }
        public string? TemplateLanguage { get; init; }
        public required string ParamMappingJson { get; init; }
        public required List<KeyValuePair<string, string>> TextParamColumns { get; init; }
        public required List<KeyValuePair<string, string>> TextParamLiterals { get; init; }
        /// <summary>HEADER media literal URL (source='literal' media mapping entry), if any.</summary>
        public string? HeaderMediaUrl { get; init; }
    }

    private sealed class HsmValidationOutcome
    {
        public required IReadOnlySet<string> RequiredParamKeys { get; init; }
    }

    /// <summary>Resolve an HSM project's run content from the LIVE project row (preview path).</summary>
    private static (HsmRunContent? hsm, string? errorCode, string? message) ResolveHsmContent(ProjectSummary p)
    {
        if (p.InstanceId is not int instanceId || instanceId <= 0)
            return (null, ErrorCodes.ProjectInvalidSendConfig, "Projenin gönderim hattı (instance) seçilmemiş.");
        var waTemplateId = p.WaTemplateId?.Trim();
        if (string.IsNullOrEmpty(waTemplateId))
            return (null, ErrorCodes.ProjectInvalidSendConfig, "Projede onaylı şablon seçilmemiş.");

        // param_mapping is stored opaque (BuildSendConfig only checked object/array + size); parse the
        // SPA shape here, fail-loud on anything unreadable — never dispatch with a guessed mapping.
        var mappingJson = p.ParamMapping is JsonElement el && el.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null
            ? el.GetRawText()
            : "[]";
        return ParseHsmMapping(instanceId, waTemplateId, p.TemplateLanguage, mappingJson);
    }

    /// <summary>Resolve an HSM run's content from the JOB SNAPSHOT (confirm-time revalidation — immutable run config).</summary>
    private static (HsmRunContent? hsm, string? errorCode, string? message) ResolveHsmContentFromJob(BulkSendRepository.JobRecord job)
    {
        if (job.InstanceId is not int instanceId || instanceId <= 0 || string.IsNullOrEmpty(job.WaTemplateId))
            return (null, ErrorCodes.ProjectInvalidSendConfig, "Önizleme kaydı eksik şablon yapılandırması taşıyor; yeniden önizleme alın.");
        return ParseHsmMapping(instanceId, job.WaTemplateId, job.TemplateLanguage, job.ParamMappingJson ?? "[]");
    }

    /// <summary>
    /// Parses the SPA param_mapping shape (camelCase entries: kind/location/paramKey/mediaType/source/
    /// column/value) into typed bindings. Column-sourced text params must reference a REAL list column
    /// (<see cref="BulkSendRepository.IsHsmListColumn"/> whitelist — also the SQL-injection boundary for
    /// the snapshot SQL). Unknown/garbled entries are a typed reject, never silently ignored.
    /// </summary>
    private static (HsmRunContent? hsm, string? errorCode, string? message) ParseHsmMapping(
        int instanceId, string waTemplateId, string? templateLanguage, string mappingJson)
    {
        var textColumns = new List<KeyValuePair<string, string>>();
        var textLiterals = new List<KeyValuePair<string, string>>();
        string? headerMediaUrl = null;
        try
        {
            using var doc = JsonDocument.Parse(mappingJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in doc.RootElement.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object)
                        return (null, ErrorCodes.ProjectInvalidSendConfig, "Şablon parametre eşlemesi bozuk; projeyi düzenleyip yeniden kaydedin.");

                    var kind = GetString(entry, "kind");
                    var source = GetString(entry, "source");
                    var paramKey = GetString(entry, "paramKey")?.Trim();

                    // STRICT value domains (Codex chunk-3 iter0+iter1): kind ∈ {text, media, null/empty
                    // legacy}, source ∈ {column, literal, null/empty legacy} — validated for EVERY entry
                    // (media included) BEFORE any branch consumes it. Anything else is a malformed/garbled
                    // mapping — typed reject, NEVER coerced into a guessed binding and dispatched.
                    var kindIsMedia = string.Equals(kind, "media", StringComparison.OrdinalIgnoreCase);
                    var kindIsText = string.IsNullOrEmpty(kind) || string.Equals(kind, "text", StringComparison.OrdinalIgnoreCase);
                    if (!kindIsMedia && !kindIsText)
                        return (null, ErrorCodes.ProjectInvalidSendConfig, $"Şablon eşlemesinde bilinmeyen tür: '{kind}'. Projeyi düzenleyip yeniden kaydedin.");

                    var sourceIsColumn = string.Equals(source, "column", StringComparison.OrdinalIgnoreCase);
                    var sourceIsLiteral = string.IsNullOrEmpty(source) || string.Equals(source, "literal", StringComparison.OrdinalIgnoreCase);
                    if (!sourceIsColumn && !sourceIsLiteral)
                        return (null, ErrorCodes.ProjectInvalidSendConfig, $"Şablon eşlemesinde bilinmeyen kaynak: '{source}'. Projeyi düzenleyip yeniden kaydedin.");

                    if (kindIsMedia)
                    {
                        // Media mapping: a LITERAL public URL — a column-sourced media is not expressible
                        // on the wire and the SPA never produces it; reject rather than read the wrong field.
                        if (sourceIsColumn)
                            return (null, ErrorCodes.ProjectInvalidSendConfig, "Medya eşlemesi kolondan beslenemez; medya için sabit bir URL girin.");
                        // Multiple media entries are not expressible on the wire (single headerMedia) —
                        // reject rather than pick one silently.
                        var url = GetString(entry, "value")?.Trim();
                        if (string.IsNullOrEmpty(url)) continue; // unset media row (operator never filled it)
                        if (headerMediaUrl != null)
                            return (null, ErrorCodes.ProjectInvalidSendConfig, "Birden fazla medya eşlemesi var; tek başlık medyası destekleniyor.");
                        headerMediaUrl = url;
                        continue;
                    }

                    if (string.IsNullOrEmpty(paramKey))
                        continue; // placeholder row the operator never bound — required-coverage check decides

                    if (sourceIsColumn)
                    {
                        var column = GetString(entry, "column")?.Trim();
                        if (string.IsNullOrEmpty(column))
                            continue; // unbound dropdown — required-coverage check decides
                        if (!BulkSendRepository.IsHsmListColumn(column))
                            return (null, ErrorCodes.ProjectInvalidSendConfig, $"'{column}' geçerli bir liste kolonu değil; eşlemeyi güncelleyin.");
                        textColumns.Add(new KeyValuePair<string, string>(paramKey, column));
                    }
                    else
                    {
                        // 'literal' (or legacy rows without source): a constant value for every recipient.
                        var value = GetString(entry, "value");
                        if (string.IsNullOrWhiteSpace(value))
                            continue; // empty literal — required-coverage check decides
                        textLiterals.Add(new KeyValuePair<string, string>(paramKey, value));
                    }
                }
            }
            else if (doc.RootElement.ValueKind != JsonValueKind.Null)
            {
                // An object-shaped mapping has no defined param semantics — fail loud (never guess).
                return (null, ErrorCodes.ProjectInvalidSendConfig, "Şablon parametre eşlemesi beklenen biçimde değil; projeyi düzenleyip yeniden kaydedin.");
            }
        }
        catch (JsonException)
        {
            return (null, ErrorCodes.ProjectInvalidSendConfig, "Şablon parametre eşlemesi okunamadı; projeyi düzenleyip yeniden kaydedin.");
        }

        return (new HsmRunContent
        {
            InstanceId = instanceId,
            WaTemplateId = waTemplateId,
            TemplateLanguage = templateLanguage,
            ParamMappingJson = mappingJson,
            TextParamColumns = textColumns,
            TextParamLiterals = textLiterals,
            HeaderMediaUrl = headerMediaUrl
        }, null, null);

        static string? GetString(JsonElement obj, string prop)
            => obj.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    /// <summary>
    /// PR-4: the LIVE cxapi catalog validation — runs at preview AND again immediately before a fresh
    /// confirm (TOCTOU). Order is deliberate: (1) CxapiSend allowlist FIRST so a non-allowlisted tenant
    /// never triggers a vendor call (INV-OB-085; empty allowlist = the whole HSM path is prod-inert),
    /// (2) creds, (3) live template list (HARD FAIL when unreachable — INV-OB-084, no stale/sessiz
    /// fallback), (4) slug ownership (INV-OB-081), (5) requiredInputs coverage: dynamic BUTTON params
    /// rejected (INV-OB-083, G13.3 deferred), required text params must be bound to a column or a
    /// non-empty literal (INV-OB-082), a required HEADER media needs an https literal URL (INV-OB-082).
    /// </summary>
    private async Task<(HsmValidationOutcome validated, string? errorCode, string? message)> ValidateHsmRunAsync(
        int tenantId, HsmRunContent hsm, CancellationToken ct)
    {
        var none = new HsmValidationOutcome { RequiredParamKeys = new HashSet<string>(StringComparer.Ordinal) };

        if (!_cxapiOptions.IsTenantAllowed(tenantId))
            return (none, ErrorCodes.HsmSendNotAllowlisted, "Onaylı şablon gönderimi bu hesapta henüz açık değil.");

        var wap = await _outboundRepo.GetWapCrmSettingsAsync(tenantId, ct);
        if (wap == null || string.IsNullOrWhiteSpace(wap.SecretKey) || wap.UserId <= 0)
            return (none, ErrorCodes.CxapiRouteMisconfigured,
                "WapCRM bağlantı ayarları eksik (instance/secret/user). Hesap ayarlarını tamamlayın.");

        var list = await _templateClient.ListTemplatesAsync(wap.SecretKey, hsm.InstanceId, tenantId, ct);
        if (list.Outcome != WapCrmTemplateListOutcome.Success)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.HsmTemplateCatalogUnreachable}] HSM template catalog fetch failed: tenant={tenantId}, instance={hsm.InstanceId}, outcome={list.Outcome}, http={list.HttpStatusCode}, providerCode={list.ProviderStatusCode}");
            return (none, ErrorCodes.HsmTemplateCatalogUnreachable,
                "Şablon doğrulaması yapılamadı (sağlayıcıya ulaşılamadı). Birazdan tekrar deneyin.");
        }

        var template = list.Templates.FirstOrDefault(t =>
            string.Equals(t.TemplateId?.Trim(), hsm.WaTemplateId, StringComparison.Ordinal));
        if (template == null)
            return (none, ErrorCodes.HsmTemplateNotFound,
                "Seçilen şablon bu hatta bulunamadı. Şablon listesini yenileyip tekrar seçin.");

        var requiredKeys = new HashSet<string>(StringComparer.Ordinal);
        var boundColumnKeys = new HashSet<string>(hsm.TextParamColumns.Select(b => b.Key), StringComparer.Ordinal);
        var boundLiteralKeys = new HashSet<string>(hsm.TextParamLiterals.Select(b => b.Key), StringComparer.Ordinal);

        foreach (var input in template.RequiredInputs ?? new List<WapCrmRequiredInputDto>())
        {
            var location = input.Location?.Trim();
            var kind = input.Kind?.Trim();

            if (string.Equals(location, "BUTTON", StringComparison.OrdinalIgnoreCase))
                return (none, ErrorCodes.HsmDynamicButtonUnsupported,
                    "Dinamik buton parametresi içeren şablonlar henüz desteklenmiyor.");

            if (string.Equals(kind, "text", StringComparison.OrdinalIgnoreCase))
            {
                var key = input.ParamKey?.Trim();
                if (string.IsNullOrEmpty(key))
                    return (none, ErrorCodes.HsmDynamicButtonUnsupported,
                        "Şablon, adlandırılmamış zorunlu bir parametre istiyor; bu şablon türü desteklenmiyor.");
                if (!boundColumnKeys.Contains(key) && !boundLiteralKeys.Contains(key))
                    return (none, ErrorCodes.HsmRequiredParamUnmapped,
                        $"Şablonun zorunlu parametresi eşlenmemiş: {key}. Proje düzenleyicisinde kolon eşlemesini tamamlayın.");
                requiredKeys.Add(key);
            }
            else if (string.Equals(kind, "media", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(location, "HEADER", StringComparison.OrdinalIgnoreCase))
                    return (none, ErrorCodes.HsmDynamicButtonUnsupported,
                        "Şablonun gerektirdiği medya konumu desteklenmiyor.");
                if (string.IsNullOrEmpty(hsm.HeaderMediaUrl)
                    || !Uri.TryCreate(hsm.HeaderMediaUrl, UriKind.Absolute, out var mediaUri)
                    || mediaUri.Scheme != Uri.UriSchemeHttps)
                    return (none, ErrorCodes.HsmRequiredParamUnmapped,
                        "Şablonun başlık medyası için geçerli bir https URL eşlemesi gerekli.");
            }
            else
            {
                return (none, ErrorCodes.HsmDynamicButtonUnsupported,
                    "Şablonun gerektirdiği parametre türü desteklenmiyor.");
            }
        }

        return (new HsmValidationOutcome { RequiredParamKeys = requiredKeys }, null, null);
    }

    /// <summary>
    /// PR-4: extract the HEADER media literal URL from a snapshotted param_mapping JSON.
    /// Used by BulkSendOrchestrator at dispatch (the mapping-shape knowledge lives HERE).
    /// Returns null on a missing/garbled mapping — dispatch-time validation already ran
    /// (preview + pre-confirm); a template that REQUIRES media never reaches dispatch unbound.
    /// </summary>
    internal static string? ExtractHeaderMediaUrl(string? mappingJson)
    {
        if (string.IsNullOrEmpty(mappingJson)) return null;
        var (hsm, _, _) = ParseHsmMapping(instanceId: 1, waTemplateId: "x", templateLanguage: null, mappingJson);
        return hsm?.HeaderMediaUrl;
    }

    /// <summary>Operator-facing "param: n" summary from the preview_skipped_params audit JSON.</summary>
    private static string FormatSkippedByParam(string? skippedJson)
    {
        if (string.IsNullOrEmpty(skippedJson)) return "eksik parametre";
        try
        {
            using var doc = JsonDocument.Parse(skippedJson);
            if (doc.RootElement.TryGetProperty("by_param", out var bp) && bp.ValueKind == JsonValueKind.Object)
            {
                var parts = bp.EnumerateObject().Select(p => $"{p.Name}: {p.Value}").ToList();
                if (parts.Count > 0) return string.Join(", ", parts);
            }
        }
        catch (JsonException) { /* audit json is server-built; fall through to the generic label */ }
        return "eksik parametre";
    }

    /// <summary>Preview-response skip block (count + by_param) from the snapshot result; null for plain runs / zero skips.</summary>
    private static BulkSkippedParamsInfo? BuildSkippedParamsInfo(BulkSendRepository.ProjectSnapshotResult snap)
    {
        if (snap.SkippedMissingParams <= 0 || string.IsNullOrEmpty(snap.SkippedParamsJson)) return null;
        var info = new BulkSkippedParamsInfo { Count = snap.SkippedMissingParams };
        try
        {
            using var doc = JsonDocument.Parse(snap.SkippedParamsJson);
            if (doc.RootElement.TryGetProperty("by_param", out var bp) && bp.ValueKind == JsonValueKind.Object)
                info.ByParam = bp.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());
        }
        catch (JsonException) { /* count alone still renders */ }
        return info;
    }

    // ------------------------------------------------------------------
    // Validation helpers
    // ------------------------------------------------------------------
    /// <summary>Trim + length-check a description for create; empty becomes null (no description stored).</summary>
    private (string? desc, string? errorCode, string? message) NormalizeDescription(string? raw)
    {
        var desc = raw?.Trim();
        if (desc != null && desc.Length > _options.MaxDescriptionLength)
            return (null, ErrorCodes.ProjectInvalidPayload, $"Description exceeds {_options.MaxDescriptionLength} characters");
        return (string.IsNullOrEmpty(desc) ? null : desc, null, null);
    }

    /// <summary>Reject non-positive ids (no silent drop), dedup (first-seen order preserved), enforce the cap.</summary>
    private (long[] ids, string? errorCode, string? message) NormalizeTargets(List<long> raw)
    {
        var seen = new HashSet<long>();
        var ordered = new List<long>();
        foreach (var id in raw)
        {
            if (id <= 0)
                return (Array.Empty<long>(), ErrorCodes.ProjectInvalidPayload, $"Invalid target list id: {id}");
            if (seen.Add(id)) ordered.Add(id);
        }
        if (ordered.Count > _options.MaxTargetsPerProject)
            return (Array.Empty<long>(), ErrorCodes.ProjectInvalidPayload,
                $"A project can target at most {_options.MaxTargetsPerProject} lists ({ordered.Count} given)");
        return (ordered.ToArray(), null, null);
    }

    // Schema-driven caps (migration 057: wa_template_id VARCHAR(128), template_language VARCHAR(8)).
    private const int WaTemplateIdMaxLength = 128;
    private const int TemplateLanguageMaxLength = 8;
    private const int ParamMappingMaxBytes = 16 * 1024; // defensive cap on stored JSONB size
    // plain_text_body is TEXT (migration 059); cap defensively at the WhatsApp text body limit (~4096).
    private const int PlainTextBodyMaxLength = 4096;

    /// <summary>
    /// Validate + normalize the optional send-config block (channel + content/template). <c>template_kind</c>
    /// is the DRIVER: null => leave config untouched (setConfig=false). When set, the WHOLE block is validated
    /// and returned for an authoritative write (setConfig=true): both kinds require a channel (instance_id&gt;0).
    /// 'plain_text' additionally requires a CONTENT choice picked in settings (Q decision 2026-06-09):
    /// content_mode='gallery_template' => a non-zero outbound_template_id (a Şablon Galerisi row), OR
    /// content_mode='free_text' => a non-empty plain_text_body (≤4096) — exactly one carrier; the other and
    /// all wa_template fields are CLEARED. 'wapcrm_template' (HSM, PR-4) requires a non-empty wa_template_id
    /// (+ optional language/param_mapping) and CLEARS the plain_text content fields. Any inconsistency returns
    /// a typed INV-OB-073 with a user-facing message — never a silent default. Ownership of the chosen channel
    /// AND gallery template is checked async in <see cref="ValidateSendConfigOwnershipAsync"/>.
    /// </summary>
    private (ProjectsRepository.ProjectSendConfigInput? config, bool setConfig, string? errorCode, string? message)
        BuildSendConfig(string? kind, int? instanceId, string? waTemplateId, string? templateLanguage, JsonElement? paramMapping,
            string? contentMode, int? outboundTemplateId, string? plainTextBody)
    {
        if (kind == null)
            return (null, false, null, null); // config block omitted -> leave existing config untouched

        if (!ProjectTemplateKinds.IsValid(kind))
            return (null, false, ErrorCodes.ProjectInvalidSendConfig, "Geçersiz mesaj türü (plain_text veya wapcrm_template).");

        if (instanceId is not > 0)
            return (null, false, ErrorCodes.ProjectInvalidSendConfig, "Gönderim için bir WhatsApp kanalı (hat) seçin.");

        if (kind == ProjectTemplateKinds.PlainText)
        {
            // plain_text: channel + a content choice (gallery template OR free text), made in settings.
            // wa_template fields are cleared (those belong to the HSM kind).
            var cmode = contentMode?.Trim();
            if (!ProjectContentModes.IsValid(cmode))
                return (null, false, ErrorCodes.ProjectInvalidSendConfig,
                    "Düz metin için içerik türü seçin: galeri şablonu veya serbest metin.");

            if (cmode == ProjectContentModes.GalleryTemplate)
            {
                if (outboundTemplateId is not > 0)
                    return (null, false, ErrorCodes.ProjectInvalidSendConfig, "Galeri şablonu için bir şablon seçin.");
                return (new ProjectsRepository.ProjectSendConfigInput
                {
                    InstanceId = instanceId,
                    TemplateKind = ProjectTemplateKinds.PlainText,
                    WaTemplateId = null,
                    TemplateLanguage = null,
                    ParamMappingJson = null,
                    ContentMode = ProjectContentModes.GalleryTemplate,
                    OutboundTemplateId = outboundTemplateId,
                    PlainTextBody = null
                }, true, null, null);
            }

            // free_text
            var body = plainTextBody?.Trim();
            if (string.IsNullOrEmpty(body))
                return (null, false, ErrorCodes.ProjectInvalidSendConfig, "Serbest metin için bir mesaj yazın.");
            if (body.Length > PlainTextBodyMaxLength)
                return (null, false, ErrorCodes.ProjectInvalidSendConfig, $"Mesaj metni {PlainTextBodyMaxLength} karakteri aşıyor.");
            return (new ProjectsRepository.ProjectSendConfigInput
            {
                InstanceId = instanceId,
                TemplateKind = ProjectTemplateKinds.PlainText,
                WaTemplateId = null,
                TemplateLanguage = null,
                ParamMappingJson = null,
                ContentMode = ProjectContentModes.FreeText,
                OutboundTemplateId = null,
                PlainTextBody = body
            }, true, null, null);
        }

        // wapcrm_template
        var tmpl = waTemplateId?.Trim();
        if (string.IsNullOrEmpty(tmpl))
            return (null, false, ErrorCodes.ProjectInvalidSendConfig, "Şablon türü için bir onaylı şablon seçin.");
        if (tmpl.Length > WaTemplateIdMaxLength)
            return (null, false, ErrorCodes.ProjectInvalidSendConfig, $"Şablon kimliği {WaTemplateIdMaxLength} karakteri aşıyor.");

        var lang = templateLanguage?.Trim();
        if (lang != null && lang.Length > TemplateLanguageMaxLength)
            return (null, false, ErrorCodes.ProjectInvalidSendConfig, $"Şablon dili {TemplateLanguageMaxLength} karakteri aşıyor.");
        if (string.IsNullOrEmpty(lang)) lang = null;

        string? pmJson = null;
        if (paramMapping is { } pm && pm.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            if (pm.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                return (null, false, ErrorCodes.ProjectInvalidSendConfig, "Şablon parametreleri bir JSON nesnesi/dizisi olmalı.");
            pmJson = JsonSerializer.Serialize(pm);
            if (Encoding.UTF8.GetByteCount(pmJson) > ParamMappingMaxBytes)
                return (null, false, ErrorCodes.ProjectInvalidSendConfig, "Şablon parametreleri çok büyük.");
        }

        return (new ProjectsRepository.ProjectSendConfigInput
        {
            InstanceId = instanceId,
            TemplateKind = ProjectTemplateKinds.WapcrmTemplate,
            WaTemplateId = tmpl,
            TemplateLanguage = lang,
            ParamMappingJson = pmJson,
            // HSM kind has no plain_text content: clear the content carriers (the consistency CHECK requires
            // content_mode NULL => both carriers NULL).
            ContentMode = null,
            OutboundTemplateId = null,
            PlainTextBody = null
        }, true, null, null);
    }

    /// <summary>
    /// Server-side authorization of the chosen send config: the channel instance_id MUST be a Cloud API line
    /// (instance_type = 1) owned by this tenant, AND — when content_mode='gallery_template' — the
    /// outbound_template_id MUST be an active template owned by this tenant. The SPA filters both client-side,
    /// but a direct API caller could submit any positive id, so this is enforced authoritatively here (Codex
    /// CQ5/CQ9 — closes the cross-tenant / non-Cloud channel + foreign-template persistence gap). No config
    /// set => nothing to authorize.
    /// </summary>
    private async Task<(string? errorCode, string? message)> ValidateSendConfigOwnershipAsync(
        int tenantId, ProjectsRepository.ProjectSendConfigInput? config, CancellationToken ct)
    {
        if (config?.InstanceId is int inst)
        {
            if (!await _repo.IsCloudApiInstanceOwnedAsync(tenantId, inst, ct))
                return (ErrorCodes.ProjectInvalidSendConfig, "Seçilen WhatsApp kanalı bu hesaba ait değil veya bir Cloud API hattı değil.");
        }
        if (config?.OutboundTemplateId is int tid)
        {
            if (!await _repo.IsOutboundTemplateOwnedActiveAsync(tenantId, tid, ct))
                return (ErrorCodes.ProjectInvalidSendConfig, "Seçilen galeri şablonu bu hesaba ait değil veya artık etkin değil.");
        }
        return (null, null);
    }
}
