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
    private readonly TenantRegistryRepository _tenantRegistry;
    private readonly JsonLinesLogger _logger;

    public LeadIntakeService(
        TenantLandingSettingsRepository tlsRepo,
        LeadRepository leadRepo,
        FieldMapResolver fieldMap,
        PhoneE164Normalizer phoneNorm,
        ApiKeyRateLimiter limiter,
        IBackgroundJobClient jobs,
        TenantRegistryRepository tenantRegistry,
        JsonLinesLogger logger)
    {
        _tlsRepo = tlsRepo;
        _leadRepo = leadRepo;
        _fieldMap = fieldMap;
        _phoneNorm = phoneNorm;
        _limiter = limiter;
        _jobs = jobs;
        _tenantRegistry = tenantRegistry;
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

    /// <summary>
    /// FEAT-LIW Chunk B: WA-direct intake. Service-to-service entry point used by
    /// Automation when an inbound WA message arrives from a phone Backend doesn't
    /// know about. Bypasses the consent gate (user-initiated contact = implied
    /// consent under GDPR Recital 32) and the field-map machinery (no tenant
    /// form involved); reuses <see cref="PhoneE164Normalizer"/> and the welcome-
    /// flow enqueue path so behaviour stays aligned with landing intake. Tenant
    /// landing settings are best-effort: a missing row falls back to platform
    /// defaults ('welcome_default' slug, 30-day dup window) so tenants can take
    /// WA leads before configuring a landing page.
    /// </summary>
    public async Task<WaDirectOutcome> IntakeWaDirectAsync(
        WaDirectIntakeRequest? request,
        string requestId,
        CancellationToken ct = default)
    {
        // 1. Payload presence + tenant id sanity (caller is internal, but defend
        //    against a misconfigured Automation just the same).
        if (request == null || request.TenantId <= 0)
            return WaDirectOutcome.Fail(400, ErrorCodes.LeadIntakePayloadEmpty,
                "Istek govdesi bos veya eksik; tenant_id zorunlu.");

        if (string.IsNullOrWhiteSpace(request.Phone))
            return WaDirectOutcome.Fail(400, ErrorCodes.LeadIntakeWaDirectPhoneInvalid,
                "Telefon numarasi eksik veya gecersiz.");

        // 2. Phone normalize. Country hint is null on the wa-direct path —
        //    inbound WA traffic crosses borders unpredictably, so we let
        //    libphonenumber try the international form first and fall through
        //    to the IE/TR defaults baked into PhoneE164Normalizer.
        var phoneE164 = _phoneNorm.Normalize(request.Phone, countryHint: null);
        if (phoneE164 == null)
            return WaDirectOutcome.Fail(400, ErrorCodes.LeadIntakeWaDirectPhoneInvalid,
                "Telefon numarasi eksik veya gecersiz.");

        // 2b. Defense-in-depth tenant existence check. The X-Internal-Service-Token
        //     gate proves the CALLER is an Invekto service, but the tenant_id in
        //     the payload is otherwise trusted blindly — without this guard a
        //     buggy Automation routing the wrong tenant_id would silently create
        //     orphan rows under a non-existent tenant. Caller-supplied tenant_id
        //     is acceptable in this trust model (peer-service auth replaces JWT
        //     tenant-claim binding for /api/internal/* paths), but the existence
        //     check catches coding bugs cheaply (single PK probe against
        //     tenant_registry).
        bool tenantExists;
        try
        {
            tenantExists = await _tenantRegistry.TenantExistsAsync(request.TenantId, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LeadIntake.WaDirect: tenant existence check DB error tenant={request.TenantId}: {ex.Message}");
            return WaDirectOutcome.Fail(503, ErrorCodes.DatabaseConnectionFailed,
                "Veritabani baglantisi kurulamadi.");
        }
        if (!tenantExists)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.LeadIntakeWaDirectUnknownTenant}] LeadIntake.WaDirect: " +
                $"unknown tenant_id={request.TenantId} (caller bug; rejecting before write)");
            return WaDirectOutcome.Fail(400, ErrorCodes.LeadIntakeWaDirectUnknownTenant,
                "Tanimsiz tenant; kayit reddedildi.");
        }

        // 3. Resolve tenant landing settings (optional — defaults applied below).
        TenantLandingSettings? tls;
        try
        {
            tls = await _tlsRepo.FindByTenantIdAsync(request.TenantId, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LeadIntake.WaDirect: TLS lookup DB error tenant={request.TenantId}: {ex.Message}");
            return WaDirectOutcome.Fail(503, ErrorCodes.DatabaseConnectionFailed,
                "Veritabani baglantisi kurulamadi.");
        }

        // Capture into a typed local so the welcome-slug fallback can be
        // expressed without the null-forgiving operator (project rule).
        // configuredSlug being non-null AND non-whitespace after this branch
        // is guaranteed by the IsNullOrWhiteSpace short-circuit.
        var configuredSlug = tls?.WelcomeFlowSlug;
        var welcomeSlug = string.IsNullOrWhiteSpace(configuredSlug)
            ? "welcome_default"
            : configuredSlug;
        var dupWindowDays = tls?.DupWindowDays ?? 30;

        // 4. Build intake_metadata snapshot. Shape mirrors landing intake (flat
        //    top-level keys + submission_<iso> object) so downstream consumers
        //    only learn one schema. `referer` and `wa_profile_name` are omitted
        //    when null — keeping JSONB honest, no synthetic placeholders.
        var now = DateTime.UtcNow;
        var submissionKey = $"submission_{now:yyyyMMddTHHmmssfffZ}";
        var submission = new Dictionary<string, object?>
        {
            ["source_slug"] = "wa-direct",
            ["channel"] = "whatsapp",
            ["submitted_at"] = (request.ReceivedAt ?? now).ToString("O")
        };
        if (!string.IsNullOrWhiteSpace(request.Referer))
            submission["referer"] = request.Referer;
        if (!string.IsNullOrWhiteSpace(request.ProfileName))
            submission["wa_profile_name"] = request.ProfileName;

        var snapshot = new Dictionary<string, object?>
        {
            ["last_submission_at"] = now.ToString("O"),
            ["last_source_slug"] = "wa-direct",
            [submissionKey] = submission
        };
        var intakeMetaJson = JsonSerializer.Serialize(snapshot);

        // 5. Idempotent ensure: existence check inside the dup window short-
        //    circuits writes (returns existing leadId, isNew=false). Outside the
        //    window OR brand-new => UPSERT with channel='whatsapp', slug='wa-direct'.
        LeadRepository.WaDirectEnsureResult ensure;
        try
        {
            ensure = await _leadRepo.EnsureLeadForWaDirectAsync(
                request.TenantId, phoneE164, request.ProfileName,
                dupWindowDays, intakeMetaJson, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LeadIntake.WaDirect: EnsureLead DB error tenant={request.TenantId} phone={phoneE164}: {ex.Message}");
            return WaDirectOutcome.Fail(503, ErrorCodes.DatabaseConnectionFailed,
                "Veritabani baglantisi kurulamadi.");
        }

        // 6. Welcome-flow enqueue mirrors landing intake — only when isNew.
        //    Failure surfaces in Warnings (lead row already committed, no point
        //    in 5xx-ing back to Automation).
        var welcomeEnqueued = false;
        List<string>? warnings = null;
        if (ensure.IsNew)
        {
            try
            {
                _jobs.Enqueue<Invekto.Automation.Services.Jobs.TriggerWelcomeFlowJob>(
                    job => job.ExecuteAsync(request.TenantId, welcomeSlug, ensure.LeadId, CancellationToken.None));
                welcomeEnqueued = true;
            }
            catch (BackgroundJobClientException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.JobStorageConnectionFailed}] LeadIntake.WaDirect: Hangfire enqueue rejected " +
                    $"(tenant={request.TenantId}, lead={ensure.LeadId}, slug={welcomeSlug}): {ex.Message}");
                warnings = new List<string> { ErrorCodes.JobStorageConnectionFailed };
            }
            catch (NpgsqlException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.JobStorageConnectionFailed}] LeadIntake.WaDirect: Hangfire storage DB error " +
                    $"(tenant={request.TenantId}, lead={ensure.LeadId}, slug={welcomeSlug}): {ex.Message}");
                warnings = new List<string> { ErrorCodes.JobStorageConnectionFailed };
            }
            catch (InvalidOperationException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.JobHandlerUnresolved}] LeadIntake.WaDirect: Hangfire enqueue misconfigured " +
                    $"(tenant={request.TenantId}, lead={ensure.LeadId}, slug={welcomeSlug}): {ex.Message}");
                warnings = new List<string> { ErrorCodes.JobHandlerUnresolved };
            }
        }

        _logger.StepInfo(
            $"LeadIntake.WaDirect: tenant={request.TenantId} lead={ensure.LeadId} " +
            $"isNew={ensure.IsNew} enqueued={welcomeEnqueued} warnings={(warnings?.Count ?? 0)}",
            requestId);

        return WaDirectOutcome.Created(new WaDirectIntakeResponse
        {
            LeadId = ensure.LeadId,
            IsNew = ensure.IsNew,
            WelcomeFlowEnqueued = welcomeEnqueued,
            Warnings = warnings
        });
    }

    /// <summary>
    /// FEAT-META-FULL-INTAKE: in-process intake for Meta Leadgen process-lead handler.
    /// Superset of <see cref="IntakeWaDirectAsync"/>: same idempotent ensure +
    /// dup-window + welcome-flow enqueue plumbing, but additionally persists
    /// canonical email + custom_1..5 + consent through intake_metadata snapshot
    /// (custom kolonlari leads tablosuna yazilmaz — FEAT-TFM-SYNC scope). Consent
    /// is hard-gated true (Meta form privacy checkbox is intentional UX): any
    /// other value/null short-circuits with 400 INV-BE-105 BEFORE phone parse +
    /// any DB write, so reject-path leaves no row + no audit-write side effects
    /// upstream caller already wrote the meta_leadgen_events audit row pre-call.
    /// </summary>
    public async Task<MetaLeadgenOutcome> IntakeMetaLeadgenAsync(
        MetaLeadgenIntakeRequest? request,
        string requestId,
        CancellationToken ct = default)
    {
        // 1. Payload presence + tenant id sanity. Caller is in-process Backend
        //    handler; defend-in-depth still applies (a refactor could introduce
        //    a buggy null-init payload before noticing).
        if (request == null || request.TenantId <= 0)
            return MetaLeadgenOutcome.Fail(400, ErrorCodes.LeadIntakePayloadEmpty,
                "Istek govdesi bos veya eksik; tenant_id zorunlu.");

        if (string.IsNullOrWhiteSpace(request.Phone))
            return MetaLeadgenOutcome.Fail(400, ErrorCodes.LeadIntakeWaDirectPhoneInvalid,
                "Telefon numarasi eksik veya gecersiz.");

        // 2. Consent gate (LIW INV-BE-105 reuse). Hard reject before phone parse
        //    so audit logs surface the missing-consent state cheaply. Both
        //    consent=false (operator opted out at form) and consent=null (canonical
        //    not mapped in field_id_map) reject — Meta form privacy URL +
        //    checkbox makes consent=true the only well-formed state.
        if (request.Consent != true)
            return MetaLeadgenOutcome.Fail(400, ErrorCodes.LeadIntakeConsentNotTrue,
                "Onay degeri true olmali.");

        // 3. Phone normalize. Country hint absent on the meta-leadgen path —
        //    Meta forms are international; PhoneE164Normalizer's IE/TR defaults
        //    cover the pilot scope, broader hints are a per-tenant config decision
        //    (post-pilot backlog if needed).
        var phoneE164 = _phoneNorm.Normalize(request.Phone, countryHint: null);
        if (phoneE164 == null)
            return MetaLeadgenOutcome.Fail(400, ErrorCodes.LeadIntakeWaDirectPhoneInvalid,
                "Telefon numarasi eksik veya gecersiz.");

        // 4. Tenant existence guard (mirrors wa-direct defense-in-depth — caller
        //    is trusted by signature-validated Meta webhook, but a routing bug
        //    could land an orphan row under a non-existent tenant_id).
        bool tenantExists;
        try
        {
            tenantExists = await _tenantRegistry.TenantExistsAsync(request.TenantId, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LeadIntake.MetaLeadgen: tenant existence check DB error tenant={request.TenantId}: {ex.Message}");
            return MetaLeadgenOutcome.Fail(503, ErrorCodes.DatabaseConnectionFailed,
                "Veritabani baglantisi kurulamadi.");
        }
        if (!tenantExists)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.LeadIntakeWaDirectUnknownTenant}] LeadIntake.MetaLeadgen: " +
                $"unknown tenant_id={request.TenantId} (caller bug; rejecting before write)");
            return MetaLeadgenOutcome.Fail(400, ErrorCodes.LeadIntakeWaDirectUnknownTenant,
                "Tanimsiz tenant; kayit reddedildi.");
        }

        // 5. Resolve tenant landing settings (welcome slug + dup window — same
        //    optional-fallback posture as wa-direct so a fresh tenant can take
        //    Meta leads before fully configuring a landing page).
        TenantLandingSettings? tls;
        try
        {
            tls = await _tlsRepo.FindByTenantIdAsync(request.TenantId, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LeadIntake.MetaLeadgen: TLS lookup DB error tenant={request.TenantId}: {ex.Message}");
            return MetaLeadgenOutcome.Fail(503, ErrorCodes.DatabaseConnectionFailed,
                "Veritabani baglantisi kurulamadi.");
        }

        var configuredSlug = tls?.WelcomeFlowSlug;
        var welcomeSlug = string.IsNullOrWhiteSpace(configuredSlug)
            ? "welcome_default"
            : configuredSlug;
        var dupWindowDays = tls?.DupWindowDays ?? 30;

        // 6. Build intake_metadata snapshot. resolved.* mirrors landing-intake
        //    canonical keys so downstream consumers (Lead detail panel,
        //    FEAT-TFM-SYNC semantic projection) see one schema. resolved.consent
        //    is always true here because step 2 already gated on Consent==true.
        var now = DateTime.UtcNow;
        var submissionKey = $"submission_{now:yyyyMMddTHHmmssfffZ}";
        var resolved = new Dictionary<string, object?>
        {
            [LeadIntakeCanonical.Name]    = request.Name,
            [LeadIntakeCanonical.Phone]   = phoneE164,
            [LeadIntakeCanonical.Email]   = request.Email,
            [LeadIntakeCanonical.Consent] = true,
            [LeadIntakeCanonical.Custom1] = request.Custom1,
            [LeadIntakeCanonical.Custom2] = request.Custom2,
            [LeadIntakeCanonical.Custom3] = request.Custom3,
            [LeadIntakeCanonical.Custom4] = request.Custom4,
            [LeadIntakeCanonical.Custom5] = request.Custom5
        };

        var submission = new Dictionary<string, object?>
        {
            ["source_slug"]  = "meta-leadgen",
            ["channel"]      = "facebook",
            ["submitted_at"] = (request.ReceivedAt ?? now).ToString("O"),
            ["resolved"]     = resolved
        };
        if (!string.IsNullOrWhiteSpace(request.Referer))
            submission["referer"] = request.Referer;

        var snapshot = new Dictionary<string, object?>
        {
            ["last_submission_at"] = now.ToString("O"),
            ["last_source_slug"]   = "meta-leadgen",
            [submissionKey]        = submission
        };
        var intakeMetaJson = JsonSerializer.Serialize(snapshot);

        // 7. Idempotent UPSERT via dedicated EnsureLeadForMetaLeadgenAsync
        //    (WaDirect superset that additionally writes leads.email).
        LeadRepository.WaDirectEnsureResult ensure;
        try
        {
            ensure = await _leadRepo.EnsureLeadForMetaLeadgenAsync(
                request.TenantId, phoneE164, request.Name, request.Email,
                dupWindowDays, intakeMetaJson, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DatabaseConnectionFailed}] LeadIntake.MetaLeadgen: EnsureLead DB error tenant={request.TenantId} phone={phoneE164}: {ex.Message}");
            return MetaLeadgenOutcome.Fail(503, ErrorCodes.DatabaseConnectionFailed,
                "Veritabani baglantisi kurulamadi.");
        }

        // 8. Welcome-flow enqueue (fail-soft mirrors wa-direct: lead is committed,
        //    Hangfire infra hiccups should not 5xx the caller).
        var welcomeEnqueued = false;
        List<string>? warnings = null;
        if (ensure.IsNew)
        {
            try
            {
                _jobs.Enqueue<Invekto.Automation.Services.Jobs.TriggerWelcomeFlowJob>(
                    job => job.ExecuteAsync(request.TenantId, welcomeSlug, ensure.LeadId, CancellationToken.None));
                welcomeEnqueued = true;
            }
            catch (BackgroundJobClientException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.JobStorageConnectionFailed}] LeadIntake.MetaLeadgen: Hangfire enqueue rejected " +
                    $"(tenant={request.TenantId}, lead={ensure.LeadId}, slug={welcomeSlug}): {ex.Message}");
                warnings = new List<string> { ErrorCodes.JobStorageConnectionFailed };
            }
            catch (NpgsqlException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.JobStorageConnectionFailed}] LeadIntake.MetaLeadgen: Hangfire storage DB error " +
                    $"(tenant={request.TenantId}, lead={ensure.LeadId}, slug={welcomeSlug}): {ex.Message}");
                warnings = new List<string> { ErrorCodes.JobStorageConnectionFailed };
            }
            catch (InvalidOperationException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.JobHandlerUnresolved}] LeadIntake.MetaLeadgen: Hangfire enqueue misconfigured " +
                    $"(tenant={request.TenantId}, lead={ensure.LeadId}, slug={welcomeSlug}): {ex.Message}");
                warnings = new List<string> { ErrorCodes.JobHandlerUnresolved };
            }
        }

        _logger.StepInfo(
            $"LeadIntake.MetaLeadgen: tenant={request.TenantId} lead={ensure.LeadId} " +
            $"isNew={ensure.IsNew} email_set={!string.IsNullOrWhiteSpace(request.Email)} " +
            $"customs_set={CountNonEmpty(request.Custom1, request.Custom2, request.Custom3, request.Custom4, request.Custom5)} " +
            $"enqueued={welcomeEnqueued} warnings={(warnings?.Count ?? 0)}",
            requestId);

        return MetaLeadgenOutcome.Created(new MetaLeadgenIntakeResponse
        {
            LeadId = ensure.LeadId,
            IsNew = ensure.IsNew,
            WelcomeFlowEnqueued = welcomeEnqueued,
            Warnings = warnings
        });
    }

    /// <summary>Diagnostic helper for the meta-leadgen log line — no behaviour impact.</summary>
    private static int CountNonEmpty(params string?[] values)
    {
        var n = 0;
        foreach (var v in values)
            if (!string.IsNullOrWhiteSpace(v)) n++;
        return n;
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

/// <summary>
/// FEAT-LIW Chunk B: transport between <see cref="LeadIntakeService.IntakeWaDirectAsync"/>
/// and the wa-direct internal endpoint mapper. Mirrors <see cref="LeadIntakeOutcome"/>
/// but carries a <see cref="WaDirectIntakeResponse"/> on success — the wa-direct
/// shape is intentionally narrower (no Duplicate flag; IsNew is the inverse semantic).
/// </summary>
public sealed class WaDirectOutcome
{
    public int StatusCode { get; init; }
    public WaDirectIntakeResponse? Success { get; init; }
    public ErrorResponse? Error { get; init; }

    public static WaDirectOutcome Created(WaDirectIntakeResponse body) =>
        new() { StatusCode = 201, Success = body };

    public static WaDirectOutcome Fail(int status, string code, string message) => new()
    {
        StatusCode = status,
        Error = ErrorResponse.Create(code, message, "-")
    };
}

/// <summary>
/// FEAT-META-FULL-INTAKE: transport between <see cref="LeadIntakeService.IntakeMetaLeadgenAsync"/>
/// and the in-process MetaLeadgenEndpoints process-lead handler. Mirrors the
/// <see cref="WaDirectOutcome"/> shape (StatusCode + Success/Error union) so the
/// caller maps to <c>Results.Json(...)</c> identically. Distinct type per LIW
/// pattern — keeps each canonical entry path's response contract independently
/// evolvable.
/// </summary>
public sealed class MetaLeadgenOutcome
{
    public int StatusCode { get; init; }
    public MetaLeadgenIntakeResponse? Success { get; init; }
    public ErrorResponse? Error { get; init; }

    public static MetaLeadgenOutcome Created(MetaLeadgenIntakeResponse body) =>
        new() { StatusCode = 201, Success = body };

    public static MetaLeadgenOutcome Fail(int status, string code, string message) => new()
    {
        StatusCode = status,
        Error = ErrorResponse.Create(code, message, "-")
    };
}
