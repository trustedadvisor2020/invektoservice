using System.Text.Json;
using System.Text.Json.Serialization;
using Chatinbox.Backend.Data;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Campaigns;
using Chatinbox.Shared.Contracts.Campaigns.Dtos;
using Chatinbox.Shared.DTOs;
using Chatinbox.Shared.Logging;
using Chatinbox.Shared.Services;
using Npgsql;

namespace Chatinbox.Backend.Endpoints;

/// <summary>
/// FEAT-MCC: tenant-scoped multi-city campaign config CRUD endpoints.
///   GET /api/v1/tenant-settings/campaign-config → current config + updated_at
///   PUT /api/v1/tenant-settings/campaign-config → validate + UPSERT + cache invalidate
///
/// Auth: gated by global JWT middleware via <c>/api/v1/tenant-settings/</c> prefix.
/// Same handler-side TenantContext guard pattern as TenantFieldMappingEndpoints +
/// no .RequireAuthorization() (Backend uses custom JWT middleware, FEAT-TFM lessons
/// 2026-04-21).
///
/// Cache invalidate: PUT calls <see cref="ITenantCampaignResolver.Invalidate"/> on the
/// receiving Backend instance. Peer Automation/Marketing instances pick up new state on
/// 5dk TTL expiry (eventual consistency, MVP — interview Q4 chose push pattern).
///
/// Response envelope:
///   { data: { tenant_id, campaign_config: { campaigns: [...] }, updated_at } }
/// Errors → ErrorResponse.Create with bracketed INV-BE-118/120/121 / INV-AUTH-010 codes.
/// </summary>
public static class TenantCampaignConfigEndpoints
{
    public static void MapTenantCampaignConfigEndpoints(this WebApplication app)
    {
        // ── GET ────────────────────────────────────────────────────────────────────
        app.MapGet("/api/v1/tenant-settings/campaign-config", async (
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

            string configJson;
            DateTime? updatedAt;
            try
            {
                (configJson, updatedAt) = await repo.GetCampaignConfigAsync(tenantId, ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo(
                    $"GET /api/v1/tenant-settings/campaign-config cancelled by client (tenant={tenantId})",
                    requestId);
                throw;
            }
            catch (NpgsqlException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.CampaignConfigDbUnavailable}] GET campaign-config DB fail (tenant={tenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.CampaignConfigDbUnavailable,
                        "Kampanya ayarlari okunamadi. Veritabani gecici kullanilamiyor; birkac saniye sonra tekrar deneyin.",
                        requestId),
                    statusCode: 500);
            }

            CampaignConfig config;
            try
            {
                config = ParseConfig(configJson);
            }
            catch (JsonException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.CampaignConfigInvalid}] GET campaign-config malformed stored JSON (tenant={tenantId}): {ex.Message} — Operator: SELECT campaign_config::text FROM tenant_settings WHERE tenant_id={tenantId}; ile dogrulayin; PUT ile gecerli JSON yazilsin.",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.CampaignConfigInvalid,
                        $"Kampanya ayarlari bozuk JSON iceriyor. Operator mudahalesi gerekir: tenant_settings.campaign_config kaydini gozden gecirin (tenant={tenantId}).",
                        requestId),
                    statusCode: 500);
            }

            return Results.Json(new
            {
                data = new
                {
                    tenant_id = tenantId,
                    campaign_config = config,
                    updated_at = updatedAt
                }
            });
        });

        // ── PUT ────────────────────────────────────────────────────────────────────
        app.MapPut("/api/v1/tenant-settings/campaign-config", async (
            HttpContext ctx,
            JsonLinesLogger jsonLog,
            TenantSettingsRepository repo,
            ITenantCampaignResolver resolver) =>
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

            UpdateCampaignConfigRequest? body;
            try
            {
                body = await ctx.Request.ReadFromJsonAsync<UpdateCampaignConfigRequest>(
                    DbTenantCampaignResolver.SnakeCaseJson, ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo(
                    $"PUT /api/v1/tenant-settings/campaign-config cancelled by client during body read (tenant={tenantId})",
                    requestId);
                throw;
            }
            catch (JsonException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.CampaignConfigInvalid}] PUT campaign-config body parse fail (tenant={tenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.CampaignConfigInvalid,
                        "Istek govdesi gecersiz JSON. Content-Type: application/json gondererek gecerli JSON syntax ile tekrar deneyin.",
                        requestId),
                    statusCode: 400);
            }

            if (body is null)
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.CampaignConfigInvalid,
                        "Istek govdesi bos. { \"campaign_config\": { \"campaigns\": [...] } } sema ile gonderin.",
                        requestId),
                    statusCode: 400);

            // Defensive cross-tenant 403 (mirrors FEAT-TFM endpoint pattern).
            if (body.TenantId.HasValue && body.TenantId.Value != tenantId)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.AuthCrossTenantBlocked}] PUT campaign-config cross-tenant attempt blocked (jwt_tenant={tenantId}, body_tenant={body.TenantId.Value})",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.AuthCrossTenantBlocked,
                        $"Cross-tenant yazma engellendi: JWT tenant={tenantId} body tenant={body.TenantId.Value}. Body'den tenant_id'yi kaldirin veya JWT ile ayni tenant'a giris yapin.",
                        requestId),
                    statusCode: 403);
            }

            var config = body.CampaignConfig ?? CampaignConfig.Empty();

            try
            {
                TenantCampaignConfigValidator.Validate(config);
            }
            catch (TenantCampaignConfigValidationException ex)
            {
                jsonLog.StepWarn(
                    $"[{ex.ErrorCode}] PUT campaign-config validation fail (tenant={tenantId}, slug='{ex.CampaignSlug}', field='{ex.FieldPath ?? "-"}'): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ex.ErrorCode, ex.Message, requestId, ex.FieldPath ?? ex.CampaignSlug),
                    statusCode: 400);
            }

            var json = JsonSerializer.Serialize(config, DbTenantCampaignResolver.SnakeCaseJson);

            DateTime updatedAt;
            try
            {
                updatedAt = await repo.UpsertCampaignConfigAsync(tenantId, json, ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo(
                    $"PUT /api/v1/tenant-settings/campaign-config cancelled by client during DB upsert (tenant={tenantId})",
                    requestId);
                throw;
            }
            catch (NpgsqlException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.CampaignConfigDbUnavailable}] PUT campaign-config DB fail (tenant={tenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.CampaignConfigDbUnavailable,
                        "Kampanya ayarlari kaydedilemedi. Veritabani gecici kullanilamiyor; birkac saniye sonra tekrar deneyin.",
                        requestId),
                    statusCode: 500);
            }

            // Local-instance cache invalidate (interview Q4: push pattern).
            resolver.Invalidate(tenantId);

            jsonLog.StepInfo(
                $"PUT /api/v1/tenant-settings/campaign-config ok (tenant={tenantId}, campaigns={config.Campaigns.Count})",
                requestId);

            return Results.Json(new
            {
                data = new
                {
                    tenant_id = tenantId,
                    campaign_config = config,
                    updated_at = updatedAt
                }
            });
        });
    }

    /// <summary>Parse stored campaign_config JSON. Empty / "{}" / no-campaigns → empty config.</summary>
    private static CampaignConfig ParseConfig(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return CampaignConfig.Empty();
        return JsonSerializer.Deserialize<CampaignConfig>(json, DbTenantCampaignResolver.SnakeCaseJson)
               ?? CampaignConfig.Empty();
    }

    public sealed class UpdateCampaignConfigRequest
    {
        /// <summary>Optional defensive field. Present-and-mismatch → 403 INV-AUTH-010.</summary>
        [JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }

        [JsonPropertyName("campaign_config")]
        public CampaignConfig? CampaignConfig { get; set; }
    }
}
