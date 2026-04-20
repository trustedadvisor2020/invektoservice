using System.Text.Json;
using System.Text.Json.Serialization;
using Invekto.Backend.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.TenantFieldMapping;
using Invekto.Shared.Contracts.TenantFieldMapping.Dtos;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Invekto.Shared.Services;
using Npgsql;

namespace Invekto.Backend.Endpoints;

/// <summary>
/// FEAT-TFM MVP: tenant-scoped field-mapping CRUD endpoints.
/// GET  /api/v1/tenant-settings/field-mapping  → current mapping JSON + updated_at.
/// PUT  /api/v1/tenant-settings/field-mapping  → validate + UPSERT + cache invalidate.
///
/// Auth: both routes are protected by route-level <c>RequireAuthorization()</c> in addition
/// to the global JWT middleware. Tenant scope comes ONLY from <c>ctx.Items["TenantId"]</c>
/// (signed claim). Body MAY optionally carry tenant_id for explicit defensive 403:
/// if body.tenant_id is present and does NOT match the JWT claim, the request is rejected.
///
/// Response envelope: success → <c>{ data: { tenant_id, field_mapping, updated_at } }</c>
/// (mirrors DMP <c>/api/v1/dynamic-fields</c> pattern). Errors → <c>ErrorResponse.Create</c>.
///
/// JSON contract: serialization/deserialization use <see cref="DbTenantFieldMappingResolver.SnakeCaseJson"/>
/// (PropertyNamingPolicy.SnakeCaseLower) so the wire format is consistent with the DTO
/// JsonPropertyName attributes and the canonical tenant_settings.field_mapping JSONB shape.
///
/// Cancellation: <see cref="OperationCanceledException"/> from request abort is logged with
/// the request id and rethrown to the framework (default 499/cancellation handling). DB and
/// JSON failures are mapped to TFM-specific error codes (INV-BE-110 + INV-BE-096).
/// </summary>
public static class TenantFieldMappingEndpoints
{
    public static void MapTenantFieldMappingEndpoints(this WebApplication app)
    {
        // ============================================
        // GET — read current mapping (empty {} for new tenants).
        // Malformed stored JSON is surfaced as 500 + INV-BE-096 (operator visibility).
        // ============================================
        app.MapGet("/api/v1/tenant-settings/field-mapping", async (
            HttpContext ctx,
            JsonLinesLogger jsonLog,
            TenantSettingsRepository repo) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

            var tenantIdStr = ctx.Items["TenantId"]?.ToString();
            if (!int.TryParse(tenantIdStr, out var tenantId))
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.AuthUnauthorized,
                        "Tenant context yok. Authorization: Bearer <jwt> header'i ile giris yapip tekrar deneyin.",
                        requestId),
                    statusCode: 401);

