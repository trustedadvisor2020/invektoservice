using System.Text;
using System.Text.Json;
using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
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
    private readonly JsonLinesLogger _logger;

    public ProjectsService(
        ProjectsRepository repo, ProjectsOptions options,
        BulkSendRepository bulkRepo, BulkSendOrchestrator bulkOrch, BulkSendOptions bulkOptions,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _options = options;
        _bulkRepo = bulkRepo;
        _bulkOrch = bulkOrch;
        _bulkOptions = bulkOptions;
        _logger = logger;
    }

    private bool Allowed(int tenantId) => _options.IsTenantAllowed(tenantId);

    // ------------------------------------------------------------------
    // Read
    // ------------------------------------------------------------------
    public async Task<(List<ProjectSummary>? projects, string? errorCode)> ListAsync(int tenantId, CancellationToken ct)
    {
        if (!Allowed(tenantId)) return (null, ErrorCodes.ProjectDisabled);
        try { return (await _repo.ListAsync(tenantId, ct), null); }
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

            // Eligibility: a run dispatches plain_text content only. HSM send is PR-4.
            var (templateId, inlineText, eligErr, eligMsg) = ResolveSendContent(detail.Project);
            if (eligErr != null) return (null, eligErr, eligMsg);

            var listIds = detail.Targets.Select(t => t.DataListId).Distinct().ToArray();
            if (listIds.Length == 0) return (null, ErrorCodes.ProjectNoTargets, "Projenin hedef listesi yok. Önce en az bir liste ekleyin.");

            // Idempotency guard (same as the bulk paths): a confirmed campaign cannot be re-previewed.
            var existing = await _bulkRepo.GetJobAsync(tenantId, campaignId, ct);
            if (existing != null && existing.Status != "preview_ready")
                return (null, ErrorCodes.BulkSendAlreadyConfirmed, $"'{campaignId}' kampanyası zaten {existing.Status}; yeni bir gönderim başlatın.");

            var (jobId, snapshotted, snapErr) = await _bulkRepo.CreatePreviewJobFromProjectAsync(
                tenantId, campaignId, projectId, templateId, inlineText, listIds, _bulkOptions.MaxRecipientsPerCampaign, ct);
            if (snapErr != null)
            {
                var msg = snapErr switch
                {
                    ErrorCodes.ProjectNoTargets => "Projenin hedef listesi yok. Önce en az bir liste ekleyin.",
                    ErrorCodes.ContactListNotFound => "Bir hedef liste bulunamadı (silinmiş olabilir). Liste seçimini güncelleyin.",
                    ErrorCodes.ContactListNotReady => "Bir hedef liste henüz hazır değil. İçe aktarımın bitmesini bekleyin.",
                    ErrorCodes.ContactListNoSendable => $"Hedef listelerde gönderilebilir alıcı yok veya üst sınır ({_bulkOptions.MaxRecipientsPerCampaign}) aşıldı.",
                    ErrorCodes.ContactListDbError => "Önizleme oluşturulurken veritabanı hatası; hiçbir şey gönderilmedi. Lütfen tekrar deneyin.",
                    _ => "Önizleme oluşturulamadı."
                };
                return (null, snapErr, msg);
            }
            if (snapshotted == 0)
                return (null, ErrorCodes.ContactListNoSendable, "Gönderilebilir alıcı yok.");

            var sample = await _bulkRepo.GetRecipientPhonesSampleAsync(tenantId, jobId, _bulkOptions.PreviewSampleSize, ct);
            _logger.SystemInfo($"project preview: tenant={tenantId}, project={projectId}, campaign={campaignId}, job={jobId}, snapshotted={snapshotted}");

            return (new BulkSendPreviewResponse
            {
                CampaignId = campaignId,
                Status = "preview_ready",
                HardCap = _bulkOptions.MaxRecipientsPerCampaign,
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
            // RE-VALIDATE at confirm time (TOCTOU: the project may have been archived or switched to HSM since
            // preview). An archived/removed project loads as null -> reject BEFORE any dispatch; an HSM /
            // content-less project is rejected by ResolveSendContent -> never silently dispatched.
            var detail = await _repo.GetAsync(tenantId, projectId, ct);
            if (detail == null) return (null, ErrorCodes.ProjectNotFound, $"Proje {projectId} bulunamadı veya arşivlenmiş.");

            var (_, _, eligErr, eligMsg) = ResolveSendContent(detail.Project);
            if (eligErr != null) return (null, eligErr, eligMsg);

            var job = await _bulkRepo.GetJobAsync(tenantId, campaignId, ct);
            if (job == null) return (null, ErrorCodes.BulkSendJobNotFound, $"'{campaignId}' için önizleme bulunamadı.");
            if (job.ProjectId != projectId)
                return (null, ErrorCodes.ProjectInvalidPayload, "Kampanya bu projeye ait değil.");

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
