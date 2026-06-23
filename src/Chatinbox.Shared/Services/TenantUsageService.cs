using System.Collections.Concurrent;
using Chatinbox.Shared.Logging;
using Npgsql;

namespace Chatinbox.Shared.Services;

/// <summary>
/// Thread-safe monthly message counter with in-memory cache and periodic DB sync.
/// Tracks messages_sent per tenant per month against PlanQuotas.MessagesPerMonth.
///
/// Fail-open: DB unavailable → allow request through (same as TenantPlanCache).
/// Register as singleton in services that send billable messages (Automation, WebChat).
/// </summary>
public sealed class TenantUsageService
{
    private readonly string _connectionString;
    private readonly JsonLinesLogger _logger;
    private readonly ConcurrentDictionary<int, UsageEntry> _counters = new();

    private const int SyncThreshold = 50;
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(5);

    public TenantUsageService(string connectionString, JsonLinesLogger logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Increment message counter and check against quota limit.
    /// Returns (Allowed, CurrentCount, Limit).
    /// If MessagesPerMonth == -1 → unlimited, always allowed.
    /// </summary>
    public async Task<(bool Allowed, int Current, int Limit)> IncrementMessageAndCheckAsync(
        int tenantId, PlanQuotas quotas, CancellationToken ct = default)
    {
        if (quotas.MessagesPerMonth < 0)
            return (true, -1, -1);

        var entry = _counters.GetOrAdd(tenantId, tid =>
        {
            var e = new UsageEntry();
            _ = Task.Run(() => SeedFromDbAsync(tid, e, default));
            return e;
        });

        var newCount = Interlocked.Increment(ref entry.InMemoryCount);

        if (newCount - entry.LastSyncedCount >= SyncThreshold ||
            DateTime.UtcNow - entry.LastSyncAt > SyncInterval)
        {
            _ = Task.Run(() => SyncToDbAsync(tenantId, entry, default));
        }

        return (newCount <= quotas.MessagesPerMonth, newCount, quotas.MessagesPerMonth);
    }

    /// <summary>
    /// Get current month usage for a tenant (reads from DB, not cache).
    /// Used by ops endpoints for accurate reporting.
    /// </summary>
    public async Task<int> GetCurrentMonthUsageAsync(int tenantId, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT messages_sent FROM tenant_usage WHERE tenant_id = @tid AND period_month = date_trunc('month', NOW())::date",
                conn);
            cmd.Parameters.AddWithValue("tid", tenantId);

            var result = await cmd.ExecuteScalarAsync(ct);
            return result is int count ? count : 0;
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[TenantUsageService] DB error reading usage for tenant {tenantId}: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Flush all dirty counters to DB. Call on service shutdown.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        foreach (var (tenantId, entry) in _counters)
        {
            if (entry.IsDirty)
                await SyncToDbAsync(tenantId, entry, ct);
        }
    }

    private async Task SeedFromDbAsync(int tenantId, UsageEntry entry, CancellationToken ct)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT messages_sent FROM tenant_usage WHERE tenant_id = @tid AND period_month = date_trunc('month', NOW())::date",
                conn);
            cmd.Parameters.AddWithValue("tid", tenantId);

            var result = await cmd.ExecuteScalarAsync(ct);
            var dbCount = result is int c ? c : 0;

            Interlocked.Exchange(ref entry.InMemoryCount, dbCount);
            entry.LastSyncedCount = dbCount;
            entry.LastSyncAt = DateTime.UtcNow;
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[TenantUsageService] DB seed failed for tenant {tenantId}: {ex.Message}");
        }
    }

    private async Task SyncToDbAsync(int tenantId, UsageEntry entry, CancellationToken ct)
    {
        var current = Volatile.Read(ref entry.InMemoryCount);
        var delta = current - entry.LastSyncedCount;
        if (delta <= 0) return;

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO tenant_usage (tenant_id, period_month, messages_sent, updated_at)
                VALUES (@tid, date_trunc('month', NOW())::date, @delta, NOW())
                ON CONFLICT (tenant_id, period_month)
                DO UPDATE SET
                    messages_sent = tenant_usage.messages_sent + @delta,
                    updated_at = NOW()", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("delta", delta);

            await cmd.ExecuteNonQueryAsync(ct);
            entry.LastSyncedCount = current;
            entry.LastSyncAt = DateTime.UtcNow;
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[TenantUsageService] DB sync failed for tenant {tenantId}: {ex.Message}");
        }
    }
}

internal sealed class UsageEntry
{
    public int InMemoryCount;
    public int LastSyncedCount;
    public DateTime LastSyncAt;
    public bool IsDirty => InMemoryCount > LastSyncedCount;
}