            string fieldMappingJson;
            DateTime? updatedAt;
            try
            {
                (fieldMappingJson, updatedAt) = await repo.GetFieldMappingAsync(tenantId, ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo(
                    $"GET /api/v1/tenant-settings/field-mapping cancelled by client (tenant={tenantId})",
                    requestId);
                throw; // Framework default cancellation handling.
            }
            catch (NpgsqlException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FieldMappingDbUnavailable}] GET /api/v1/tenant-settings/field-mapping DB fail (tenant={tenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.FieldMappingDbUnavailable,
                        "Tenant alan tanimlari okunamadi. Veritabani gecici kullanilamiyor; birkac saniye sonra tekrar deneyin.",
                        requestId),
                    statusCode: 500);
            }

            // Parse stored JSON. Malformed stored data is NOT a normal state — surface 500
            // so operators see it instead of returning misleading "no config" empty map.
            Dictionary<string, TenantFieldMappingEntry> mapping;
            try
            {
                mapping = ParseMapping(fieldMappingJson);
            }
            catch (JsonException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FieldMappingInvalid}] GET field-mapping malformed stored JSON (tenant={tenantId}): {ex.Message} — Operator: SELECT field_mapping::text FROM tenant_settings WHERE tenant_id={tenantId} ile dogrulayin; PUT ile gecerli JSON ile uzerine yazilsin.",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.FieldMappingInvalid,
                        $"Tenant alan tanimlari bozuk JSON iceriyor. Operator mudahalesi gerekir: tenant_settings.field_mapping kaydini gozden gecirin (tenant={tenantId}).",
                        requestId),
                    statusCode: 500);
            }

            return Results.Json(new
            {
                data = new
                {
                    tenant_id = tenantId,
                    field_mapping = mapping,
                    updated_at = updatedAt
                }
            });
        });
        // NOTE: route-level RequireAuthorization() removed — Backend uses custom JWT
        // middleware (sets ctx.Items["TenantId"] from signed claim) instead of .NET
        // AddAuthentication scheme. Adding RequireAuthorization() here throws
        // "No authenticationScheme was specified" InvalidOperationException at request
        // time → 500 instead of 401. Same pattern as /api/v1/dynamic-fields (DMP) and
        // /api/v1/optout: rely on the global middleware + handler-side TenantId guard.

        // ============================================
        // PUT — validate + UPSERT + invalidate local resolver cache.
        // ============================================
        // Multi-instance note: cache invalidate runs on the receiving Backend instance only.
        // Outbound/Automation resolver caches expire via 5dk TTL (eventual consistency, MVP).
        // Defensive cross-tenant 403: body.tenant_id (optional) MUST match JWT claim if sent.
        app.MapPut("/api/v1/tenant-settings/field-mapping", async (
            HttpContext ctx,
            JsonLinesLogger jsonLog,
            TenantSettingsRepository repo,
            ITenantFieldMappingResolver resolver) =>
        {
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

            var tenantIdStr = ctx.Items["TenantId"]?.ToString();
            if (!int.TryParse(tenantIdStr, out var tenantId))
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.AuthUnauthorized,
                        "Tenant context yok. Authorization: Bearer <jwt> header'i ile giris yapip tekrar deneyin.",
                        requestId),
                    statusCode: 401);

            UpdateFieldMappingRequest? body;
            try
            {
                body = await ctx.Request.ReadFromJsonAsync<UpdateFieldMappingRequest>(
                    DbTenantFieldMappingResolver.SnakeCaseJson, ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo(
                    $"PUT /api/v1/tenant-settings/field-mapping cancelled by client during body read (tenant={tenantId})",
                    requestId);
                throw;
            }
            catch (JsonException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FieldMappingInvalid}] PUT field-mapping body parse fail (tenant={tenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.FieldMappingInvalid,
                        "Istek govdesi gecersiz JSON. Content-Type: application/json gondererek gecerli JSON syntax ile tekrar deneyin.",
                        requestId),
                    statusCode: 400);
            }

            if (body is null)
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.FieldMappingInvalid,
                        "Istek govdesi bos. { \"field_mapping\": { ... } } sema ile gonderin.",
                        requestId),
                    statusCode: 400);

            // Defensive cross-tenant 403: if body explicitly carries tenant_id, it MUST match JWT.
            // Body field is optional by design — JWT remains the authoritative source. This
            // branch protects against stale/copy-pasted payloads from other tenants.
            if (body.TenantId.HasValue && body.TenantId.Value != tenantId)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.AuthCrossTenantBlocked}] PUT field-mapping cross-tenant attempt blocked (jwt_tenant={tenantId}, body_tenant={body.TenantId.Value})",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.AuthCrossTenantBlocked,
                        $"Cross-tenant yazma engellendi: JWT tenant={tenantId} body tenant={body.TenantId.Value}. Body'den tenant_id'yi kaldirin veya JWT ile ayni tenant'a giris yapin.",
                        requestId),
                    statusCode: 403);
            }

            var mapping = body.FieldMapping ?? new Dictionary<string, TenantFieldMappingEntry>();

            try
            {
                TenantFieldMappingValidator.Validate(mapping);
            }
            catch (TenantFieldMappingValidationException ex)
            {
                jsonLog.StepWarn(
                    $"[{ex.ErrorCode}] PUT field-mapping validation fail (tenant={tenantId}, semantic='{ex.SemanticName}'): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(ex.ErrorCode, ex.Message, requestId, ex.SemanticName),
                    statusCode: 400);
            }

            // Serialize with snake_case contract so persisted JSONB matches the DTO wire format.
            var json = JsonSerializer.Serialize(mapping, DbTenantFieldMappingResolver.SnakeCaseJson);

            DateTime updatedAt;
            try
            {
                updatedAt = await repo.UpsertFieldMappingAsync(tenantId, json, ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                jsonLog.StepInfo(
                    $"PUT /api/v1/tenant-settings/field-mapping cancelled by client during DB upsert (tenant={tenantId})",
                    requestId);
                throw;
            }
            catch (NpgsqlException ex)
            {
                jsonLog.StepWarn(
                    $"[{ErrorCodes.FieldMappingDbUnavailable}] PUT field-mapping DB fail (tenant={tenantId}): {ex.Message}",
                    requestId);
                return Results.Json(
                    ErrorResponse.Create(
                        ErrorCodes.FieldMappingDbUnavailable,
                        "Tenant alan tanimlari kaydedilemedi. Veritabani gecici kullanilamiyor; birkac saniye sonra tekrar deneyin.",
                        requestId),
                    statusCode: 500);
            }

            // Local-instance cache invalidate via interface method (no type-cast). DI
            // decoration / future replacement of the resolver still honors invalidation.
            resolver.Invalidate(tenantId);

            jsonLog.StepInfo(
                $"PUT /api/v1/tenant-settings/field-mapping ok (tenant={tenantId}, entries={mapping.Count})",
                requestId);

            return Results.Json(new
            {
                data = new
                {
                    tenant_id = tenantId,
                    field_mapping = mapping,
                    updated_at = updatedAt
                }
            });
        });
        // NOTE: route-level RequireAuthorization() removed — Backend uses custom JWT
        // middleware (sets ctx.Items["TenantId"] from signed claim) instead of .NET
        // AddAuthentication scheme. Adding RequireAuthorization() here throws
        // "No authenticationScheme was specified" InvalidOperationException at request
        // time → 500 instead of 401. Same pattern as /api/v1/dynamic-fields (DMP) and
        // /api/v1/optout: rely on the global middleware + handler-side TenantId guard.
    }

    /// <summary>
    /// Parse stored field_mapping JSON with the snake_case serializer contract. Empty / "{}"
    /// returns empty dictionary. Throws <see cref="JsonException"/> on malformed JSON so
    /// the GET handler can surface a 500 to the operator (DO NOT swallow — silent empty map
    /// hides corruption).
    /// </summary>
    private static Dictionary<string, TenantFieldMappingEntry> ParseMapping(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return new Dictionary<string, TenantFieldMappingEntry>();

        return JsonSerializer.Deserialize<Dictionary<string, TenantFieldMappingEntry>>(
                   json, DbTenantFieldMappingResolver.SnakeCaseJson)
               ?? new Dictionary<string, TenantFieldMappingEntry>();
    }

    public sealed class UpdateFieldMappingRequest
    {
        /// <summary>
        /// Optional defensive field. If present, MUST match JWT claim — otherwise 403.
        /// JWT remains the authoritative tenant scope source.
        /// </summary>
        [JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }

        [JsonPropertyName("field_mapping")]
        public Dictionary<string, TenantFieldMappingEntry>? FieldMapping { get; set; }
    }
}
