// Adim 3 Paket 3-B1: Dashboard UI icin paginated+filtered sync log + manuel retry endpoint'leri.
//   GET  /api/v1/zoho/sync-log                  -> tenant JWT, offset-based pagination + filtreler.
//   POST /api/v1/zoho/sync-log/{id}/retry       -> tenant JWT, failed row'u pending'e set eder.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Integrations.Data;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Zoho;
using Invekto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Invekto.Integrations.Services.Zoho;

public static class ZohoSyncLogEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize     = 100;

    // Whitelist filter values to avoid arbitrary column values driving SQL (defense-in-depth;
    // repository already uses parameterized queries).
    private static readonly HashSet<string> AllowedStatusValues =
        new(StringComparer.OrdinalIgnoreCase) { "pending", "failed", "success" };

    public static IEndpointRouteBuilder MapZohoSyncLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/zoho/sync-log", async (
            HttpContext ctx,
            ZohoSyncLogRepository repo,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var tenantContext = ctx.Items["TenantContext"] as TenantContext;
            if (tenantContext is null)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
                    statusCode: 401);

            var query = ctx.Request.Query;
            var page     = ParseInt(query["page"].ToString(), fallback: 1, min: 1);
            var pageSize = ParseInt(query["pageSize"].ToString(), fallback: DefaultPageSize, min: 1, max: MaxPageSize);

            var rawStatus = query["status"].ToString();
            string? statusFilter = null;
            if (!string.IsNullOrEmpty(rawStatus))
            {
                if (!AllowedStatusValues.Contains(rawStatus))
                    return Results.Json(
                        ErrorResponse.Create(ErrorCodes.GeneralValidation,
                            $"Invalid status filter '{rawStatus}'. Allowed: pending, failed, success.",
                            requestId),
                        statusCode: 400);
                statusFilter = rawStatus.ToLowerInvariant();
            }

            var rawEvent = query["event"].ToString();
            string? eventFilter = string.IsNullOrEmpty(rawEvent) ? null : rawEvent;

            var rawFrom = query["from"].ToString();
            var rawTo   = query["to"].ToString();
            DateTime? fromUtc = null;
            DateTime? toUtc   = null;
            if (!string.IsNullOrEmpty(rawFrom))
            {
                if (!TryParseIso(rawFrom, out var f))
                    return Results.Json(
                        ErrorResponse.Create(ErrorCodes.GeneralValidation,
                            $"Invalid 'from' date '{rawFrom}'. Expected ISO8601 (e.g. 2026-04-16T00:00:00Z).",
                            requestId),
                        statusCode: 400);
                fromUtc = f;
            }
            if (!string.IsNullOrEmpty(rawTo))
            {
                if (!TryParseIso(rawTo, out var t))
                    return Results.Json(
                        ErrorResponse.Create(ErrorCodes.GeneralValidation,
                            $"Invalid 'to' date '{rawTo}'. Expected ISO8601 (e.g. 2026-04-16T23:59:59Z).",
                            requestId),
                        statusCode: 400);
                toUtc = t;
            }

            var (rows, total) = await repo.ListForDashboardAsync(
                tenantContext.TenantId, statusFilter, eventFilter, fromUtc, toUtc, page, pageSize, ct).ConfigureAwait(false);

            var items = new List<ZohoSyncLogEntryDto>(rows.Count);
            foreach (var r in rows)
            {
                items.Add(new ZohoSyncLogEntryDto
                {
                    Id               = r.Id,
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

            return Results.Ok(new ZohoSyncLogPageResponse
            {
                Items      = items,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = total,
            });
        });

        app.MapPost("/api/v1/zoho/sync-log/{id:long}/retry", async (
            long id,
            HttpContext ctx,
            ZohoSyncLogRepository repo,
            CancellationToken ct) =>
        {
            var requestId = ResolveRequestId(ctx);
            var tenantContext = ctx.Items["TenantContext"] as TenantContext;
            if (tenantContext is null)
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
                    statusCode: 401);

            var resetOk = await repo.ResetForRetryAsync(tenantContext.TenantId, id, ct).ConfigureAwait(false);
            if (resetOk)
            {
                return Results.Ok(new ZohoSyncLogRetryResponse
                {
                    RetriedId       = id,
                    NewAttemptCount = 0,
                });
            }

            // Reset returned 0 rows — differentiate 404 (not found / wrong tenant) vs 409 (wrong state).
            var row = await repo.GetAsync(tenantContext.TenantId, id, ct).ConfigureAwait(false);
            if (row is null)
                return Results.Json(
                    ErrorResponse.Create(ZohoErrorCodes.SyncLogNotFound,
                        $"Zoho sync log row id={id} not found for this tenant.",
                        requestId),
                    statusCode: 404);

            return Results.Json(
                ErrorResponse.Create(ZohoErrorCodes.SyncLogNotRetryable,
                    $"Zoho sync log row id={id} has status '{row.Status}'; only 'failed' rows can be manually retried.",
                    requestId),
                statusCode: 409);
        });

        return app;
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
