using System.Text.Json;
using Invekto.Backend.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Leads;
using Invekto.Shared.Data;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Backend.Services;

/// <summary>
/// FEAT-LIW Chunk C: Dashboard-facing orchestrator for the tenant landing
/// settings endpoints. Owns the transactional envelope around settings
/// mutations — every apikey.rotate / apikey.revoke / fieldmap.save runs
/// (1) a SELECT to capture the before-snapshot inside the tx,
/// (2) the settings UPDATE via <see cref="TenantLandingSettingsRepository"/>
///     with optimistic-concurrency on updated_at (0 rows -> Conflict),
/// (3) a liw_audit_log INSERT via <see cref="LiwAuditRepository"/>
///     using the SAME connection, and
/// (4) COMMIT — so an audit insert failure rolls back the whole mutation.
/// Dry-run path is read-only and reuses the exact <see cref="FieldMapResolver"/>
/// + <see cref="PhoneE164Normalizer"/> + consent-check code path as real intake
/// so preview output can never drift from runtime behaviour.
/// Result pattern: each method returns a typed Status enum + payload so the
/// endpoint layer maps to HTTP responses without exception-as-control-flow.
/// </summary>
public sealed class LiwSettingsService
{
    private readonly PostgresConnectionFactory _db;
    private readonly TenantLandingSettingsRepository _tlsRepo;
    private readonly LiwAuditRepository _auditRepo;
    private readonly FlowLookupClient _flowLookup;
    private readonly FieldMapValidator _validator;
    private readonly FieldMapResolver _fieldMap;
    private readonly PhoneE164Normalizer _phoneNorm;
    private readonly ApiKeyGenerator _keyGen;
    private readonly JsonLinesLogger _logger;

    public LiwSettingsService(
        PostgresConnectionFactory db,
        TenantLandingSettingsRepository tlsRepo,
        LiwAuditRepository auditRepo,
        FlowLookupClient flowLookup,
        FieldMapValidator validator,
        FieldMapResolver fieldMap,
        PhoneE164Normalizer phoneNorm,
        ApiKeyGenerator keyGen,
        JsonLinesLogger logger)
    {
        _db = db;
        _tlsRepo = tlsRepo;
        _auditRepo = auditRepo;
        _flowLookup = flowLookup;
        _validator = validator;
        _fieldMap = fieldMap;
        _phoneNorm = phoneNorm;
        _keyGen = keyGen;
        _logger = logger;
    }

    // ================================================================
    //  GET /api/v1/tenant/landing/settings
    // ================================================================

