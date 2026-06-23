using System.Text;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Models;
using Npgsql;
using NpgsqlTypes;

namespace Chatinbox.WhatsAppAnalytics.Data;

/// <summary>
/// Repository for wa_conversation_outcomes and wa_batch_jobs.
/// </summary>
public sealed class ConversationOutcomeRepository
{
    private readonly AnalyticsConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public ConversationOutcomeRepository(AnalyticsConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // wa_batch_jobs
    // ============================================================

    public async Task<int> CreateBatchJobAsync(int tenantId, string databaseName, int? instanceId,
        string? sector, string jobType, int lookbackDays, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO wa_batch_jobs (tenant_id, database_name, instance_id, sector, job_type, lookback_days, status, started_at)
            VALUES (@tid, @db, @iid, @sector, @jt, @ld, 'pending', NOW())
            RETURNING id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("db", databaseName);
        cmd.Parameters.AddWithValue("iid", (object?)instanceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sector", (object?)sector ?? DBNull.Value);
        cmd.Parameters.AddWithValue("jt", jobType);
        cmd.Parameters.AddWithValue("ld", lookbackDays);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is not int id)
            throw new InvalidOperationException("Failed to create batch job — no ID returned");
        return id;
    }

    public async Task UpdateBatchStatusAsync(int id, string status, string? stageProgress = null,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_batch_jobs
            SET status = @status, stage_progress = @sp, updated_at = NOW()
            WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("sp", (object?)stageProgress ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateBatchCountsAsync(int id, int? totalCandidates = null,
        int? alreadyClassified = null, int? classifiedCount = null, int? errorCount = null,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        var setClauses = new List<string> { "updated_at = NOW()" };
        if (totalCandidates.HasValue) { setClauses.Add("total_candidates = @tc"); cmd.Parameters.AddWithValue("tc", totalCandidates.Value); }
        if (alreadyClassified.HasValue) { setClauses.Add("already_classified = @ac"); cmd.Parameters.AddWithValue("ac", alreadyClassified.Value); }
        if (classifiedCount.HasValue) { setClauses.Add("classified_count = @cc"); cmd.Parameters.AddWithValue("cc", classifiedCount.Value); }
        if (errorCount.HasValue) { setClauses.Add("error_count = @ec"); cmd.Parameters.AddWithValue("ec", errorCount.Value); }

        cmd.CommandText = $"UPDATE wa_batch_jobs SET {string.Join(", ", setClauses)} WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task CompleteBatchJobAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_batch_jobs
            SET status = 'completed', completed_at = NOW(), updated_at = NOW()
            WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task FailBatchJobAsync(int id, string errorMessage, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_batch_jobs
            SET status = 'failed', error_message = @err, completed_at = NOW(), updated_at = NOW()
            WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("err", errorMessage);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<BatchJob?> GetBatchJobAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, tenant_id, database_name, instance_id, sector, job_type, status,
                   lookback_days, total_candidates, already_classified, classified_count, error_count,
                   stage_progress, error_message, started_at, completed_at, created_at, updated_at
            FROM wa_batch_jobs WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new BatchJob
        {
            Id = reader.GetInt32(0),
            TenantId = reader.GetInt32(1),
            DatabaseName = reader.GetString(2),
            InstanceId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            Sector = reader.IsDBNull(4) ? null : reader.GetString(4),
            JobType = reader.GetString(5),
            Status = reader.GetString(6),
            LookbackDays = reader.GetInt32(7),
            TotalCandidates = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            AlreadyClassified = reader.GetInt32(9),
            ClassifiedCount = reader.GetInt32(10),
            ErrorCount = reader.GetInt32(11),
            StageProgress = reader.IsDBNull(12) ? null : reader.GetString(12),
            ErrorMessage = reader.IsDBNull(13) ? null : reader.GetString(13),
            StartedAt = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
            CompletedAt = reader.IsDBNull(15) ? null : reader.GetDateTime(15),
            CreatedAt = reader.GetDateTime(16),
            UpdatedAt = reader.GetDateTime(17)
        };
    }

    // ============================================================
    // wa_conversation_outcomes
    // ============================================================

    /// <summary>
    /// Get already-classified conversation IDs for a tenant (used to skip re-classification).
    /// </summary>
    public async Task<HashSet<string>> GetClassifiedConversationIdsAsync(int tenantId,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT conversation_id FROM wa_conversation_outcomes WHERE tenant_id = @tid";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(reader.GetString(0));
        return result;
    }

    /// <summary>
    /// Upsert a batch of conversation outcomes (INSERT ON CONFLICT UPDATE).
    /// </summary>
    public async Task UpsertOutcomesAsync(List<ConversationOutcome> outcomes, CancellationToken ct = default)
    {
        if (outcomes.Count == 0) return;

        const int batchSize = 50;
        for (var offset = 0; offset < outcomes.Count; offset += batchSize)
        {
            var batch = outcomes.Skip(offset).Take(batchSize).ToList();
            await UpsertBatchInternalAsync(batch, ct);
        }
    }

    private async Task UpsertBatchInternalAsync(List<ConversationOutcome> batch, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var sb = new StringBuilder();
        sb.AppendLine(@"INSERT INTO wa_conversation_outcomes
            (tenant_id, database_name, instance_id, conversation_id, sector,
             outcome_label, confidence, has_offer, evidence, model_version, classified_at)
            VALUES ");

        for (var i = 0; i < batch.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.AppendLine($"(@tid{i}, @db{i}, @iid{i}, @cid{i}, @sec{i}, @lbl{i}, @conf{i}, @ho{i}, @ev{i}, @mv{i}, NOW())");
            var o = batch[i];
            cmd.Parameters.AddWithValue($"tid{i}", o.TenantId);
            cmd.Parameters.AddWithValue($"db{i}", o.DatabaseName);
            cmd.Parameters.AddWithValue($"iid{i}", (object?)o.InstanceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue($"cid{i}", o.ConversationId);
            cmd.Parameters.AddWithValue($"sec{i}", (object?)o.Sector ?? DBNull.Value);
            cmd.Parameters.AddWithValue($"lbl{i}", o.OutcomeLabel);
            cmd.Parameters.AddWithValue($"conf{i}", o.Confidence);
            cmd.Parameters.AddWithValue($"ho{i}", o.HasOffer);
            cmd.Parameters.AddWithValue($"ev{i}", (object?)o.Evidence ?? DBNull.Value);
            cmd.Parameters.AddWithValue($"mv{i}", o.ModelVersion);
        }

        sb.AppendLine(@"ON CONFLICT (tenant_id, conversation_id) DO UPDATE SET
            outcome_label = EXCLUDED.outcome_label,
            confidence = EXCLUDED.confidence,
            has_offer = EXCLUDED.has_offer,
            evidence = EXCLUDED.evidence,
            model_version = EXCLUDED.model_version,
            classified_at = NOW(),
            updated_at = NOW()");

        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Get outcome label distribution for a tenant.
    /// </summary>
    public async Task<Dictionary<string, int>> GetLabelDistributionAsync(int tenantId,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT outcome_label, COUNT(*)::INT
            FROM wa_conversation_outcomes
            WHERE tenant_id = @tid
            GROUP BY outcome_label
            ORDER BY COUNT(*) DESC";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = new Dictionary<string, int>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString(0)] = reader.GetInt32(1);
        return result;
    }
}
