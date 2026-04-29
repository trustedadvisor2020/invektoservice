using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Invekto.Backend.Data;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.ClinicMetadata;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Invekto.Shared.Services;
using Npgsql;

namespace Invekto.Backend.Endpoints;

/// <summary>
/// FEAT-CLINIC-METADATA: tenant-scoped clinic_contact + team_members CRUD endpoints.
///   GET /api/v1/tenant-settings/clinic-metadata → current contact + team + updated_at
///   PUT /api/v1/tenant-settings/clinic-metadata → validate + UPSERT + cache invalidate
///
/// Auth: gated by global JWT middleware via <c>/api/v1/tenant-settings/</c> prefix.
/// Same handler-side TenantContext guard pattern as TenantCampaignConfigEndpoints +
/// TenantFieldMappingEndpoints.
///
/// Cross-tenant 3-layer defense (TFM-AUTH-HOTFIX precedent commit 44780d0):
///   (1) JWT TenantContext gate (ctx.Items["TenantContext"] is TenantContext tc)
///   (2) Optional body.tenant_id mismatch → 403 INV-AUTH-010
///   (3) SQL UPSERT WHERE tenant_id = @tid (JWT-bound, body-scoped)
///
/// Cache invalidate: PUT calls <see cref="ITenantClinicMetadataResolver.Invalidate"/>
/// on the receiving Backend instance. Peer Automation instance picks up new state on
/// 5dk TTL expiry (eventual consistency MVP — FEAT-MCC pattern).
///
/// Validation:
///   phone     — E.164-style ^\+[0-9 ]{8,20}$ (INV-BE-123)
///   website / instagram / facebook — ^https?:// (INV-BE-124)
///   JSON body — INV-BE-122 on parse failure
///
/// Response envelope:
///   { data: { tenant_id, clinic_contact: {...}, team_members: [...], updated_at } }
/// </summary>
public static class ClinicMetadataEndpoints
{
    private static readonly Regex PhoneE164 = new(
        @"^\+[0-9 ]{8,20}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    private static readonly Regex HttpUrl = new(
        @"^https?://[^\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    public static void MapClinicMetadataEndpoints(this WebApplication app)
    {
        // ── GET ────────────────────────────────────────────────────────────────────
        app.MapGet("/api/v1/tenant-settings/clinic-metadata", async (
            HttpContext ctx,
            JsonLinesLogger jsonLog,
            TenantSettingsRepository repo) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

            if (ctx.Items["TenantContext"] is not TenantContext tenantContext)
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.AuthUnauthorized,
                        "Tenant context yok. Authorization: Bearer <jwt> header'i ile giris yapip tekrar deneyin.",
                        requestId),
                    statusCode: 401);
            var tenantId = tenantContext.TenantId;

            string contactJson;
            string teamJson;
            DateTime? updatedAt;
            try
            {
                (contactJson, teamJson, updatedAt) = await repo.GetClinicMetadataAsync(tenantId, ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo(
                    $"GET /api/v1/tenant-settings/clinic-metadata cancelled by client (tenant={tenantId})",
                    requestId);
                throw;
            }
            catch (NpgsqlException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.ClinicMetadataDbUnavailable}] GET clinic-metadata DB fail (tenant={tenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.ClinicMetadataDbUnavailable,
                        "Klinik bilgileri okunamadi. Veritabani gecici kullanilamiyor; birkac saniye sonra tekrar deneyin.",
                        requestId),
                    statusCode: 503);
            }