    public async Task<GetSettingsResult> GetSettingsAsync(int tenantId, CancellationToken ct = default)
    {
        TenantLandingSettings? row;
        try
        {
            row = await _tlsRepo.FindByTenantIdAsync(tenantId, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LiwSettings.Get: DB error tenant={tenantId}: {ex.Message}");
            return GetSettingsResult.Failure(503, ErrorCodes.DatabaseConnectionFailed, "Veritabani baglantisi kurulamadi.");
        }

        var dto = BuildSettingsDto(tenantId, row);

        // flow_status lookup: Backend calls Automation's /api/v1/automation/flows/lookup
        // via FlowLookupClient (microservice-isolation-respecting HTTP hop, not a
        // direct DB read). If Automation is unreachable / returns non-2xx, the client
        // returns null and we leave flow_status.exists=false so the UI banner renders
        // (tenant sees the problem loudly, same as a real missing-slug case).
        var flowMeta = await _flowLookup.LookupAsync(tenantId, dto.FlowStatus.ResolvedSlug, ct);
        if (flowMeta != null && flowMeta.Exists)
        {
            dto.FlowStatus.Exists = true;
            dto.FlowStatus.FlowId = flowMeta.FlowId;
            dto.FlowStatus.FlowDisplayName = flowMeta.DisplayName;
        }

        return GetSettingsResult.Success(dto);
    }

    // ================================================================
    //  POST /api/v1/tenant/landing/apikey/rotate
    // ================================================================

    public async Task<RotateApiKeyResult> RotateApiKeyAsync(
        int tenantId,
        int userId,
        DateTime? expectedRowVersion,
        CancellationToken ct = default)
    {
        // Open connection + begin transaction FIRST so the pre-lookup, the UPDATE,
        // and the audit insert all share the same transaction boundary (CQ9 iter 1
        // fix: before-snapshot cannot drift under a concurrent writer because the
        // UPDATE's optimistic concurrency check and the audit INSERT execute against
        // the same transaction as the SELECT that produced the snapshot).
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            TenantLandingSettings? existing = await _tlsRepo.FindByTenantIdAsync(conn, tenantId, ct);

            if (existing == null)
            {
                return await RotateFirstTimeInTxAsync(conn, tx, tenantId, userId, ct);
            }

            if (expectedRowVersion == null)
            {
                // Row exists but caller omitted If-Match — concurrency hazard, refuse.
                await tx.RollbackAsync(ct);
                return RotateApiKeyResult.Conflict();
            }

            return await RotateExistingInTxAsync(conn, tx, tenantId, userId, existing, expectedRowVersion.Value, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LiwSettings.Rotate: DB error tenant={tenantId}: {ex.Message}");
            try { await tx.RollbackAsync(ct); } catch (NpgsqlException) { /* tx already dead */ }
            return RotateApiKeyResult.Failure(503, ErrorCodes.DatabaseConnectionFailed, "Veritabani baglantisi kurulamadi.");
        }
    }

    // First-time rotate inner helper: runs INSIDE the caller's open transaction so
    // the before-state SELECT (done by the public method above), the INSERT, and
    // the audit INSERT all commit atomically.
    private async Task<RotateApiKeyResult> RotateFirstTimeInTxAsync(
        NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        int tenantId,
        int userId,
        CancellationToken ct)
    {
        var newKey = _keyGen.Generate();
        var nowUtc = DateTime.UtcNow;

        EnsureRowResult ensure;
        try
        {
            ensure = await _tlsRepo.EnsureRowExistsOrCreateWithKeyAsync(conn, tenantId, newKey, nowUtc, ct);
        }
        catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseDuplicateEntry}] LiwSettings.Rotate first-time: unique-violation on landing_api_key tenant={tenantId}; retrying with fresh key");
            newKey = _keyGen.Generate();
            ensure = await _tlsRepo.EnsureRowExistsOrCreateWithKeyAsync(conn, tenantId, newKey, nowUtc, ct);
        }

        if (!ensure.InsertedNewRow)
        {
            // Another request won the create race — conflict so caller can refetch.
            await tx.RollbackAsync(ct);
            return RotateApiKeyResult.Conflict();
        }

        var afterJson = JsonSerializer.Serialize(new
        {
            masked_active_key = ApiKeyGenerator.Mask(newKey),
            masked_old_key = (string?)null,
            old_expires_at = (DateTime?)null
        });
        await _auditRepo.InsertAsync(conn, tenantId, userId, LiwAuditActions.ApiKeyRotate,
            beforeJson: null, afterJson: afterJson, ct);

        await tx.CommitAsync(ct);

        return RotateApiKeyResult.Success(new RotateApiKeyResponse
        {
            ActivePlaintext = newKey,
            MaskedOld = null,
            OldExpiresAt = null,
            RowVersion = ensure.RowVersion
        });
    }

    // Existing-row rotate inner helper: runs INSIDE the caller's open transaction.
    private async Task<RotateApiKeyResult> RotateExistingInTxAsync(
        NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        int tenantId,
        int userId,
        TenantLandingSettings before,
        DateTime expectedRowVersion,
        CancellationToken ct)
    {
        var newKey = _keyGen.Generate();
        var graceUntil = DateTime.UtcNow.AddHours(24);

        DateTime? updatedVersion;
        try
        {
            updatedVersion = await _tlsRepo.RotateApiKeyAsync(conn, tenantId, newKey, graceUntil, expectedRowVersion, ct);
        }
        catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseDuplicateEntry}] LiwSettings.Rotate: unique-violation on landing_api_key tenant={tenantId}; retrying with fresh key");
            newKey = _keyGen.Generate();
            updatedVersion = await _tlsRepo.RotateApiKeyAsync(conn, tenantId, newKey, graceUntil, expectedRowVersion, ct);
        }

        if (updatedVersion == null)
        {
            await tx.RollbackAsync(ct);
            return RotateApiKeyResult.Conflict();
        }

        var maskedBeforeActive = ApiKeyGenerator.Mask(before.ActiveApiKey);
        var maskedNew = ApiKeyGenerator.Mask(newKey);
        var beforeJson = JsonSerializer.Serialize(new
        {
            masked_active_key = maskedBeforeActive,
            masked_old_key = ApiKeyGenerator.Mask(before.OldApiKey),
            old_expires_at = before.OldApiKeyExpiresAt
        });
        var afterJson = JsonSerializer.Serialize(new
        {
            masked_active_key = maskedNew,
            masked_old_key = maskedBeforeActive,
            old_expires_at = (DateTime?)graceUntil
        });
        await _auditRepo.InsertAsync(conn, tenantId, userId, LiwAuditActions.ApiKeyRotate,
            beforeJson: beforeJson, afterJson: afterJson, ct);

        await tx.CommitAsync(ct);

        return RotateApiKeyResult.Success(new RotateApiKeyResponse
        {
            ActivePlaintext = newKey,
            MaskedOld = maskedBeforeActive,
            OldExpiresAt = graceUntil,
            RowVersion = updatedVersion.Value
        });
    }

    // ================================================================
    //  POST /api/v1/tenant/landing/apikey/revoke
    // ================================================================

    public async Task<MutationResult> RevokeApiKeyAsync(
        int tenantId,
        int userId,
        DateTime expectedRowVersion,
        CancellationToken ct = default)
    {
        // Same-transaction pattern: SELECT + UPDATE + audit INSERT share one tx boundary
        // so the before-snapshot cannot drift relative to the UPDATE + audit (CQ9 iter 1 fix).
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            TenantLandingSettings? before = await _tlsRepo.FindByTenantIdAsync(conn, tenantId, ct);
            if (before == null)
            {
                await tx.RollbackAsync(ct);
                return MutationResult.Failure(404, ErrorCodes.LeadIntakeTenantNotConfigured, "Tenant ayarlari tamamlanmadi.");
            }

            var updatedVersion = await _tlsRepo.RevokeApiKeyAsync(conn, tenantId, expectedRowVersion, ct);
            if (updatedVersion == null)
            {
                await tx.RollbackAsync(ct);
                return MutationResult.Conflict();
            }

            var beforeJson = JsonSerializer.Serialize(new
            {
                masked_active_key = ApiKeyGenerator.Mask(before.ActiveApiKey),
                masked_old_key = ApiKeyGenerator.Mask(before.OldApiKey),
                old_expires_at = before.OldApiKeyExpiresAt
            });
            var afterJson = JsonSerializer.Serialize(new
            {
                masked_active_key = (string?)null,
                masked_old_key = (string?)null,
                old_expires_at = (DateTime?)null
            });
            await _auditRepo.InsertAsync(conn, tenantId, userId, LiwAuditActions.ApiKeyRevoke,
                beforeJson: beforeJson, afterJson: afterJson, ct);

            await tx.CommitAsync(ct);
            return MutationResult.Success(updatedVersion.Value);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LiwSettings.Revoke: DB error tenant={tenantId}: {ex.Message}");
            try { await tx.RollbackAsync(ct); } catch (NpgsqlException) { /* tx already dead */ }
            return MutationResult.Failure(503, ErrorCodes.DatabaseConnectionFailed, "Veritabani baglantisi kurulamadi.");
        }
    }

    // ================================================================
    //  PUT /api/v1/tenant/landing/fieldmap
    // ================================================================

    public async Task<UpdateFieldMapResult> UpdateFieldMapAsync(
        int tenantId,
        int userId,
        UpdateFieldMapRequest req,
        CancellationToken ct = default)
    {
        // Validate input shape via FieldMapValidator (source -> canonical direction).
        var validation = _validator.Validate(req.FieldMap);
        if (!validation.IsValid)
        {
            var first = validation.Errors[0];
            return UpdateFieldMapResult.ValidationFailed(first.ErrorCode, first.Message, validation.Errors);
        }

        var expected = req.ExpectedRowVersion;
        if (expected == null)
        {
            // Missing required If-Match / body.expected_row_version is a CLIENT INPUT bug
            // (not a concurrency conflict). Codex iter 2 CQ1+CQ9+CQ12: 400 validation path
            // with an actionable message so the caller knows to GET /settings first and
            // include the row_version header/field on retry.
            var error = new ValidationError(
                SourceField: "expected_row_version",
                CanonicalField: "",
                ErrorCode: ErrorCodes.GeneralValidation,
                Message: "If-Match header veya body.expected_row_version zorunlu (revoke/save oncesi GET /settings ile row_version alinmali).");
            return UpdateFieldMapResult.ValidationFailed(
                error.ErrorCode,
                error.Message,
                new[] { error });
        }

        // Convert UI shape (source -> canonical) into stored JSONB shape (canonical -> source + optional phone.country_hint).
        var storedJson = SerializeFieldMapForStorage(req.FieldMap, req.PhoneCountryHint);

        // Same-transaction pattern: SELECT + UPDATE + audit INSERT share one tx boundary
        // so the before-snapshot cannot drift relative to the UPDATE + audit (CQ9 iter 1 fix).
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            TenantLandingSettings? before = await _tlsRepo.FindByTenantIdAsync(conn, tenantId, ct);
            if (before == null)
            {
                await tx.RollbackAsync(ct);
                return UpdateFieldMapResult.Failure(404, ErrorCodes.LeadIntakeTenantNotConfigured, "Tenant ayarlari tamamlanmadi.");
            }

            var updatedVersion = await _tlsRepo.UpdateFieldMapAsync(conn, tenantId, storedJson, expected.Value, ct);
            if (updatedVersion == null)
            {
                await tx.RollbackAsync(ct);
                return UpdateFieldMapResult.Conflict();
            }

            await _auditRepo.InsertAsync(conn, tenantId, userId, LiwAuditActions.FieldMapSave,
                beforeJson: before.LandingFieldMapJson,
                afterJson: storedJson,
                ct);

            await tx.CommitAsync(ct);

            var response = new UpdateFieldMapResponse
            {
                FieldMap = new Dictionary<string, string>(req.FieldMap),
                PhoneCountryHint = string.IsNullOrWhiteSpace(req.PhoneCountryHint) ? null : req.PhoneCountryHint.Trim().ToUpperInvariant(),
                RowVersion = updatedVersion.Value
            };
            return UpdateFieldMapResult.Success(response);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LiwSettings.FieldMap: DB error tenant={tenantId}: {ex.Message}");
            try { await tx.RollbackAsync(ct); } catch (NpgsqlException) { /* tx already dead */ }
            return UpdateFieldMapResult.Failure(503, ErrorCodes.DatabaseConnectionFailed, "Veritabani baglantisi kurulamadi.");
        }
    }

    // ================================================================
    //  POST /api/v1/tenant/landing/dry-run
    // ================================================================

    public async Task<DryRunOutcome> DryRunAsync(int tenantId, DryRunRequest? req, CancellationToken ct = default)
    {
        if (req == null)
        {
            return DryRunOutcome.Failure(400, ErrorCodes.LeadIntakeDryRunPayloadInvalid, "Dry-run payload gecersiz (JSON format hatasi).");
        }

        // Resolve the effective field_map: override wins; else use saved map.
        LandingFieldMap map;
        var response = new DryRunResponse();

        if (req.FieldMapOverride != null)
        {
            map = _fieldMap.FromSourceToCanonical(req.FieldMapOverride, req.PhoneCountryHintOverride);
        }
        else
        {
            TenantLandingSettings? saved;
            try
            {
                saved = await _tlsRepo.FindByTenantIdAsync(tenantId, ct);
            }
            catch (NpgsqlException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LiwSettings.DryRun: saved-map lookup DB error tenant={tenantId}: {ex.Message}");
                return DryRunOutcome.Failure(503, ErrorCodes.DatabaseConnectionFailed, "Veritabani baglantisi kurulamadi.");
            }

            if (saved == null)
            {
                response.Warnings.Add("Tenant henuz landing ayarlari kaydetmedi — override field_map gondererek test edebilirsiniz.");
                return DryRunOutcome.Success(response);
            }

            try
            {
                map = _fieldMap.ParseMap(saved.LandingFieldMapJson);
            }
            catch (JsonException)
            {
                response.Warnings.Add("Kayitli landing_field_map bozuk (JSONB parse failed) — yetkili ile iletisime gecin.");
                return DryRunOutcome.Success(response);
            }
        }

        // Convert the incoming Dictionary<string, JsonElement> payload to the
        // Dictionary<string, object?> shape FieldMapResolver expects.
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (req.Payload != null)
        {
            foreach (var kvp in req.Payload)
            {
                fields[kvp.Key] = kvp.Value;
            }
        }

        if (fields.Count == 0)
        {
            response.Warnings.Add("Payload bos — hicbir kaynak alan saglanmadi.");
        }

        // Resolve per-canonical. Warnings cover the three failure modes surfaced
        // at real intake (required canonical unmapped, required value absent, consent false).
        foreach (var canonical in FieldMapValidator.RequiredCanonicals)
        {
            if (map.GetSourceField(canonical) == null)
            {
                response.Warnings.Add($"Zorunlu canonical '{canonical}' alani field_map icinde tanimli degil.");
            }
        }

        var nameVal = _fieldMap.ResolveString(map, fields, LeadIntakeCanonical.Name);
        var phoneRaw = _fieldMap.ResolveString(map, fields, LeadIntakeCanonical.Phone);
        var emailVal = _fieldMap.ResolveString(map, fields, LeadIntakeCanonical.Email);
        var consentVal = _fieldMap.ResolveBool(map, fields, LeadIntakeCanonical.Consent);

        response.ResolvedFields[LeadIntakeCanonical.Name] = nameVal;
        response.ResolvedFields[LeadIntakeCanonical.Email] = emailVal;
        response.ResolvedFields[LeadIntakeCanonical.Consent] = consentVal;

        // Phone normalize.
        if (string.IsNullOrWhiteSpace(phoneRaw))
        {
            response.Warnings.Add("Telefon degeri payload'dan alinamadi — field_map kaynak alan adini kontrol edin.");
            response.ResolvedFields[LeadIntakeCanonical.Phone] = null;
        }
        else
        {
            var phoneE164 = _phoneNorm.Normalize(phoneRaw, map.PhoneCountryHint);
            if (phoneE164 == null)
            {
                response.Warnings.Add($"Telefon E.164 normalize basarisiz: '{phoneRaw}' (country_hint={map.PhoneCountryHint ?? "none"}).");
                response.ResolvedFields[LeadIntakeCanonical.Phone] = phoneRaw;
            }
            else
            {
                response.PhoneE164 = phoneE164;
                response.ResolvedFields[LeadIntakeCanonical.Phone] = phoneE164;
            }
        }

        response.ConsentOk = consentVal == true;
        if (!response.ConsentOk)
        {
            response.Warnings.Add("KVKK onay degeri true degil — lead kaydi reddedilir.");
        }

        var requiredMappedAndResolved = FieldMapValidator.RequiredCanonicals.All(c => map.GetSourceField(c) != null);
        response.WouldUpsert = requiredMappedAndResolved && response.PhoneE164 != null && response.ConsentOk;
        // would_trigger_welcome is a conservative estimate; real intake also checks dup-window
        // against leads.created_at for that tenant+phone. Dry-run does NOT read leads so we
        // report the optimistic "would welcome fire if no prior lead exists" case.
        response.WouldTriggerWelcome = response.WouldUpsert;

        return DryRunOutcome.Success(response);
    }

    // ================================================================
    //  GET /api/v1/tenant/landing/audit
    // ================================================================

    public async Task<ListAuditResult> ListAuditAsync(int tenantId, int limit, CancellationToken ct = default)
    {
        var clamped = Math.Clamp(limit, 1, 200);
        try
        {
            var entries = await _auditRepo.ListAsync(tenantId, clamped, ct);
            return ListAuditResult.Success(entries);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LiwSettings.ListAudit: DB error tenant={tenantId}: {ex.Message}");
            return ListAuditResult.Failure(503, ErrorCodes.DatabaseConnectionFailed, "Veritabani baglantisi kurulamadi.");
        }
    }

    // ================================================================
    //  Helpers
    // ================================================================

    private TenantLandingSettingsDto BuildSettingsDto(int tenantId, TenantLandingSettings? row)
    {
        if (row == null)
        {
            return new TenantLandingSettingsDto
            {
                TenantId = tenantId,
                MaskedActiveKey = null,
                MaskedOldKey = null,
                OldExpiresAt = null,
                FieldMap = new Dictionary<string, string>(),
                PhoneCountryHint = null,
                WelcomeFlowSlug = null,
                DupWindowDays = 30,
                FlowStatus = new FlowStatusDto { ResolvedSlug = "welcome_default" },
                UpdatedAt = null,
                CreatedAt = null,
                HasActiveKey = false
            };
        }

        // Parse stored JSONB (canonical -> source + optional phone.country_hint)
        // and invert to the UI-natural direction (source -> canonical).
        LandingFieldMap parsed;
        try { parsed = _fieldMap.ParseMap(row.LandingFieldMapJson); }
        catch (JsonException) { parsed = new LandingFieldMap(); }

        var uiMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in parsed.Map)
        {
            // kvp.Key = canonical, kvp.Value = source_field. UI wants source -> canonical.
            uiMap[kvp.Value] = kvp.Key;
        }

        var resolvedSlug = string.IsNullOrWhiteSpace(row.WelcomeFlowSlug) ? "welcome_default" : row.WelcomeFlowSlug;

        return new TenantLandingSettingsDto
        {
            TenantId = row.TenantId,
            MaskedActiveKey = ApiKeyGenerator.Mask(row.ActiveApiKey),
            MaskedOldKey = ApiKeyGenerator.Mask(row.OldApiKey),
            OldExpiresAt = row.OldApiKeyExpiresAt,
            FieldMap = uiMap,
            PhoneCountryHint = parsed.PhoneCountryHint,
            WelcomeFlowSlug = row.WelcomeFlowSlug,
            DupWindowDays = row.DupWindowDays,
            FlowStatus = new FlowStatusDto { ResolvedSlug = resolvedSlug },
            UpdatedAt = row.UpdatedAt,
            CreatedAt = row.CreatedAt,
            HasActiveKey = !string.IsNullOrEmpty(row.ActiveApiKey)
        };
    }

    /// <summary>
    /// Convert UI shape (source -> canonical) + country hint into the stored
    /// JSONB shape { canonical: source_field, 'phone.country_hint'?: 'IE' }.
    /// Mirrors <see cref="FieldMapResolver.ParseMap"/>'s inverse direction.
    /// </summary>
    private static string SerializeFieldMapForStorage(
        IDictionary<string, string> sourceToCanonical,
        string? phoneCountryHint)
    {
        var stored = new Dictionary<string, string>();
        foreach (var kvp in sourceToCanonical)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value)) continue;
            // kvp.Key = source, kvp.Value = canonical -> store as canonical -> source.
            // metadata canonical may have multiple source rows; latest wins in v1 (the
            // FieldMapResolver only resolves each canonical to ONE source anyway; a
            // multi-metadata-source feature would need a resolver extension).
            stored[kvp.Value] = kvp.Key;
        }
        if (!string.IsNullOrWhiteSpace(phoneCountryHint))
        {
            stored[LeadIntakeCanonical.PhoneCountryHintKey] = phoneCountryHint.Trim().ToUpperInvariant();
        }
        return JsonSerializer.Serialize(stored);
    }
}

