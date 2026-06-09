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
    private readonly JsonLinesLogger _logger;

    public ProjectsService(ProjectsRepository repo, ProjectsOptions options, JsonLinesLogger logger)
    {
        _repo = repo;
        _options = options;
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
