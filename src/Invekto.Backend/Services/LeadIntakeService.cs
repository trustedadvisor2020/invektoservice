using System.Text.Json;
using System.Text.RegularExpressions;
using Hangfire;
using Invekto.Backend.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Leads;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Backend.Services;

/// <summary>
/// FEAT-LIW Chunk A orchestrator for POST /api/v1/leads/intake/{source_slug}.
/// Sequence: slug validate → rate limit → api-key resolve → field map resolve
/// → phone normalize → consent check → UPSERT lead → enqueue welcome flow.
/// Returns a typed outcome; the endpoint layer maps to HTTP status + envelope.
/// </summary>
public sealed class LeadIntakeService
{
    private static readonly Regex SlugRegex = new("^[a-z0-9][a-z0-9-]{0,49}$", RegexOptions.Compiled);

    private readonly TenantLandingSettingsRepository _tlsRepo;
    private readonly LeadRepository _leadRepo;
    private readonly FieldMapResolver _fieldMap;
    private readonly PhoneE164Normalizer _phoneNorm;
    private readonly ApiKeyRateLimiter _limiter;
    private readonly IBackgroundJobClient _jobs;
    private readonly JsonLinesLogger _logger;

    public LeadIntakeService(
        TenantLandingSettingsRepository tlsRepo,
        LeadRepository leadRepo,
        FieldMapResolver fieldMap,
        PhoneE164Normalizer phoneNorm,
        ApiKeyRateLimiter limiter,
        IBackgroundJobClient jobs,
        JsonLinesLogger logger)
    {
        _tlsRepo = tlsRepo;
        _leadRepo = leadRepo;
        _fieldMap = fieldMap;
        _phoneNorm = phoneNorm;
        _limiter = limiter;
        _jobs = jobs;
        _logger = logger;
    }

    public async Task<LeadIntakeOutcome> IntakeAsync(
        string sourceSlug,
        string? apiKey,
        LeadIntakeRequest? request,
        string requestId,
        CancellationToken ct = default)
    {
        // 1. Slug format.
        if (string.IsNullOrWhiteSpace(sourceSlug) || !SlugRegex.IsMatch(sourceSlug))
            return LeadIntakeOutcome.Fail(400, ErrorCodes.LeadIntakeSourceSlugInvalid,
                "Kaynak tanimi gecersiz.");

        // 2. API key present (raw, pre-lookup; 401 either way).
        if (string.IsNullOrWhiteSpace(apiKey))
            return LeadIntakeOutcome.Fail(401, ErrorCodes.LeadIntakeApiKeyInvalid,
                "Gecersiz API anahtari.");

        // 3. Rate limit — applied on the RAW key BEFORE the DB lookup. This keeps
        //    a brute-force "try random keys" loop from spamming the registry.
        var now = DateTime.UtcNow;
        var gate = _limiter.TryAcquire(apiKey, now);
        if (!gate.Allowed)
            return LeadIntakeOutcome.RateLimited(gate.RetryAfterSeconds);

        // 4. Resolve tenant settings from the key (active or in-grace old).
        TenantLandingSettings? tls;
        try
        {
            tls = await _tlsRepo.FindByApiKeyAsync(apiKey, now, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LeadIntake: DB error resolving api key (requestId={requestId}): {ex.Message}");
            return LeadIntakeOutcome.Fail(503, ErrorCodes.DatabaseConnectionFailed,
                "Veritabani baglantisi kurulamadi.");
        }
        if (tls == null)
            return LeadIntakeOutcome.Fail(401, ErrorCodes.LeadIntakeApiKeyInvalid,
                "Gecersiz API anahtari.");

        // 5. Payload presence — distinguished from field-map mismatch: the caller
        //    sent either no JSON body at all or an empty `fields` object, so there
        //    is no canonical-value resolution to attempt. Dedicated INV-BE-109 tells
        //    the tenant operator "your form isn't posting fields", separate from
        //    INV-BE-103 "your map has canonicals my payload doesn't supply".
        if (request == null || request.Fields == null || request.Fields.Count == 0)
            return LeadIntakeOutcome.Fail(400, ErrorCodes.LeadIntakePayloadEmpty,
                "Istek govdesi bos veya eksik; fields alani zorunlu.");

        // 6. Parse field map JSONB.
        LandingFieldMap map;
        try
        {
            map = _fieldMap.ParseMap(tls.LandingFieldMapJson);
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.LeadIntakeFieldMapMalformed}] LeadIntake: tenant {tls.TenantId} landing_field_map JSONB unparseable: {ex.Message}");
            return LeadIntakeOutcome.Fail(500, ErrorCodes.LeadIntakeFieldMapMalformed,
                "Tenant alan eslemesi bozuk, yetkili ile iletisime gecin.");
        }