// ================================================================
//  Typed results (avoid exception-as-control-flow in endpoint layer)
// ================================================================

public enum MutationStatus
{
    Success = 0,
    Conflict = 1,
    Failure = 2,
    ValidationFailed = 3
}

public sealed class GetSettingsResult
{
    public MutationStatus Status { get; init; }
    public TenantLandingSettingsDto? Payload { get; init; }
    public int HttpStatus { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public static GetSettingsResult Success(TenantLandingSettingsDto dto) =>
        new() { Status = MutationStatus.Success, Payload = dto, HttpStatus = 200 };

    public static GetSettingsResult Failure(int http, string code, string message) =>
        new() { Status = MutationStatus.Failure, HttpStatus = http, ErrorCode = code, Message = message };
}

public sealed class RotateApiKeyResult
{
    public MutationStatus Status { get; init; }
    public RotateApiKeyResponse? Payload { get; init; }
    public int HttpStatus { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public static RotateApiKeyResult Success(RotateApiKeyResponse payload) =>
        new() { Status = MutationStatus.Success, Payload = payload, HttpStatus = 200 };

    public static RotateApiKeyResult Conflict() =>
        new() { Status = MutationStatus.Conflict, HttpStatus = 409, ErrorCode = ErrorCodes.LeadIntakeSettingsConcurrentModification, Message = "Ayarlar baska bir sekmede degistirildi, son hali yuklendi." };

    public static RotateApiKeyResult Failure(int http, string code, string message) =>
        new() { Status = MutationStatus.Failure, HttpStatus = http, ErrorCode = code, Message = message };
}

public sealed class MutationResult
{
    public MutationStatus Status { get; init; }
    public DateTime? NewRowVersion { get; init; }
    public int HttpStatus { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public static MutationResult Success(DateTime rowVersion) =>
        new() { Status = MutationStatus.Success, NewRowVersion = rowVersion, HttpStatus = 204 };

    public static MutationResult Conflict() =>
        new() { Status = MutationStatus.Conflict, HttpStatus = 409, ErrorCode = ErrorCodes.LeadIntakeSettingsConcurrentModification, Message = "Ayarlar baska bir sekmede degistirildi, son hali yuklendi." };

    public static MutationResult Failure(int http, string code, string message) =>
        new() { Status = MutationStatus.Failure, HttpStatus = http, ErrorCode = code, Message = message };
}

public sealed class UpdateFieldMapResult
{
    public MutationStatus Status { get; init; }
    public UpdateFieldMapResponse? Payload { get; init; }
    public IReadOnlyList<ValidationError>? ValidationErrors { get; init; }
    public int HttpStatus { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public static UpdateFieldMapResult Success(UpdateFieldMapResponse payload) =>
        new() { Status = MutationStatus.Success, Payload = payload, HttpStatus = 200 };

    public static UpdateFieldMapResult Conflict() =>
        new() { Status = MutationStatus.Conflict, HttpStatus = 409, ErrorCode = ErrorCodes.LeadIntakeSettingsConcurrentModification, Message = "Ayarlar baska bir sekmede degistirildi, son hali yuklendi." };

    public static UpdateFieldMapResult ValidationFailed(string code, string message, IReadOnlyList<ValidationError> errors) =>
        new() { Status = MutationStatus.ValidationFailed, HttpStatus = 400, ErrorCode = code, Message = message, ValidationErrors = errors };

    public static UpdateFieldMapResult Failure(int http, string code, string message) =>
        new() { Status = MutationStatus.Failure, HttpStatus = http, ErrorCode = code, Message = message };
}

public sealed class DryRunOutcome
{
    public MutationStatus Status { get; init; }
    public DryRunResponse? Payload { get; init; }
    public int HttpStatus { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public static DryRunOutcome Success(DryRunResponse payload) =>
        new() { Status = MutationStatus.Success, Payload = payload, HttpStatus = 200 };

    public static DryRunOutcome Failure(int http, string code, string message) =>
        new() { Status = MutationStatus.Failure, HttpStatus = http, ErrorCode = code, Message = message };
}

public sealed class ListAuditResult
{
    public MutationStatus Status { get; init; }
    public IReadOnlyList<LiwAuditEntryDto>? Payload { get; init; }
    public int HttpStatus { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public static ListAuditResult Success(IReadOnlyList<LiwAuditEntryDto> entries) =>
        new() { Status = MutationStatus.Success, Payload = entries, HttpStatus = 200 };

    public static ListAuditResult Failure(int http, string code, string message) =>
        new() { Status = MutationStatus.Failure, HttpStatus = http, ErrorCode = code, Message = message };
}
