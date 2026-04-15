// Adim 3 Paket 3-C: Super-admin cross-tenant ops endpoints.
// Auth: X-Internal-Service-Token (shared-secret). Outside /api/v1/ prefix so JwtAuth middleware skips.
//   GET    /api/internal/ops/zoho/connections
//   GET    /api/internal/ops/zoho/sync-log           (filtre + paginate; tenantId opsiyonel)
//   DELETE /api/internal/ops/zoho/connections/{tenantId}
//   POST   /api/internal/ops/zoho/sync-log/retry     (body { ids:[...] }, max 50)
// Trust: Backend /api/ops/* ValidateOpsAuth ile super-admin dogrulamasi yapar; Integrations
// shared-secret ile Backend'e guvenir. Audit log Backend tarafinda (JsonLinesLogger SystemInfo).
//
// Schema source of truth (NO new migrations — reuses existing tables):
//   - zoho_connections:  arch/db/migrations/012-zoho-connections.sql (Adim 2 Paket A)
//                        Columns read: tenant_id, region, zoho_user_email, connected_at,
//                        updated_at, last_refreshed_at, disconnected_at (soft-delete).
//   - zoho_sync_log:     arch/db/migrations/014-zoho-sync-log.sql + 015-rename-gunes-to-zoho.sql
//                        Columns read/written: id, tenant_id, zoho_event, source_lead_id,
//                        zoho_lead_id, status, attempt_count, last_error_code/message,
//                        updated_at, completed_at. Status whitelist: pending|failed|success.
// Canonical: arch/db/integrations.sql.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Integrations.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Zoho;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Invekto.Integrations.Services.Zoho;

public static class ZohoOpsEndpoints
{
    public const string InternalTokenHeader = "X-Internal-Service-Token";
    private const string SharedSecretConfigKey = "InternalServices:SharedSecret";

    private const int DefaultPageSize = 20;
    private const int MaxPageSize     = 100;
    public  const int MaxBatchRetryIds = 50;

    private static readonly HashSet<string> AllowedStatusValues =
        new(StringComparer.OrdinalIgnoreCase) { "pending", "failed", "success" };

