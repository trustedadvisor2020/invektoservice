using System.Text;
using System.Text.Json;
using Invekto.Backend.Data;
using Invekto.Backend.Services.Internal;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Leads;
using Invekto.Shared.Contracts.MetaLeadgen;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Backend.Services.MetaLeadgen;

/// <summary>
/// Paket B-META: all Meta Leadgen HTTP surface in a single map extension.
///
/// Route groups:
///   * Public  — <c>/api/inbound/meta/leadgen/{tenantId}</c> GET+POST
///     (Meta subscription handshake + signed webhook delivery; excluded from
///     global JWT middleware via the <c>/api/inbound/</c> prefix, same shape
///     as LIW Chunk A's <c>/api/v1/leads/intake/</c>).
///   * Admin   — <c>/api/v1/tenant-settings/meta-leadgen/*</c> (JWT-gated;
///     tenant_id comes from TenantContext claim, never the URL).
///   * Internal— <c>/api/internal/meta-leadgen/process-lead</c> (IntakeInternalAuth
///     shared-secret + JWT; called by Automation <c>MetaLeadgenIntakeJob</c>).
///
/// All error responses use <see cref="ErrorResponse.Create"/> so the envelope
/// stays consistent with LIW / FEAT-MCC / FEAT-EFS. The public webhook path
/// uses plain-text bodies where Meta's spec requires it (GET handshake →
/// echo hub.challenge verbatim). Raw body capture on POST happens BEFORE
/// JSON binding so HMAC-SHA256 has byte-level fidelity.
/// </summary>
public static class MetaLeadgenEndpoints
{
    private const string MetaLeadgenBase      = "/api/v1/tenant-settings/meta-leadgen";
    private const string InboundWebhookPrefix = "/api/inbound/meta/leadgen";
    private const string InternalBase         = "/api/internal/meta-leadgen";