        // 7. Required canonical presence in map.
        foreach (var canonical in LeadIntakeCanonical.Required)
        {
            if (map.GetSourceField(canonical) == null)
            {
                // Consent missing from the map is its own code for actionable UX;
                // other required canonicals share INV-BE-103.
                if (canonical == LeadIntakeCanonical.Consent)
                    return LeadIntakeOutcome.Fail(400, ErrorCodes.LeadIntakeConsentMissing,
                        "Onay alani zorunlu.");
                return LeadIntakeOutcome.Fail(400, ErrorCodes.LeadIntakeFieldMappingMissing,
                    $"Alan eslemesi eksik: {canonical}");
            }
        }

        // 8. Pull resolved values.
        var nameVal = _fieldMap.ResolveString(map, request.Fields, LeadIntakeCanonical.Name);
        var phoneVal = _fieldMap.ResolveString(map, request.Fields, LeadIntakeCanonical.Phone);
        var emailVal = _fieldMap.ResolveString(map, request.Fields, LeadIntakeCanonical.Email);
        var consentVal = _fieldMap.ResolveBool(map, request.Fields, LeadIntakeCanonical.Consent);

        if (string.IsNullOrWhiteSpace(phoneVal))
            return LeadIntakeOutcome.Fail(400, ErrorCodes.LeadIntakePhoneInvalid,
                "Telefon numarasi gecersiz.");

        // 9. Consent must be explicitly true.
        if (consentVal != true)
            return LeadIntakeOutcome.Fail(400, ErrorCodes.LeadIntakeConsentNotTrue,
                "Onay degeri true olmali.");

        // 10. Phone normalize.
        var phoneE164 = _phoneNorm.Normalize(phoneVal, map.PhoneCountryHint);
        if (phoneE164 == null)
            return LeadIntakeOutcome.Fail(400, ErrorCodes.LeadIntakePhoneInvalid,
                "Telefon numarasi gecersiz.");

        // 11. Build intake_metadata snapshot (flat top-level keys so JSONB `||`
        //     concat on re-submit appends without overwriting earlier submissions).
        var submissionKey = $"submission_{now:yyyyMMddTHHmmssfffZ}";
        var resolved = BuildResolvedFields(map, request.Fields, nameVal, phoneE164, emailVal);
        var snapshot = new Dictionary<string, object?>
        {
            ["last_submission_at"] = now.ToString("O"),
            ["last_source_slug"] = sourceSlug,
            [submissionKey] = new Dictionary<string, object?>
            {
                ["source_slug"] = sourceSlug,
                ["submitted_at"] = (request.SubmittedAt ?? now).ToString("O"),
                ["referer"] = request.Referer,
                ["utm"] = request.Utm,
                ["resolved"] = resolved
            }
        };
        var intakeMetaJson = JsonSerializer.Serialize(snapshot);