    public static IEndpointRouteBuilder MapZohoOpsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/internal/ops/zoho/connections
        app.MapGet("/api/internal/ops/zoho/connections", async (
            HttpContext ctx,
            IConfiguration config,
            ZohoConnectionRepository connRepo,
            ZohoSyncLogRepository logRepo,
            JsonLinesLogger logger,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var auth = ValidateInternalAuth(ctx, config);
            if (!auth.Ok)
                return Results.Json(ErrorResponse.Create(auth.ErrorCode, auth.Reason, requestId), statusCode: auth.StatusCode);

            try
            {
                var rows = await connRepo.ListAllForOpsAsync(ct).ConfigureAwait(false);
                var items = new List<ZohoOpsConnectionEntryDto>(rows.Count);
                int connected = 0;
                int disconnected = 0;
                foreach (var c in rows)
                {
                    var isConnected = c.DisconnectedAt is null;
                    if (isConnected) connected++; else disconnected++;
                    items.Add(new ZohoOpsConnectionEntryDto
                    {
                        TenantId       = c.TenantId,
                        Region         = c.Region,
                        ZohoUserEmail  = c.ZohoUserEmail,
                        ConnectedAt    = new DateTimeOffset(DateTime.SpecifyKind(c.ConnectedAt, DateTimeKind.Utc)),
                        LastRefreshedAt = c.LastRefreshedAt.HasValue
                            ? new DateTimeOffset(DateTime.SpecifyKind(c.LastRefreshedAt.Value, DateTimeKind.Utc))
                            : null,
                        DisconnectedAt = c.DisconnectedAt.HasValue
                            ? new DateTimeOffset(DateTime.SpecifyKind(c.DisconnectedAt.Value, DateTimeKind.Utc))
                            : null,
                    });
                }

                var failed24h = await logRepo.CountFailedLast24hAsync(ct).ConfigureAwait(false);
                return Results.Ok(new ZohoOpsConnectionListResponse
                {
                    Items              = items,
                    ConnectedCount     = connected,
                    DisconnectedCount  = disconnected,
                    FailedLast24hCount = failed24h,
                });
            }
            catch (NpgsqlException ex)
            {
                logger.SystemWarn($"[{ZohoErrorCodes.OpsReadFailed}] ops connections list failed: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ZohoErrorCodes.OpsReadFailed,
                        "Zoho yonetim verisi okunamadi, tekrar deneyin.", requestId),
                    statusCode: 500);
            }
        });

        // GET /api/internal/ops/zoho/sync-log
        app.MapGet("/api/internal/ops/zoho/sync-log", async (
            HttpContext ctx,
            IConfiguration config,
            ZohoSyncLogRepository repo,
            JsonLinesLogger logger,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var auth = ValidateInternalAuth(ctx, config);
            if (!auth.Ok)
                return Results.Json(ErrorResponse.Create(auth.ErrorCode, auth.Reason, requestId), statusCode: auth.StatusCode);

            var query = ctx.Request.Query;
            var page     = ParseInt(query["page"].ToString(), fallback: 1, min: 1);
            var pageSize = ParseInt(query["pageSize"].ToString(), fallback: DefaultPageSize, min: 1, max: MaxPageSize);

            int? tenantFilter = null;
            var rawTenant = query["tenantId"].ToString();
            if (!string.IsNullOrEmpty(rawTenant))
            {
                if (!int.TryParse(rawTenant, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tid) || tid < 0)
                    return Results.Json(
                        ErrorResponse.Create(ZohoErrorCodes.OpsQueryValidation,
                            $"Invalid tenantId '{rawTenant}'.", requestId),
                        statusCode: 400);
                tenantFilter = tid;
            }

            string? statusFilter = null;
            var rawStatus = query["status"].ToString();
            if (!string.IsNullOrEmpty(rawStatus))
            {
                if (!AllowedStatusValues.Contains(rawStatus))
                    return Results.Json(
                        ErrorResponse.Create(ZohoErrorCodes.OpsQueryValidation,
                            $"Invalid status filter '{rawStatus}'. Allowed: pending, failed, success.", requestId),
                        statusCode: 400);
                statusFilter = rawStatus.ToLowerInvariant();
            }

            var rawEvent = query["event"].ToString();
            string? eventFilter = string.IsNullOrEmpty(rawEvent) ? null : rawEvent;

            DateTime? fromUtc = null, toUtc = null;
            var rawFrom = query["from"].ToString();
            var rawTo   = query["to"].ToString();
            if (!string.IsNullOrEmpty(rawFrom))
            {
                if (!TryParseIso(rawFrom, out var f))
                    return Results.Json(
                        ErrorResponse.Create(ZohoErrorCodes.OpsQueryValidation,
                            $"Invalid 'from' date '{rawFrom}'. Expected ISO8601.", requestId),
                        statusCode: 400);
                fromUtc = f;
            }
            if (!string.IsNullOrEmpty(rawTo))
            {
                if (!TryParseIso(rawTo, out var t))
                    return Results.Json(
                        ErrorResponse.Create(ZohoErrorCodes.OpsQueryValidation,
                            $"Invalid 'to' date '{rawTo}'. Expected ISO8601.", requestId),
                        statusCode: 400);
                toUtc = t;
            }

            try
            {
                var (rows, total) = await repo.ListAllForOpsAsync(
                    tenantFilter, statusFilter, eventFilter, fromUtc, toUtc, page, pageSize, ct).ConfigureAwait(false);

                var items = new List<ZohoOpsSyncLogEntryDto>(rows.Count);
                foreach (var r in rows)
                {
                    items.Add(new ZohoOpsSyncLogEntryDto
                    {
                        Id               = r.Id,
                        TenantId         = r.TenantId,
                        ZohoEvent        = r.ZohoEvent,
                        SourceLeadId     = r.SourceLeadId,
                        ZohoLeadId       = r.ZohoLeadId,
                        Status           = r.Status,
                        AttemptCount     = r.AttemptCount,
                        LastErrorCode    = r.LastErrorCode,
                        LastErrorMessage = r.LastErrorMessage,
                        UpdatedAt        = new DateTimeOffset(DateTime.SpecifyKind(r.UpdatedAt, DateTimeKind.Utc)),
                        CompletedAt      = r.CompletedAt.HasValue
                            ? new DateTimeOffset(DateTime.SpecifyKind(r.CompletedAt.Value, DateTimeKind.Utc))
                            : null,
                    });
                }

                return Results.Ok(new ZohoOpsSyncLogPageResponse
                {
                    Items      = items,
                    Page       = page,
                    PageSize   = pageSize,
                    TotalCount = total,
                });
            }
            catch (NpgsqlException ex)
            {
                logger.SystemWarn($"[{ZohoErrorCodes.OpsReadFailed}] ops sync-log list failed: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ZohoErrorCodes.OpsReadFailed,
                        "Zoho yonetim verisi okunamadi, tekrar deneyin.", requestId),
                    statusCode: 500);
            }
        });

        // DELETE /api/internal/ops/zoho/connections/{tenantId}
        app.MapDelete("/api/internal/ops/zoho/connections/{tenantId:int}", async (
            int tenantId,
            HttpContext ctx,
            IConfiguration config,
            ZohoConnectionService service,
            JsonLinesLogger logger,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var auth = ValidateInternalAuth(ctx, config);
            if (!auth.Ok)
                return Results.Json(ErrorResponse.Create(auth.ErrorCode, auth.Reason, requestId), statusCode: auth.StatusCode);

            var (disconnected, revokeOk) = await service.DisconnectAsync(tenantId, logger, ct).ConfigureAwait(false);
            if (!disconnected)
            {
                return Results.Json(
                    ErrorResponse.Create(ZohoErrorCodes.OpsTargetConnectionNotFound,
                        $"Bu firma (tenant={tenantId}) icin aktif Zoho baglantisi bulunamadi.", requestId),
                    statusCode: 404);
            }

            return Results.Ok(new ZohoOpsDisconnectResponse { TenantId = tenantId, TokenRevoked = revokeOk });
        });

        // POST /api/internal/ops/zoho/sync-log/retry
        app.MapPost("/api/internal/ops/zoho/sync-log/retry", async (
            HttpContext ctx,
            IConfiguration config,
            ZohoSyncLogRepository repo,
            JsonLinesLogger logger,
            ZohoOpsBatchRetryRequest body,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var auth = ValidateInternalAuth(ctx, config);
            if (!auth.Ok)
                return Results.Json(ErrorResponse.Create(auth.ErrorCode, auth.Reason, requestId), statusCode: auth.StatusCode);

            var ids = body?.Ids ?? Array.Empty<long>();
            if (ids.Count == 0)
                return Results.Json(
                    ErrorResponse.Create(ZohoErrorCodes.OpsBatchRetryInvalid,
                        "Tekrar denenecek kayit listesi bos veya gecersiz.", requestId),
                    statusCode: 400);

            if (ids.Count > MaxBatchRetryIds)
                return Results.Json(
                    ErrorResponse.Create(ZohoErrorCodes.OpsBatchRetryLimitExceeded,
                        $"Toplu tekrar deneme en fazla {MaxBatchRetryIds} kayit icerebilir (istenen: {ids.Count}).",
                        requestId),
                    statusCode: 400);

            try
            {
                var (updated, skipped) = await repo.ResetBatchForOpsRetryAsync(ids, ct).ConfigureAwait(false);
                var skippedDtos = new List<ZohoOpsBatchRetrySkipEntry>(skipped.Count);
                foreach (var s in skipped)
                    skippedDtos.Add(new ZohoOpsBatchRetrySkipEntry { Id = s.Id, Reason = s.Reason });

                return Results.Ok(new ZohoOpsBatchRetryResponse
                {
                    Requested = ids.Count,
                    Updated   = updated.Count,
                    Skipped   = skippedDtos,
                });
            }
            catch (NpgsqlException ex)
            {
                logger.SystemWarn($"[{ZohoErrorCodes.OpsReadFailed}] ops batch retry failed: {ex.Message}");
                return Results.Json(
                    ErrorResponse.Create(ZohoErrorCodes.OpsReadFailed,
                        "Toplu tekrar deneme sirasinda bir hata olustu.", requestId),
                    statusCode: 500);
            }
        });

        return app;
    }

    private static (bool Ok, int StatusCode, string ErrorCode, string Reason) ValidateInternalAuth(
        HttpContext ctx, IConfiguration config)
    {
        var expected = config[SharedSecretConfigKey];
        if (string.IsNullOrWhiteSpace(expected))
            return (false, 500, ZohoErrorCodes.InternalAuthNotConfigured,
                $"Internal service auth not configured (missing {SharedSecretConfigKey}).");

        if (!ctx.Request.Headers.TryGetValue(InternalTokenHeader, out var provided) || provided.Count == 0)
            return (false, 401, ZohoErrorCodes.InternalAuthInvalid,
                $"Missing {InternalTokenHeader} header.");

        if (!SlowEquals(provided[0], expected))
            return (false, 403, ZohoErrorCodes.InternalAuthInvalid,
                $"Invalid {InternalTokenHeader}.");

        return (true, 200, string.Empty, string.Empty);
    }

    private static bool SlowEquals(string? a, string b)
    {
        if (a is null) return false;
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private static int ParseInt(string raw, int fallback, int min = 1, int max = int.MaxValue)
    {
        if (string.IsNullOrEmpty(raw)) return fallback;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return fallback;
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }

    private static bool TryParseIso(string raw, out DateTime utc)
    {
        utc = default;
        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return false;
        utc = dto.UtcDateTime;
        return true;
    }

    private static string ResolveRequestId(HttpContext ctx) =>
        ctx.Items["RequestId"] as string ?? ctx.TraceIdentifier;
}
