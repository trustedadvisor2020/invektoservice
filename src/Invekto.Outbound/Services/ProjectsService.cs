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
            request.TemplateKind, request.InstanceId, request.WaTemplateId, request.TemplateLanguage, request.ParamMapping);
        if (cfgErr != null) return (null, cfgErr, cfgMsg);

        try
        {
            var (chErr, chMsg) = await ValidateChannelOwnershipAsync(tenantId, sendConfig, ct);
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
            request.TemplateKind, request.InstanceId, request.WaTemplateId, request.TemplateLanguage, request.ParamMapping);
        if (cfgErr != null) return (null, cfgErr, cfgMsg);

        if (name == null && desc == null && targetIds == null && !setConfig)
            return (null, ErrorCodes.ProjectInvalidPayload, "No changes supplied");

        try
        {
            var (chErr, chMsg) = await ValidateChannelOwnershipAsync(tenantId, sendConfig, ct);
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

    /// <summary>
    /// Validate + normalize the optional send-config block (channel + template). <c>template_kind</c> is the
    /// DRIVER: null => leave config untouched (setConfig=false). When set, the WHOLE block is validated and
    /// returned for an authoritative write (setConfig=true): both kinds require a channel (instance_id&gt;0);
    /// 'wapcrm_template' requires a non-empty wa_template_id (+ optional language/param_mapping); 'plain_text'
    /// CLEARS the template fields (text is supplied at send time, a later slice). Any inconsistency returns a
    /// typed INV-OB-073 with a user-facing message — never a silent default. param_mapping must be a JSON
    /// object/array within the size cap. Send EXECUTION that consumes this config is a later slice.
    /// </summary>
    private (ProjectsRepository.ProjectSendConfigInput? config, bool setConfig, string? errorCode, string? message)
        BuildSendConfig(string? kind, int? instanceId, string? waTemplateId, string? templateLanguage, JsonElement? paramMapping)
    {
        if (kind == null)
            return (null, false, null, null); // config block omitted -> leave existing config untouched

        if (!ProjectTemplateKinds.IsValid(kind))
            return (null, false, ErrorCodes.ProjectInvalidSendConfig, "Geçersiz mesaj türü (plain_text veya wapcrm_template).");

        if (instanceId is not > 0)
            return (null, false, ErrorCodes.ProjectInvalidSendConfig, "Gönderim için bir WhatsApp kanalı (hat) seçin.");

        if (kind == ProjectTemplateKinds.PlainText)
        {
            // plain_text: channel only; template fields are cleared (message text is entered at send time).
            return (new ProjectsRepository.ProjectSendConfigInput
            {
                InstanceId = instanceId,
                TemplateKind = ProjectTemplateKinds.PlainText,
                WaTemplateId = null,
                TemplateLanguage = null,
                ParamMappingJson = null
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
            ParamMappingJson = pmJson
        }, true, null, null);
    }

    /// <summary>
    /// Server-side authorization of the chosen send channel: the instance_id MUST be a Cloud API line
    /// (instance_type = 1) owned by this tenant. The SPA filters channels client-side, but a direct API
    /// caller could submit any positive id, so this is enforced authoritatively here (Codex CQ5/CQ9 —
    /// closes the cross-tenant / non-Cloud channel persistence gap). No channel set => nothing to authorize.
    /// </summary>
    private async Task<(string? errorCode, string? message)> ValidateChannelOwnershipAsync(
        int tenantId, ProjectsRepository.ProjectSendConfigInput? config, CancellationToken ct)
    {
        if (config?.InstanceId is not int inst) return (null, null);
        var ok = await _repo.IsCloudApiInstanceOwnedAsync(tenantId, inst, ct);
        return ok
            ? (null, null)
            : (ErrorCodes.ProjectInvalidSendConfig, "Seçilen WhatsApp kanalı bu hesaba ait değil veya bir Cloud API hattı değil.");
    }
}