        // 12. UPSERT lead.
        LeadRepository.LeadIntakeUpsertResult upsert;
        try
        {
            upsert = await _leadRepo.UpsertIntakeLeadAsync(
                tls.TenantId, phoneE164, nameVal, emailVal, sourceSlug,
                request.Utm?.UtmSource, request.Utm?.UtmMedium, request.Utm?.UtmCampaign,
                intakeMetaJson, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LeadIntake: DB error on UPSERT tenant={tls.TenantId} phone={phoneE164}: {ex.Message}");
            return LeadIntakeOutcome.Fail(503, ErrorCodes.DatabaseConnectionFailed,
                "Veritabani baglantisi kurulamadi.");
        }

        // 13. Duplicate-window classification: lead is "duplicate" when a prior
        //     row existed AND it was created inside the tenant's dup window.
        //     Outside the window counts as re-engagement: duplicate=false, welcome fires.
        var dupWindow = TimeSpan.FromDays(Math.Max(1, tls.DupWindowDays));
        var isDuplicate = upsert.PriorCreatedAt != null
                          && (now - upsert.PriorCreatedAt.Value) <= dupWindow;

        // 14. Welcome-flow enqueue (fresh insert OR re-engagement). Responds via
        //     the welcome_flow_enqueued flag — scheduling only, delivery is the
        //     Hangfire worker's concern with its own retries. Enqueue failure is
        //     structured-warn best-effort: lead is already created, surfacing a
        //     500 would punish the tenant for our infra hiccup. The response
        //     also mirrors the failure code into Warnings so the caller sees it
        //     without having to read server logs.
        var welcomeEnqueued = false;
        List<string>? warnings = null;
        if (!isDuplicate)
        {
            var slug = string.IsNullOrWhiteSpace(tls.WelcomeFlowSlug)
                ? "welcome_default"
                : tls.WelcomeFlowSlug!;
            try
            {
                _jobs.Enqueue<Invekto.Automation.Services.Jobs.TriggerWelcomeFlowJob>(
                    job => job.ExecuteAsync(tls.TenantId, slug, upsert.LeadId, CancellationToken.None));
                welcomeEnqueued = true;
            }
            catch (BackgroundJobClientException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.JobStorageConnectionFailed}] LeadIntake: Hangfire enqueue rejected " +
                    $"(tenant={tls.TenantId}, lead={upsert.LeadId}, slug={slug}): {ex.Message}");
                warnings = new List<string> { ErrorCodes.JobStorageConnectionFailed };
            }
            catch (NpgsqlException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.JobStorageConnectionFailed}] LeadIntake: Hangfire storage DB error " +
                    $"(tenant={tls.TenantId}, lead={upsert.LeadId}, slug={slug}): {ex.Message}");
                warnings = new List<string> { ErrorCodes.JobStorageConnectionFailed };
            }
            catch (InvalidOperationException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.JobHandlerUnresolved}] LeadIntake: Hangfire enqueue misconfigured " +
                    $"(tenant={tls.TenantId}, lead={upsert.LeadId}, slug={slug}): {ex.Message}");
                warnings = new List<string> { ErrorCodes.JobHandlerUnresolved };
            }
        }

        _logger.StepInfo(
            $"LeadIntake: tenant={tls.TenantId} lead={upsert.LeadId} slug={sourceSlug} " +
            $"duplicate={isDuplicate} enqueued={welcomeEnqueued} warnings={(warnings?.Count ?? 0)}",
            requestId);

        return LeadIntakeOutcome.Created(new LeadIntakeResponse
        {
            LeadId = upsert.LeadId,
            Duplicate = isDuplicate,
            WelcomeFlowEnqueued = welcomeEnqueued,
            Warnings = warnings
        });
    }

    private static Dictionary<string, object?> BuildResolvedFields(
        LandingFieldMap map,
        IReadOnlyDictionary<string, object?> fields,
        string? name,
        string phoneE164,
        string? email)
    {
        var resolved = new Dictionary<string, object?>
        {
            [LeadIntakeCanonical.Name] = name,
            [LeadIntakeCanonical.Phone] = phoneE164,
            [LeadIntakeCanonical.Email] = email
        };
        foreach (var canonical in LeadIntakeCanonical.Optional)
        {
            if (canonical == LeadIntakeCanonical.Email) continue;
            var sourceKey = map.GetSourceField(canonical);
            if (sourceKey == null) continue;
            fields.TryGetValue(sourceKey, out var raw);
            resolved[canonical] = NormaliseScalar(raw);
        }
        return resolved;
    }

    private static object? NormaliseScalar(object? raw)
    {
        if (raw is null) return null;
        if (raw is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => el.GetRawText()
            };
        }
        return raw;
    }
}

/// <summary>Transport object between <see cref="LeadIntakeService"/> and the endpoint mapper.</summary>
public sealed class LeadIntakeOutcome
{
    public int StatusCode { get; init; }
    public LeadIntakeResponse? Success { get; init; }
    public ErrorResponse? Error { get; init; }
    public int? RetryAfterSeconds { get; init; }

    public static LeadIntakeOutcome Created(LeadIntakeResponse body) =>
        new() { StatusCode = 201, Success = body };

    public static LeadIntakeOutcome RateLimited(int retryAfter) => new()
    {
        StatusCode = 429,
        RetryAfterSeconds = retryAfter,
        Error = ErrorResponse.Create(
            ErrorCodes.LeadIntakeRateLimitExceeded,
            "Cok fazla istek, sonra deneyiniz.",
            "-")
    };

    public static LeadIntakeOutcome Fail(int status, string code, string message) => new()
    {
        StatusCode = status,
        Error = ErrorResponse.Create(code, message, "-")
    };
}