            ClinicContactDto contact;
            List<TeamMemberDto> team;
            try
            {
                contact = ParseContact(contactJson);
                team = ParseTeam(teamJson);
            }
            catch (JsonException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.ClinicMetadataInvalidJson}] GET clinic-metadata malformed stored JSON (tenant={tenantId}): {ex.Message} — Operator: SELECT clinic_contact::text, team_members::text FROM tenant_settings WHERE tenant_id={tenantId}; ile dogrulayin; PUT ile gecerli JSON yazilsin.",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.ClinicMetadataInvalidJson,
                        $"Klinik bilgileri bozuk JSON iceriyor. Operator mudahalesi gerekir: tenant_settings.clinic_contact veya team_members kaydini gozden gecirin (tenant={tenantId}).",
                        requestId),
                    statusCode: 500);
            }

            return Results.Json(new
            {
                data = new
                {
                    tenant_id = tenantId,
                    clinic_contact = contact,
                    team_members = team,
                    updated_at = updatedAt
                }
            });
        });

        // ── PUT ────────────────────────────────────────────────────────────────────
        app.MapPut("/api/v1/tenant-settings/clinic-metadata", async (
            HttpContext ctx,
            JsonLinesLogger jsonLog,
            TenantSettingsRepository repo,
            ITenantClinicMetadataResolver resolver) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

            if (ctx.Items["TenantContext"] is not TenantContext tenantContext)
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.AuthUnauthorized,
                        "Tenant context yok. Authorization: Bearer <jwt> header'i ile giris yapip tekrar deneyin.",
                        requestId),
                    statusCode: 401);
            var tenantId = tenantContext.TenantId;

            UpdateClinicMetadataRequest? body;
            try
            {
                body = await ctx.Request.ReadFromJsonAsync<UpdateClinicMetadataRequest>(
                    DbTenantClinicMetadataResolver.SnakeCaseJson, ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo(
                    $"PUT /api/v1/tenant-settings/clinic-metadata cancelled by client during body read (tenant={tenantId})",
                    requestId);
                throw;
            }
            catch (JsonException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.ClinicMetadataInvalidJson}] PUT clinic-metadata body parse fail (tenant={tenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.ClinicMetadataInvalidJson,
                        "Istek govdesi gecersiz JSON. Content-Type: application/json gondererek gecerli JSON syntax ile tekrar deneyin.",
                        requestId),
                    statusCode: 400);
            }

            if (body is null)
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.ClinicMetadataInvalidJson,
                        "Istek govdesi bos. { \"clinic_contact\": {...}, \"team_members\": [...] } sema ile gonderin.",
                        requestId),
                    statusCode: 400);

            // Layer 2 of cross-tenant defense (mirrors FEAT-TFM endpoint pattern).
            if (body.TenantId.HasValue && body.TenantId.Value != tenantId)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.AuthCrossTenantBlocked}] PUT clinic-metadata cross-tenant attempt blocked (jwt_tenant={tenantId}, body_tenant={body.TenantId.Value})",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.AuthCrossTenantBlocked,
                        $"Cross-tenant yazma engellendi: JWT tenant={tenantId} body tenant={body.TenantId.Value}. Body'den tenant_id'yi kaldirin veya JWT ile ayni tenant'a giris yapin.",
                        requestId),
                    statusCode: 403);
            }

            var contact = body.ClinicContact ?? ClinicContactDto.Empty();
            var team = body.TeamMembers ?? new List<TeamMemberDto>();

            // Validation: phone E.164 (INV-BE-123) + URL https?:// (INV-BE-124).
            var phoneError = ValidatePhone(contact.Phone);
            if (phoneError is not null)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.ClinicMetadataPhoneInvalid}] PUT clinic-metadata phone validation fail (tenant={tenantId}, value='{contact.Phone}')",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.ClinicMetadataPhoneInvalid, phoneError, requestId, "phone"),
                    statusCode: 400);
            }

            var urlError = ValidateUrls(contact);
            if (urlError is not null)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.ClinicMetadataUrlInvalid}] PUT clinic-metadata URL validation fail (tenant={tenantId}, field='{urlError.Field}', value='{urlError.Value}')",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.ClinicMetadataUrlInvalid, urlError.Message, requestId, urlError.Field),
                    statusCode: 400);
            }

            var contactJson = JsonSerializer.Serialize(contact, DbTenantClinicMetadataResolver.SnakeCaseJson);
            var teamJson    = JsonSerializer.Serialize(team, DbTenantClinicMetadataResolver.SnakeCaseJson);

            DateTime updatedAt;
            try
            {
                // Layer 3 of cross-tenant defense: SQL UPSERT WHERE tenant_id = @tid (JWT-bound).
                updatedAt = await repo.UpsertClinicMetadataAsync(tenantId, contactJson, teamJson, ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo(
                    $"PUT /api/v1/tenant-settings/clinic-metadata cancelled by client during DB upsert (tenant={tenantId})",
                    requestId);
                throw;
            }
            catch (NpgsqlException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.ClinicMetadataDbUnavailable}] PUT clinic-metadata DB fail (tenant={tenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.ClinicMetadataDbUnavailable,
                        "Klinik bilgileri kaydedilemedi. Veritabani gecici kullanilamiyor; birkac saniye sonra tekrar deneyin.",
                        requestId),
                    statusCode: 503);
            }

            // Local-instance cache invalidate (FEAT-MCC + FEAT-TFM pattern).
            resolver.Invalidate(tenantId);

            jsonLog.StepInfo(
                $"PUT /api/v1/tenant-settings/clinic-metadata ok (tenant={tenantId}, team_count={team.Count})",
                requestId);

            return Results.Json(new
            {
                data = new
                {
                    tenant_id = tenantId,
                    clinic_contact = contact,
                    team_members = team,
                    updated_at = updatedAt
                }
            });
        });
    }

    /// <summary>Parse stored clinic_contact JSON. Empty / "{}" → empty DTO.</summary>
    private static ClinicContactDto ParseContact(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return ClinicContactDto.Empty();
        return JsonSerializer.Deserialize<ClinicContactDto>(json, DbTenantClinicMetadataResolver.SnakeCaseJson)
               ?? ClinicContactDto.Empty();
    }

    /// <summary>Parse stored team_members JSON array. Empty / "[]" → empty list.</summary>
    private static List<TeamMemberDto> ParseTeam(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return new List<TeamMemberDto>();
        return JsonSerializer.Deserialize<List<TeamMemberDto>>(json, DbTenantClinicMetadataResolver.SnakeCaseJson)
               ?? new List<TeamMemberDto>();
    }

    /// <summary>Validate phone E.164-style: optional. Empty / null = OK (graceful).
    /// Non-empty must match <c>^\+[0-9 ]{8,20}$</c>. Returns user-facing error message
    /// (Turkish) or null.</summary>
    private static string? ValidatePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        return PhoneE164.IsMatch(phone)
            ? null
            : "Telefon numarasi E.164 formatinda olmali (orn. +90 545 123 4567). Sadece + ile baslayan, rakam ve bosluk iceren format kabul edilir.";
    }

    /// <summary>Validate URL fields (website, instagram, facebook). Empty / null = OK.
    /// Non-empty must match <c>^https?://</c>. Address + name are free-text, no validation.
    /// Returns first failing field details or null.</summary>
    private static UrlValidationError? ValidateUrls(ClinicContactDto contact)
    {
        if (!string.IsNullOrWhiteSpace(contact.Website) && !HttpUrl.IsMatch(contact.Website))
            return new UrlValidationError("website", contact.Website,
                "Web sitesi URL'i gecerli format olmali (https:// veya http:// ile baslamali).");
        if (!string.IsNullOrWhiteSpace(contact.Instagram) && !HttpUrl.IsMatch(contact.Instagram))
            return new UrlValidationError("instagram", contact.Instagram,
                "Instagram URL'i gecerli format olmali (https:// ile baslamali).");
        if (!string.IsNullOrWhiteSpace(contact.Facebook) && !HttpUrl.IsMatch(contact.Facebook))
            return new UrlValidationError("facebook", contact.Facebook,
                "Facebook URL'i gecerli format olmali (https:// ile baslamali).");
        return null;
    }

    private sealed record UrlValidationError(string Field, string Value, string Message);

    public sealed class UpdateClinicMetadataRequest
    {
        /// <summary>Optional defensive field. Present-and-mismatch → 403 INV-AUTH-010.</summary>
        [JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }

        [JsonPropertyName("clinic_contact")]
        public ClinicContactDto? ClinicContact { get; set; }

        [JsonPropertyName("team_members")]
        public List<TeamMemberDto>? TeamMembers { get; set; }
    }
}
