using System.Text.Json;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs.Integrations;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Integrations.Data;

public sealed class IntegrationsRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public IntegrationsRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ================================================================
    // Integration Accounts
    // ================================================================

    public async Task<IntegrationAccountResponse?> GetAccountAsync(
        int tenantId, string provider, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, provider, seller_id, status, last_sync_at, error_message, created_at
            FROM integration_accounts
            WHERE tenant_id = @tid AND provider = @provider";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("provider", provider);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadAccountResponse(reader);
        return null;
    }

    public async Task<List<IntegrationAccountResponse>> ListAccountsAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, provider, seller_id, status, last_sync_at, error_message, created_at
            FROM integration_accounts
            WHERE tenant_id = @tid
            ORDER BY created_at DESC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var accounts = new List<IntegrationAccountResponse>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            accounts.Add(ReadAccountResponse(reader));
        return accounts;
    }

    public async Task<int> UpsertAccountAsync(
        int tenantId, string provider, string? apiKeyEnc, string? apiSecretEnc,
        string? sellerId, string? settingsJson, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO integration_accounts (tenant_id, provider, api_key_enc, api_secret_enc, seller_id, settings_json)
            VALUES (@tid, @provider, @apiKey, @apiSecret, @sellerId, @settings::jsonb)
            ON CONFLICT (tenant_id, provider)
            DO UPDATE SET
                api_key_enc = EXCLUDED.api_key_enc,
                api_secret_enc = EXCLUDED.api_secret_enc,
                seller_id = EXCLUDED.seller_id,
                settings_json = EXCLUDED.settings_json,
                status = 'active',
                error_message = NULL,
                updated_at = NOW()
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("provider", provider);
        cmd.Parameters.AddWithValue("apiKey", (object?)apiKeyEnc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("apiSecret", (object?)apiSecretEnc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sellerId", (object?)sellerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("settings", (object?)settingsJson ?? "{}");

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task UpdateAccountStatusAsync(
        int tenantId, string provider, string status, string? errorMessage,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE integration_accounts
            SET status = @status, error_message = @error, updated_at = NOW()
            WHERE tenant_id = @tid AND provider = @provider";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("provider", provider);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("error", (object?)errorMessage ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateLastSyncAsync(
        int tenantId, string provider, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE integration_accounts
            SET last_sync_at = NOW(), updated_at = NOW()
            WHERE tenant_id = @tid AND provider = @provider";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("provider", provider);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Get raw account credentials for provider API calls.
    /// NEVER expose these in API responses.
    /// </summary>
    public async Task<(string? ApiKeyEnc, string? ApiSecretEnc, string? SellerId)?> GetAccountCredentialsAsync(
        int tenantId, string provider, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT api_key_enc, api_secret_enc, seller_id
            FROM integration_accounts
            WHERE tenant_id = @tid AND provider = @provider AND status = 'active'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("provider", provider);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2)
        );
    }

    // ================================================================
    // Orders Cache
    // ================================================================

    public async Task UpsertOrderAsync(
        int tenantId, string provider, string externalOrderId,
        string? customerPhone, string? customerName,
        string? trackingCode, string? cargoProvider,
        string orderStatus, decimal? totalAmount, string currency,
        string? orderDataJson, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO orders_cache
                (tenant_id, provider, external_order_id, customer_phone, customer_name,
                 tracking_code, cargo_provider, order_status, total_amount, currency,
                 order_data_json, synced_at)
            VALUES
                (@tid, @provider, @extId, @phone, @name,
                 @tracking, @cargo, @status, @amount, @currency,
                 @data::jsonb, NOW())
            ON CONFLICT (tenant_id, provider, external_order_id)
            DO UPDATE SET
                customer_phone = EXCLUDED.customer_phone,
                customer_name = EXCLUDED.customer_name,
                tracking_code = EXCLUDED.tracking_code,
                cargo_provider = EXCLUDED.cargo_provider,
                order_status = EXCLUDED.order_status,
                total_amount = EXCLUDED.total_amount,
                order_data_json = EXCLUDED.order_data_json,
                synced_at = NOW(),
                updated_at = NOW()";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("provider", provider);
        cmd.Parameters.AddWithValue("extId", externalOrderId);
        cmd.Parameters.AddWithValue("phone", (object?)customerPhone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("name", (object?)customerName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tracking", (object?)trackingCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cargo", (object?)cargoProvider ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", orderStatus);
        cmd.Parameters.AddWithValue("amount", (object?)totalAmount ?? DBNull.Value);
        cmd.Parameters.AddWithValue("currency", currency);
        cmd.Parameters.AddWithValue("data", (object?)orderDataJson ?? "{}");

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<OrderCacheResponse>> QueryOrdersAsync(
        int tenantId, string? provider, string? status,
        string? customerPhone, DateTime? fromDate, DateTime? toDate,
        int limit, int offset, CancellationToken ct = default)
    {
        var conditions = new List<string> { "tenant_id = @tid" };
        var parameters = new List<NpgsqlParameter> { new("tid", tenantId) };

        if (!string.IsNullOrEmpty(provider))
        {
            conditions.Add("provider = @provider");
            parameters.Add(new("provider", provider));
        }
        if (!string.IsNullOrEmpty(status))
        {
            conditions.Add("order_status = @status");
            parameters.Add(new("status", status));
        }
        if (!string.IsNullOrEmpty(customerPhone))
        {
            conditions.Add("customer_phone = @phone");
            parameters.Add(new("phone", customerPhone));
        }
        if (fromDate.HasValue)
        {
            conditions.Add("created_at >= @fromDate");
            parameters.Add(new("fromDate", fromDate.Value));
        }
        if (toDate.HasValue)
        {
            conditions.Add("created_at <= @toDate");
            parameters.Add(new("toDate", toDate.Value));
        }

        parameters.Add(new("limit", Math.Min(limit, 200)));
        parameters.Add(new("offset", Math.Max(offset, 0)));

        var sql = $@"
            SELECT id, provider, external_order_id, customer_phone, customer_name,
                   tracking_code, cargo_provider, order_status, total_amount, currency,
                   synced_at, created_at
            FROM orders_cache
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY created_at DESC LIMIT @limit OFFSET @offset";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters.ToArray());

        var orders = new List<OrderCacheResponse>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            orders.Add(ReadOrderResponse(reader));
        return orders;
    }

    public async Task<OrderCacheResponse?> GetOrderByExternalIdAsync(
        int tenantId, string provider, string externalOrderId,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, provider, external_order_id, customer_phone, customer_name,
                   tracking_code, cargo_provider, order_status, total_amount, currency,
                   synced_at, created_at
            FROM orders_cache
            WHERE tenant_id = @tid AND provider = @provider AND external_order_id = @extId";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("provider", provider);
        cmd.Parameters.AddWithValue("extId", externalOrderId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadOrderResponse(reader);
        return null;
    }

    /// <summary>
    /// Get order's previous status to detect status changes for proactive notifications.
    /// </summary>
    public async Task<string?> GetOrderPreviousStatusAsync(
        int tenantId, string provider, string externalOrderId,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT order_status FROM orders_cache
            WHERE tenant_id = @tid AND provider = @provider AND external_order_id = @extId";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("provider", provider);
        cmd.Parameters.AddWithValue("extId", externalOrderId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    // ================================================================
    // Cargo Tracking Events
    // ================================================================

    public async Task InsertCargoEventAsync(
        int tenantId, int orderId, string cargoProvider, string trackingCode,
        string status, string? location, DateTime eventTime, string? rawDataJson,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO cargo_tracking_events
                (tenant_id, order_id, cargo_provider, tracking_code, status, location, event_time, raw_data_json)
            VALUES
                (@tid, @orderId, @cargo, @tracking, @status, @location, @eventTime, @raw::jsonb)";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("orderId", orderId);
        cmd.Parameters.AddWithValue("cargo", cargoProvider);
        cmd.Parameters.AddWithValue("tracking", trackingCode);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("location", (object?)location ?? DBNull.Value);
        cmd.Parameters.AddWithValue("eventTime", eventTime);
        cmd.Parameters.AddWithValue("raw", (object?)rawDataJson ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<(string Status, string? Location, DateTime EventTime)>> GetCargoEventsAsync(
        int tenantId, string trackingCode, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT status, location, event_time
            FROM cargo_tracking_events
            WHERE tenant_id = @tid AND tracking_code = @tracking
            ORDER BY event_time ASC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("tracking", trackingCode);

        var events = new List<(string, string?, DateTime)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            events.Add((
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetDateTime(2)
            ));
        }
        return events;
    }

    // ================================================================
    // Active accounts for sync
    // ================================================================

    // NOTE: Intentionally no tenant_id filter — sync iterates ALL active accounts across tenants
    public async Task<List<(int TenantId, string Provider, string? ApiKeyEnc, string? ApiSecretEnc, string? SellerId)>>
        GetActiveAccountsForSyncAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT tenant_id, provider, api_key_enc, api_secret_enc, seller_id
            FROM integration_accounts
            WHERE status = 'active'
            ORDER BY last_sync_at ASC NULLS FIRST
            LIMIT 50";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);

        var accounts = new List<(int, string, string?, string?, string?)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            accounts.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)
            ));
        }
        return accounts;
    }

    // ================================================================
    // Review Alerts (PKT-6B1: GR-3.16)
    // ================================================================

    /// <summary>
    /// Insert a review alert from external webhook. ON CONFLICT updates review_text + rating.
    /// Returns the review alert id.
    /// </summary>
    public async Task<int> UpsertReviewAlertAsync(
        int tenantId, string provider, string? externalReviewId,
        int rating, string? reviewText, string? customerPhone, string? orderId,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO review_alerts
                (tenant_id, provider, external_review_id, rating, review_text,
                 customer_phone, order_id)
            VALUES
                (@tid, @provider, @extId, @rating, @text, @phone, @orderId)
            ON CONFLICT (tenant_id, provider, external_review_id)
                WHERE external_review_id IS NOT NULL
            DO UPDATE SET
                rating = EXCLUDED.rating,
                review_text = EXCLUDED.review_text,
                updated_at = NOW()
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("provider", provider);
        cmd.Parameters.AddWithValue("extId", (object?)externalReviewId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("rating", rating);
        cmd.Parameters.AddWithValue("text", (object?)reviewText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone", (object?)customerPhone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("orderId", (object?)orderId ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Get review alerts for a tenant, optionally filtered by status.
    /// </summary>
    public async Task<List<(int Id, string Provider, string? ExternalReviewId, int Rating,
        string? ReviewText, string? CustomerPhone, string RecoveryStatus,
        int RecoveryAttempt, DateTime CreatedAt)>> GetReviewAlertsAsync(
        int tenantId, string? status, int limit, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, provider, external_review_id, rating, review_text,
                   customer_phone, recovery_status, recovery_attempt, created_at
            FROM review_alerts
            WHERE tenant_id = @tid
              AND (@status IS NULL OR recovery_status = @status)
            ORDER BY created_at DESC LIMIT @limit";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("limit", Math.Min(limit, 100));

        var alerts = new List<(int, string, string?, int, string?, string?, string, int, DateTime)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            alerts.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetDateTime(8)
            ));
        }
        return alerts;
    }

    /// <summary>
    /// Update recovery status and increment attempt counter.
    /// </summary>
    public async Task<bool> UpdateRecoveryStatusAsync(
        int tenantId, int alertId, string newStatus, string? recoveryMessage,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE review_alerts
            SET recovery_status = @status,
                recovery_message = COALESCE(@msg, recovery_message),
                recovery_attempt = recovery_attempt + 1,
                last_attempt_at = NOW(),
                resolved_at = CASE WHEN @status IN ('resolved', 'unresolved', 'expired') THEN NOW() ELSE resolved_at END,
                updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", alertId);
        cmd.Parameters.AddWithValue("status", newStatus);
        cmd.Parameters.AddWithValue("msg", (object?)recoveryMessage ?? DBNull.Value);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    /// <summary>
    /// Get review recovery stats: total, by status, by provider.
    /// </summary>
    public async Task<(int Total, Dictionary<string, int> ByStatus, Dictionary<string, int> ByProvider)>
        GetReviewRecoveryStatsAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT recovery_status, provider, COUNT(*) as cnt
            FROM review_alerts
            WHERE tenant_id = @tid
            GROUP BY recovery_status, provider";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var byStatus = new Dictionary<string, int>();
        var byProvider = new Dictionary<string, int>();
        var total = 0;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var status = reader.GetString(0);
            var provider = reader.GetString(1);
            var count = reader.GetInt32(2);
            total += count;

            byStatus[status] = byStatus.GetValueOrDefault(status) + count;
            byProvider[provider] = byProvider.GetValueOrDefault(provider) + count;
        }

        return (total, byStatus, byProvider);
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static IntegrationAccountResponse ReadAccountResponse(NpgsqlDataReader reader)
    {
        return new IntegrationAccountResponse
        {
            Id = reader.GetInt32(0),
            Provider = reader.GetString(1),
            SellerId = reader.IsDBNull(2) ? null : reader.GetString(2),
            Status = reader.GetString(3),
            LastSyncAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
            ErrorMessage = reader.IsDBNull(5) ? null : reader.GetString(5),
            CreatedAt = reader.GetDateTime(6)
        };
    }

    private static OrderCacheResponse ReadOrderResponse(NpgsqlDataReader reader)
    {
        return new OrderCacheResponse
        {
            Id = reader.GetInt32(0),
            Provider = reader.GetString(1),
            ExternalOrderId = reader.GetString(2),
            CustomerPhone = reader.IsDBNull(3) ? null : reader.GetString(3),
            CustomerName = reader.IsDBNull(4) ? null : reader.GetString(4),
            TrackingCode = reader.IsDBNull(5) ? null : reader.GetString(5),
            CargoProvider = reader.IsDBNull(6) ? null : reader.GetString(6),
            OrderStatus = reader.GetString(7),
            TotalAmount = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
            Currency = reader.GetString(9),
            SyncedAt = reader.GetDateTime(10),
            CreatedAt = reader.GetDateTime(11)
        };
    }
}
