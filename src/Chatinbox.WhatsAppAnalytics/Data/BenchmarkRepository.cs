using System.Text;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Models;
using Npgsql;
using NpgsqlTypes;

namespace Chatinbox.WhatsAppAnalytics.Data;

/// <summary>
/// Repository for wa_benchmark_jobs and wa_benchmark_results.
/// Follows AnalyticsRepository patterns.
/// </summary>
public sealed class BenchmarkRepository
{
    private readonly AnalyticsConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public BenchmarkRepository(AnalyticsConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // wa_benchmark_jobs
    // ============================================================

    public async Task<int> CreateJobAsync(int tenantId, string databaseName, int? instanceId,
        int sampleSize, string? configJson, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO wa_benchmark_jobs (tenant_id, database_name, instance_id, sample_size, config_json, status, started_at)
            VALUES (@tid, @db, @iid, @ss, @cfg::jsonb, 'pending', NOW())
            RETURNING id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("db", databaseName);
        cmd.Parameters.AddWithValue("iid", (object?)instanceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ss", sampleSize);
        cmd.Parameters.AddWithValue("cfg", (object?)configJson ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is not int id)
            throw new InvalidOperationException("Failed to create benchmark job — no ID returned");
        return id;
    }

    public async Task UpdateJobStatusAsync(int id, string status, string? stageProgress = null, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_benchmark_jobs
            SET status = @status, stage_progress = @sp::jsonb
            WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("sp", (object?)stageProgress ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task CompleteJobAsync(int id, int actualSample, string? resultsSummary, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_benchmark_jobs
            SET status = 'completed', actual_sample = @actual, results_summary = @rs::jsonb, completed_at = NOW()
            WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("actual", actualSample);
        cmd.Parameters.AddWithValue("rs", (object?)resultsSummary ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task FailJobAsync(int id, string errorMessage, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_benchmark_jobs
            SET status = 'error', error_message = @err, completed_at = NOW()
            WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("err", errorMessage);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<BenchmarkJob?> GetJobAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, tenant_id, status, database_name, instance_id,
                   sample_size, actual_sample, config_json::text,
                   stage_progress::text, results_summary::text,
                   error_message, started_at, completed_at, created_at, updated_at
            FROM wa_benchmark_jobs WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new BenchmarkJob
        {
            Id = reader.GetInt32(0),
            TenantId = reader.GetInt32(1),
            Status = reader.GetString(2),
            DatabaseName = reader.GetString(3),
            InstanceId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            SampleSize = reader.GetInt32(5),
            ActualSample = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            ConfigJson = reader.IsDBNull(7) ? null : reader.GetString(7),
            StageProgress = reader.IsDBNull(8) ? null : reader.GetString(8),
            ResultsSummary = reader.IsDBNull(9) ? null : reader.GetString(9),
            ErrorMessage = reader.IsDBNull(10) ? null : reader.GetString(10),
            StartedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
            CompletedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            CreatedAt = reader.GetDateTime(13),
            UpdatedAt = reader.GetDateTime(14)
        };
    }

    // ============================================================
    // wa_benchmark_results
    // ============================================================

    /// <summary>
    /// Strips unpaired Unicode surrogates that crash Npgsql's UTF-8 encoder.
    /// WhatsApp messages occasionally contain lone surrogates (e.g. \uDCCC).
    /// </summary>
    private static string SanitizeForPostgres(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value!;

        var hasIssue = false;
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1])) { hasIssue = true; break; }
                i++; // skip the valid pair
            }
            else if (char.IsLowSurrogate(value[i]))
            {
                hasIssue = true; break;
            }
        }

        if (!hasIssue) return value;

        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    sb.Append(value[i]);
                    sb.Append(value[i + 1]);
                    i++;
                }
                // else: lone high surrogate — skip
            }
            else if (char.IsLowSurrogate(value[i]))
            {
                // lone low surrogate — skip
            }
            else
            {
                sb.Append(value[i]);
            }
        }

        return sb.ToString();
    }

    public async Task InsertResultsBatchAsync(List<BenchmarkResult> results, CancellationToken ct = default)
    {
        if (results.Count == 0) return;

        await using var conn = await _db.OpenConnectionAsync(ct);

        // Batch insert with parameterized values (50 at a time)
        const int batchSize = 50;
        for (var i = 0; i < results.Count; i += batchSize)
        {
            var batch = results.Skip(i).Take(batchSize).ToList();
            var sb = new StringBuilder();
            sb.Append(@"INSERT INTO wa_benchmark_results (
                benchmark_id, tenant_id, conversation_id, message_count, thread_text_masked,
                keyword_label, claude_haiku_label, claude_haiku_confidence, claude_haiku_evidence,
                claude_sonnet_label, claude_sonnet_confidence, claude_sonnet_evidence,
                gemini_flash_label, gemini_flash_confidence, gemini_flash_evidence,
                gemini_pro_label, gemini_pro_confidence, gemini_pro_evidence,
                gemini_3_flash_label, gemini_3_flash_confidence, gemini_3_flash_evidence,
                tiered_label, tiered_confidence, tiered_evidence,
                has_offer, ground_truth_label) VALUES ");

            await using var cmd = conn.CreateCommand();
            for (var j = 0; j < batch.Count; j++)
            {
                if (j > 0) sb.Append(',');
                var p = j * 26; // parameter offset (26 columns)
                sb.Append($"(@p{p},@p{p + 1},@p{p + 2},@p{p + 3},@p{p + 4},@p{p + 5},@p{p + 6},@p{p + 7},@p{p + 8},@p{p + 9},@p{p + 10},@p{p + 11},@p{p + 12},@p{p + 13},@p{p + 14},@p{p + 15},@p{p + 16},@p{p + 17},@p{p + 18},@p{p + 19},@p{p + 20},@p{p + 21},@p{p + 22},@p{p + 23},@p{p + 24},@p{p + 25})");

                var r = batch[j];
                cmd.Parameters.AddWithValue($"p{p}", r.BenchmarkId);
                cmd.Parameters.AddWithValue($"p{p + 1}", r.TenantId);
                cmd.Parameters.AddWithValue($"p{p + 2}", r.ConversationId);
                cmd.Parameters.AddWithValue($"p{p + 3}", r.MessageCount);
                cmd.Parameters.AddWithValue($"p{p + 4}", (object?)SanitizeForPostgres(r.ThreadTextMasked) ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 5}", (object?)r.KeywordLabel ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 6}", (object?)r.ClaudeHaikuLabel ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 7}", (object?)r.ClaudeHaikuConfidence ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 8}", (object?)SanitizeForPostgres(r.ClaudeHaikuEvidence) ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 9}", (object?)r.ClaudeSonnetLabel ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 10}", (object?)r.ClaudeSonnetConfidence ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 11}", (object?)SanitizeForPostgres(r.ClaudeSonnetEvidence) ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 12}", (object?)r.GeminiFlashLabel ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 13}", (object?)r.GeminiFlashConfidence ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 14}", (object?)SanitizeForPostgres(r.GeminiFlashEvidence) ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 15}", (object?)r.GeminiProLabel ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 16}", (object?)r.GeminiProConfidence ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 17}", (object?)SanitizeForPostgres(r.GeminiProEvidence) ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 18}", (object?)r.Gemini3FlashLabel ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 19}", (object?)r.Gemini3FlashConfidence ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 20}", (object?)SanitizeForPostgres(r.Gemini3FlashEvidence) ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 21}", (object?)r.TieredLabel ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 22}", (object?)r.TieredConfidence ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 23}", (object?)SanitizeForPostgres(r.TieredEvidence) ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 24}", (object?)r.HasOffer ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{p + 25}", (object?)r.GroundTruthLabel ?? DBNull.Value);
            }

            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
        }

        _logger.SystemInfo($"[BenchmarkRepo] Inserted {results.Count} benchmark results");
    }

    public async Task<List<BenchmarkResult>> GetResultsAsync(int benchmarkId,
        string? modelFilter = null, string? labelFilter = null, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var sb = new StringBuilder(@"
            SELECT id, benchmark_id, tenant_id, conversation_id, message_count,
                   keyword_label, claude_haiku_label, claude_haiku_confidence, claude_haiku_evidence,
                   claude_sonnet_label, claude_sonnet_confidence, claude_sonnet_evidence,
                   gemini_flash_label, gemini_flash_confidence, gemini_flash_evidence,
                   gemini_pro_label, gemini_pro_confidence, gemini_pro_evidence,
                   gemini_3_flash_label, gemini_3_flash_confidence, gemini_3_flash_evidence,
                   tiered_label, tiered_confidence, tiered_evidence,
                   ground_truth_label, created_at
            FROM wa_benchmark_results
            WHERE benchmark_id = @bid");
        cmd.Parameters.AddWithValue("bid", benchmarkId);

        // Optional label filter (for any model)
        if (!string.IsNullOrEmpty(labelFilter))
        {
            sb.Append(@" AND (keyword_label = @lf OR claude_haiku_label = @lf OR claude_sonnet_label = @lf
                         OR gemini_flash_label = @lf OR gemini_pro_label = @lf OR gemini_3_flash_label = @lf
                         OR tiered_label = @lf)");
            cmd.Parameters.AddWithValue("lf", labelFilter);
        }

        sb.Append(" ORDER BY conversation_id");
        cmd.CommandText = sb.ToString();

        var results = new List<BenchmarkResult>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new BenchmarkResult
            {
                Id = reader.GetInt64(0),
                BenchmarkId = reader.GetInt32(1),
                TenantId = reader.GetInt32(2),
                ConversationId = reader.GetString(3),
                MessageCount = reader.GetInt32(4),
                KeywordLabel = reader.IsDBNull(5) ? null : reader.GetString(5),
                ClaudeHaikuLabel = reader.IsDBNull(6) ? null : reader.GetString(6),
                ClaudeHaikuConfidence = reader.IsDBNull(7) ? null : reader.GetFloat(7),
                ClaudeHaikuEvidence = reader.IsDBNull(8) ? null : reader.GetString(8),
                ClaudeSonnetLabel = reader.IsDBNull(9) ? null : reader.GetString(9),
                ClaudeSonnetConfidence = reader.IsDBNull(10) ? null : reader.GetFloat(10),
                ClaudeSonnetEvidence = reader.IsDBNull(11) ? null : reader.GetString(11),
                GeminiFlashLabel = reader.IsDBNull(12) ? null : reader.GetString(12),
                GeminiFlashConfidence = reader.IsDBNull(13) ? null : reader.GetFloat(13),
                GeminiFlashEvidence = reader.IsDBNull(14) ? null : reader.GetString(14),
                GeminiProLabel = reader.IsDBNull(15) ? null : reader.GetString(15),
                GeminiProConfidence = reader.IsDBNull(16) ? null : reader.GetFloat(16),
                GeminiProEvidence = reader.IsDBNull(17) ? null : reader.GetString(17),
                Gemini3FlashLabel = reader.IsDBNull(18) ? null : reader.GetString(18),
                Gemini3FlashConfidence = reader.IsDBNull(19) ? null : reader.GetFloat(19),
                Gemini3FlashEvidence = reader.IsDBNull(20) ? null : reader.GetString(20),
                TieredLabel = reader.IsDBNull(21) ? null : reader.GetString(21),
                TieredConfidence = reader.IsDBNull(22) ? null : reader.GetFloat(22),
                TieredEvidence = reader.IsDBNull(23) ? null : reader.GetString(23),
                GroundTruthLabel = reader.IsDBNull(24) ? null : reader.GetString(24),
                CreatedAt = reader.GetDateTime(25)
            });
        }

        return results;
    }

    public async Task<int> UpdateGroundTruthAsync(int benchmarkId, List<GroundTruthEntry> labels, CancellationToken ct = default)
    {
        if (labels.Count == 0) return 0;

        await using var conn = await _db.OpenConnectionAsync(ct);
        var updated = 0;

        foreach (var entry in labels)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE wa_benchmark_results
                SET ground_truth_label = @label
                WHERE benchmark_id = @bid AND conversation_id = @cid";
            cmd.Parameters.AddWithValue("bid", benchmarkId);
            cmd.Parameters.AddWithValue("cid", entry.ConversationId);
            cmd.Parameters.AddWithValue("label", entry.Label);
            updated += await cmd.ExecuteNonQueryAsync(ct);
        }

        return updated;
    }
}