    public static void MapMetaLeadgenEndpoints(this WebApplication app)
    {
        MapPublicHandshakeAndWebhook(app);
        MapAdminSettings(app);
        MapInternalProcessLead(app);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC: Meta verify handshake + signed webhook delivery
    // ═══════════════════════════════════════════════════════════════════════
    private static void MapPublicHandshakeAndWebhook(WebApplication app)
    {
        // GET verify handshake — Meta subscription setup hits this once with
        // hub.mode=subscribe + hub.verify_token + hub.challenge. Spec requires
        // text/plain echo of hub.challenge on success, 403 on token mismatch.
        app.MapGet(InboundWebhookPrefix + "/{tenantId:int}", async (
            int tenantId,
            HttpContext ctx,
            MetaLeadgenConfigRepository configRepo,
            TenantRegistryRepository tenantRegistry,
            JsonLinesLogger jsonLog) =>
        {
            var mode      = ctx.Request.Query["hub.mode"].FirstOrDefault();
            var token     = ctx.Request.Query["hub.verify_token"].FirstOrDefault();
            var challenge = ctx.Request.Query["hub.challenge"].FirstOrDefault();

            if (!string.Equals(mode, "subscribe", StringComparison.Ordinal))
                return Results.Text($"[{ErrorCodes.MetaVerifyTokenMismatch}] bad hub.mode",
                    "text/plain", statusCode: 403);

            bool tenantExists;
            try
            {
                tenantExists = await tenantRegistry.TenantExistsAsync(tenantId, ctx.RequestAborted);
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgen verify: tenant exist DB error tenant={tenantId}: {ex.Message}");
                return Results.Text("db-unavailable", "text/plain", statusCode: 503);
            }
            if (!tenantExists)
                return Results.Text($"[{ErrorCodes.MetaTenantUnknown}] tenant-unknown",
                    "text/plain", statusCode: 404);

            MetaLeadgenConfigDto config;
            try
            {
                (config, _) = await configRepo.GetConfigAsync(tenantId, ctx.RequestAborted);
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgen verify: config DB error tenant={tenantId}: {ex.Message}");
                return Results.Text("db-unavailable", "text/plain", statusCode: 503);
            }
            catch (JsonException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.MetaFieldDataParseFailed}] MetaLeadgen verify: stored config malformed tenant={tenantId}: {ex.Message}");
                return Results.Text("config-malformed", "text/plain", statusCode: 500);
            }

            if (string.IsNullOrEmpty(config.VerifyToken) ||
                !TokenEqualsConstantTime(token, config.VerifyToken))
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.MetaVerifyTokenMismatch}] MetaLeadgen verify: token mismatch tenant={tenantId}");
                return Results.Text($"[{ErrorCodes.MetaVerifyTokenMismatch}] verify-token-mismatch",
                    "text/plain", statusCode: 403);
            }

            // Spec requires raw-echo hub.challenge on success. Missing/empty
            // challenge still returns 200 (Meta tolerates empty body here).
            return Results.Text(challenge ?? string.Empty, "text/plain", statusCode: 200);
        }).AllowAnonymous();

        // POST signed webhook delivery — Meta sends { entry: [ { changes: [...] } ] }.
        // We capture raw body, HMAC-verify, audit, enqueue. Always return 200 on
        // accept paths (Meta's 3s deadline) but 4xx on clear reject paths so ops
        // get audit visibility from the event table and Meta Dashboard alike.
        app.MapPost(InboundWebhookPrefix + "/{tenantId:int}", async (
            int tenantId,
            HttpContext ctx,
            MetaLeadgenWebhookService webhookService) =>
        {
            var rid = GetRequestId(ctx);

            // Buffer body to bytes BEFORE any JSON binding — HMAC wants verbatim.
            byte[] rawBody;
            try
            {
                using var ms = new MemoryStream();
                await ctx.Request.Body.CopyToAsync(ms, ctx.RequestAborted);
                rawBody = ms.ToArray();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException ex)
            {
                // Transport-layer failure reading the request body (TCP reset, client
                // abort mid-upload). Not a signature problem — use GeneralUnknown code
                // with the underlying transport message preserved for ops.
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.GeneralUnknown,
                        $"İstek gövdesi okunamadı (transport hatası: {ex.Message}). " +
                        $"Meta tarafı retry edecek; kalıcı ise Kestrel access log'larını inceleyin.",
                        rid),
                    statusCode: 400);
            }

            var signatureHeader = ctx.Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
            var outcome = await webhookService.ProcessWebhookAsync(tenantId, rawBody, signatureHeader, ctx.RequestAborted);

            if (outcome.IsAccepted)
                return Results.Json(new { data = new { accepted = true } }, statusCode: 200);

            return Results.Json(
                ErrorResponse.Create(
                    outcome.ErrorCode ?? ErrorCodes.GeneralUnknown,
                    WebhookRejectMessage(outcome.ErrorCode),
                    rid),
                statusCode: outcome.StatusCode);
        }).AllowAnonymous();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ADMIN: /settings CRUD + rotate + discover + events
    // ═══════════════════════════════════════════════════════════════════════
    private static void MapAdminSettings(WebApplication app)
    {
        // GET config (masked secrets — return last-4 only via MaskSecrets helper)
        app.MapGet(MetaLeadgenBase, async (
            HttpContext ctx,
            JsonLinesLogger jsonLog,
            MetaLeadgenConfigRepository configRepo) =>
        {
            var rid = GetRequestId(ctx);
            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, AuthMsg, rid), statusCode: 401);

            MetaLeadgenConfigDto config;
            DateTime? updatedAt;
            try
            {
                (config, updatedAt) = await configRepo.GetConfigAsync(tc.TenantId, ctx.RequestAborted);
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgen GET config: tenant={tc.TenantId}: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Ayarlar okunamadi.", rid),
                    statusCode: 503);
            }
            catch (JsonException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.MetaFieldDataParseFailed}] MetaLeadgen GET config: stored JSON malformed tenant={tc.TenantId}: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.MetaFieldDataParseFailed,
                        "Kayitli yapilandirma bozuk; operator mudahalesi gerekir.", rid),
                    statusCode: 500);
            }

            var webhookUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}{InboundWebhookPrefix}/{tc.TenantId}";
            return Results.Json(new
            {
                data = new
                {
                    tenant_id   = tc.TenantId,
                    webhook_url = webhookUrl,
                    config      = MaskSecrets(config),
                    updated_at  = updatedAt
                }
            });
        });

        // PUT config (null-means-keep on secret fields)
        app.MapPut(MetaLeadgenBase, async (
            HttpContext ctx,
            JsonLinesLogger jsonLog,
            MetaLeadgenConfigRepository configRepo,
            MetaLeadgenConfigDto? request) =>
        {
            var rid = GetRequestId(ctx);
            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, AuthMsg, rid), statusCode: 401);
            if (request is null)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.GeneralValidation, "Yapilandirma govdesi zorunlu.", rid),
                    statusCode: 400);

            MetaLeadgenConfigDto existing;
            try
            {
                (existing, _) = await configRepo.GetConfigAsync(tc.TenantId, ctx.RequestAborted);
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgen PUT config: load tenant={tc.TenantId}: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Ayarlar yazilamadi.", rid),
                    statusCode: 503);
            }
            catch (JsonException)
            {
                existing = MetaLeadgenConfigDto.Empty();
            }

            // Null-means-keep: if operator did not retype the secret, preserve the stored value.
            var merged = new MetaLeadgenConfigDto
            {
                VerifyToken     = PickNonNull(request.VerifyToken,     existing.VerifyToken),
                AppSecret       = PickNonNull(request.AppSecret,       existing.AppSecret),
                PageAccessToken = PickNonNull(request.PageAccessToken, existing.PageAccessToken),
                PageId          = PickNonNull(request.PageId,          existing.PageId),
                FieldIdMap      = request.FieldIdMap ?? existing.FieldIdMap ?? new Dictionary<string, string>()
            };

            DateTime updatedAt;
            try
            {
                updatedAt = await configRepo.UpsertConfigAsync(tc.TenantId, merged, ctx.RequestAborted);
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgen PUT config: upsert tenant={tc.TenantId}: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Ayarlar yazilamadi.", rid),
                    statusCode: 503);
            }

            jsonLog.StepInfo(
                $"MetaLeadgen PUT config: tenant={tc.TenantId} page_id_set={!string.IsNullOrEmpty(merged.PageId)} field_map_entries={merged.FieldIdMap.Count}",
                rid);
            return Results.Json(new
            {
                data = new
                {
                    tenant_id = tc.TenantId,
                    updated_at = updatedAt
                }
            });
        });

        // POST rotate-verify-token
        app.MapPost(MetaLeadgenBase + "/rotate-verify-token", async (
            HttpContext ctx,
            JsonLinesLogger jsonLog,
            MetaLeadgenConfigRepository configRepo) =>
        {
            var rid = GetRequestId(ctx);
            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, AuthMsg, rid), statusCode: 401);

            try
            {
                var (newToken, updatedAt) = await configRepo.RotateVerifyTokenAsync(tc.TenantId, ctx.RequestAborted);
                jsonLog.StepInfo(
                    $"MetaLeadgen verify_token rotated: tenant={tc.TenantId} token_last4={Last4(newToken)}",
                    rid);
                return Results.Json(new
                {
                    data = new
                    {
                        tenant_id    = tc.TenantId,
                        verify_token = newToken,   // returned once; operator pastes into Meta Subscription
                        updated_at   = updatedAt
                    }
                });
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgen rotate: tenant={tc.TenantId}: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Token rotasyonu yapilamadi.", rid),
                    statusCode: 503);
            }
        });

        // GET /discover-forms — Graph API hop
        app.MapGet(MetaLeadgenBase + "/discover-forms", async (
            HttpContext ctx,
            JsonLinesLogger jsonLog,
            MetaLeadgenConfigRepository configRepo,
            IMetaGraphApiClient graphClient) =>
        {
            var rid = GetRequestId(ctx);
            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, AuthMsg, rid), statusCode: 401);

            MetaLeadgenConfigDto config;
            try
            {
                (config, _) = await configRepo.GetConfigAsync(tc.TenantId, ctx.RequestAborted);
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgen discover: config DB error tenant={tc.TenantId}: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Ayarlar okunamadi.", rid),
                    statusCode: 503);
            }
            catch (JsonException)
            {
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.MetaFieldDataParseFailed,
                        "Kayitli yapilandirma bozuk.", rid),
                    statusCode: 500);
            }

            if (string.IsNullOrWhiteSpace(config.PageId))
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.GeneralValidation,
                        "page_id ayarlanmamis; Meta Business Manager'dan Page ID'yi girin.", rid),
                    statusCode: 400);

            var result = await graphClient.DiscoverFormsAsync(config.PageId, config.PageAccessToken, ctx.RequestAborted);
            if (!result.IsSuccess)
            {
                return Results.Json(
                    ErrorResponse.Create(
                        result.ErrorCode ?? ErrorCodes.MetaGraphApiFetchFailed,
                        result.ErrorMessage ?? "Form kesfi basarisiz.",
                        rid),
                    statusCode: result.ErrorCode == ErrorCodes.MetaAccessTokenMissing ? 500 : 502);
            }
            return Results.Json(new { data = result.Forms });
        });

        // GET /events?limit=N
        app.MapGet(MetaLeadgenBase + "/events", async (
            HttpContext ctx,
            JsonLinesLogger jsonLog,
            MetaLeadgenEventRepository eventRepo) =>
        {
            var rid = GetRequestId(ctx);
            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, AuthMsg, rid), statusCode: 401);

            var limitRaw = ctx.Request.Query["limit"].FirstOrDefault();
            if (!int.TryParse(limitRaw, out var limit)) limit = 5;

            try
            {
                var events = await eventRepo.ListRecentAsync(tc.TenantId, limit, ctx.RequestAborted);
                return Results.Json(new { data = events });
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.DatabaseConnectionFailed}] MetaLeadgen events: tenant={tc.TenantId}: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Olay listesi okunamadi.", rid),
                    statusCode: 503);
            }
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INTERNAL: Automation → Backend process-lead hop (Graph fetch + intake + lead_id back-fill)
    // ═══════════════════════════════════════════════════════════════════════
    private static void MapInternalProcessLead(WebApplication app)
    {
        app.MapPost(InternalBase + "/process-lead", async (
            HttpContext ctx,
            IConfiguration appConfig,
            JsonLinesLogger jsonLog,
            MetaLeadgenConfigRepository configRepo,
            MetaLeadgenEventRepository eventRepo,
            IMetaGraphApiClient graphClient,
            Invekto.Backend.Services.LeadIntakeService leadIntake,
            ProcessLeadRequest? request) =>
        {
            var rid = GetRequestId(ctx);
            const string authMsg = "Servisler arasi yetki gecersiz veya yapilandirilmamis.";

            var auth = IntakeInternalAuth.Validate(ctx, appConfig);
            if (!auth.Ok)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.LeadIntakeInternalAuthInvalid}] process-lead: internal-auth fail (status={auth.StatusCode}, reason='{auth.Reason}')");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.LeadIntakeInternalAuthInvalid, authMsg, rid),
                    statusCode: auth.StatusCode);
            }
            if (ctx.Items["TenantContext"] is not TenantContext tc)
                return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, authMsg, rid), statusCode: 401);
            if (request is null || request.TenantId <= 0 || string.IsNullOrEmpty(request.LeadgenId) || request.EventId <= 0)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.GeneralValidation,
                        "process-lead gövdesi eksik (tenantId/leadgenId/eventId zorunlu).", rid),
                    statusCode: 400);
            if (request.TenantId != tc.TenantId)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.LeadIntakeInternalAuthInvalid, authMsg, rid),
                    statusCode: 403);

            // 1. Load config (page_access_token + field_id_map).
            MetaLeadgenConfigDto config;
            try
            {
                (config, _) = await configRepo.GetConfigAsync(tc.TenantId, ctx.RequestAborted);
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.DatabaseConnectionFailed}] process-lead: config DB error tenant={tc.TenantId}: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Ayarlar okunamadi.", rid),
                    statusCode: 503);
            }

            // 2. Graph API fetch.
            var fetch = await graphClient.FetchLeadAsync(request.LeadgenId, config.PageAccessToken, ctx.RequestAborted);
            if (!fetch.IsSuccess)
            {
                await eventRepo.UpdateLeadLinkAsync(request.EventId, tc.TenantId, leadId: null,
                    errorCode: fetch.ErrorCode, ctx.RequestAborted);
                var httpStatus = fetch.ErrorCode == ErrorCodes.MetaAccessTokenMissing ? 500
                              : fetch.ErrorCode == ErrorCodes.MetaFieldDataParseFailed ? 422
                              : 502; // transient
                return Results.Json(
                    ErrorResponse.Create(fetch.ErrorCode ?? ErrorCodes.MetaGraphApiFetchFailed,
                        fetch.ErrorMessage ?? "Graph API hatasi.", rid),
                    statusCode: httpStatus);
            }

            // 3. Canonical mapping via field_id_map. Required canonical: "phone".
            var canonical = CanonicalMap(fetch.FieldData, config.FieldIdMap);
            if (!canonical.TryGetValue("phone", out var phone) || string.IsNullOrWhiteSpace(phone))
            {
                await eventRepo.UpdateLeadLinkAsync(request.EventId, tc.TenantId, leadId: null,
                    errorCode: ErrorCodes.MetaFieldDataParseFailed, ctx.RequestAborted);
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.MetaFieldDataParseFailed}] process-lead: phone canonical unresolved tenant={tc.TenantId} leadgen={request.LeadgenId} field_data_keys=[{string.Join(",", fetch.FieldData.Keys)}]");
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.MetaFieldDataParseFailed,
                        "Meta lead alanlarinda telefon bulunamadi (field_id_map eksik olabilir).", rid),
                    statusCode: 422);
            }

            // 4. Intake hop via existing LIW WaDirect path (reuses phone normalize +
            //    dup-window + welcome-flow enqueue). Meta's created_time stamps
            //    ReceivedAt so downstream analytics see the actual Meta form submission.
            canonical.TryGetValue("name", out var leadName);
            var waReq = new WaDirectIntakeRequest
            {
                TenantId    = tc.TenantId,
                Phone       = phone,
                ProfileName = string.IsNullOrWhiteSpace(leadName) ? null : leadName,
                Referer     = $"meta-leadgen:{fetch.FormId ?? "unknown"}",
                ReceivedAt  = fetch.CreatedTime ?? DateTime.UtcNow
            };
            var intake = await leadIntake.IntakeWaDirectAsync(waReq, rid, ctx.RequestAborted);
            if (intake.Success is null)
            {
                var failEc = intake.Error?.ErrorCode ?? ErrorCodes.GeneralUnknown;
                await eventRepo.UpdateLeadLinkAsync(request.EventId, tc.TenantId, leadId: null,
                    errorCode: failEc, ctx.RequestAborted);
                return Results.Json(intake.Error, statusCode: intake.StatusCode);
            }

            // 5. Back-fill lead_id on the audit row.
            await eventRepo.UpdateLeadLinkAsync(
                request.EventId, tc.TenantId, intake.Success.LeadId,
                errorCode: null, ctx.RequestAborted);

            jsonLog.StepInfo(
                $"MetaLeadgen process-lead: tenant={tc.TenantId} leadgen={request.LeadgenId} eventId={request.EventId} leadId={intake.Success.LeadId} isNew={intake.Success.IsNew}",
                rid);
            return Results.Json(new
            {
                data = new
                {
                    tenant_id = tc.TenantId,
                    leadgen_id = request.LeadgenId,
                    event_id = request.EventId,
                    lead_id = intake.Success.LeadId,
                    is_new = intake.Success.IsNew
                }
            });
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════
    private const string AuthMsg = "Oturum gecerli degil; tekrar giris yapin.";

    private static string GetRequestId(HttpContext ctx) =>
        ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    /// <summary>
    /// Maps webhook reject <c>outcome.ErrorCode</c> to an operator-facing,
    /// actionable Turkish message. A generic fallback is kept for forward-compat
    /// if new codes are introduced without a message update.
    /// </summary>
    private static string WebhookRejectMessage(string? errorCode) => errorCode switch
    {
        ErrorCodes.MetaSignatureInvalid =>
            "Meta webhook imzası doğrulanamadı. App Secret yanlış veya Meta tarafında Subscription setup yapılmadı. Dashboard → Meta Leadgen → App Secret alanını kontrol edin.",
        ErrorCodes.MetaVerifyTokenMismatch =>
            "Verify token uyuşmuyor. Dashboard'dan [Rotate Verify Token] çalıştırıp yeni değeri Meta Subscription ayarında güncelleyin.",
        ErrorCodes.MetaTenantUnknown =>
            "URL'deki tenant_id tenant_registry'de bulunamadı veya pasif. Müşteri Meta Business Manager'da yanlış callback URL girmiş olabilir.",
        ErrorCodes.MetaFieldDataParseFailed =>
            "Meta payload'unda leadgen_id bulunamadı — form yapılandırması veya Meta API sürüm değişikliği kontrol edilmeli.",
        ErrorCodes.GeneralValidation =>
            "Kayıtlı Meta Leadgen yapılandırması bozuk (JSONB corrupt). Dashboard → Meta Leadgen → [Kaydet] ile yeniden yazılmalı.",
        ErrorCodes.DatabaseConnectionFailed =>
            "Veritabanı geçici olarak ulaşılamıyor. Meta otomatik retry edecek; kalıcı ise /health endpoint'ini inceleyin.",
        ErrorCodes.JobStorageConnectionFailed =>
            "Arka plan iş kuyruğu geçici olarak kullanılamıyor. Meta'nın bir sonraki retry'ında yeniden kuyruğa alınacak.",
        _ => "Meta webhook reddedildi — ops/log kontrolü gerekli."
    };

    /// <summary>
    /// Projects the config for the admin GET response. Secrets (verify_token /
    /// app_secret / page_access_token) are revealed as last-4 only so the
    /// operator can verify "which key is currently active" without exposing
    /// the full secret on a copy-paste. Dashboard's edit form re-sends the
    /// secret when the operator wants to change it (null = keep server value).
    /// </summary>
    private static object MaskSecrets(MetaLeadgenConfigDto c) => new
    {
        verify_token_last4     = Last4(c.VerifyToken),
        app_secret_last4       = Last4(c.AppSecret),
        page_access_token_last4 = Last4(c.PageAccessToken),
        page_id                = c.PageId,
        field_id_map           = c.FieldIdMap
    };

    private static string? Last4(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.Length <= 4 ? "****" : $"****{value[^4..]}";
    }

    private static string? PickNonNull(string? incoming, string? existing)
        => incoming is null ? existing : incoming;

    /// <summary>
    /// Translates Meta question_id -> canonical. <paramref name="fieldIdMap"/>
    /// is question_id -> canonical. Meta sometimes uses standard field names
    /// verbatim (e.g. "full_name", "phone_number", "email") — we include a
    /// small fallback map so new tenants can accept submissions even before
    /// they map anything manually. Tenant-mapped entries win over fallback.
    /// </summary>
    private static Dictionary<string, string> CanonicalMap(
        IReadOnlyDictionary<string, string> fieldData,
        IReadOnlyDictionary<string, string>? fieldIdMap)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Built-in fallback for Meta standard field names.
        foreach (var (k, v) in fieldData)
        {
            var canonical = k switch
            {
                "full_name" or "name"        => "name",
                "phone_number" or "phone"    => "phone",
                "email"                      => "email",
                _ => null
            };
            if (canonical is not null && !result.ContainsKey(canonical))
                result[canonical] = v;
        }

        // Tenant-configured map wins.
        if (fieldIdMap is not null)
        {
            foreach (var (questionId, canonical) in fieldIdMap)
            {
                if (string.IsNullOrEmpty(canonical)) continue;
                if (fieldData.TryGetValue(questionId, out var value))
                    result[canonical] = value;
            }
        }
        return result;
    }

    /// <summary>
    /// Constant-time string compare for the verify_token handshake. Uses the
    /// same SlowEquals approach as <see cref="IntakeInternalAuth"/> so timing
    /// attacks can't probe token length/content.
    /// </summary>
    private static bool TokenEqualsConstantTime(string? a, string b)
    {
        if (a is null) return false;
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}

/// <summary>Internal process-lead body shape (Automation → Backend).</summary>
public sealed class ProcessLeadRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("tenant_id")]  public int    TenantId  { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("leadgen_id")] public string LeadgenId { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonPropertyName("event_id")]   public long   EventId   { get; set; }
}
