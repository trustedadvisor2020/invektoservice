using System.Text;
using System.Text.Json;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Models;
using Npgsql;
using NpgsqlTypes;

namespace Invekto.WhatsAppAnalytics.Data;

/// <summary>
/// Repository for all WhatsApp Analytics DB operations.
/// Phase A: wa_analyses, wa_messages, wa_conversations, wa_metadata.
/// Phase B (PKT-4): wa_intents, wa_sentiments, wa_products, wa_prices, wa_faq_pairs, wa_faq_clusters + query layer.
/// </summary>
public sealed class AnalyticsRepository
{
    private readonly AnalyticsConnectionFactory _db;
    private readonly JsonLinesLogger _logger;
    private const int BatchSize = 50;

    public AnalyticsRepository(AnalyticsConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // Health
    // ============================================================

    public async Task<bool> CheckConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"[AnalyticsRepository] Health check failed: {ex.Message}");
            return false;
        }
    }

    // ============================================================
    // wa_analyses CRUD
    // ============================================================

    public async Task<int> CreateAnalysisAsync(int tenantId, string sourceFileName, string? configJson, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO wa_analyses (tenant_id, source_file_name, config_json, status, started_at)
            VALUES (@tid, @src, @cfg::jsonb, 'pending', NOW())
            RETURNING id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("src", sourceFileName);
        cmd.Parameters.AddWithValue("cfg", (object?)configJson ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (int)result!;
    }

    public async Task<AnalysisJob?> GetAnalysisAsync(int tenantId, int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, tenant_id, status, source_file_name, config_json::text,
                   total_messages, total_conversations, stage_progress::text,
                   error_message, started_at, completed_at, created_at, updated_at
            FROM wa_analyses
            WHERE id = @aid AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ReadAnalysisJob(reader);
    }

    public async Task<(List<AnalysisJob> Items, int Total)> ListAnalysesAsync(int tenantId, int page, int limit, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        // Count
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM wa_analyses WHERE tenant_id = @tid";
        countCmd.Parameters.AddWithValue("tid", tenantId);
        var total = (int)(long)(await countCmd.ExecuteScalarAsync(ct))!;

        // List
        await using var listCmd = conn.CreateCommand();
        listCmd.CommandText = @"
            SELECT id, tenant_id, status, source_file_name, config_json::text,
                   total_messages, total_conversations, stage_progress::text,
                   error_message, started_at, completed_at, created_at, updated_at
            FROM wa_analyses
            WHERE tenant_id = @tid
            ORDER BY created_at DESC
            LIMIT @lim OFFSET @off";
        listCmd.Parameters.AddWithValue("tid", tenantId);
        listCmd.Parameters.AddWithValue("lim", limit);
        listCmd.Parameters.AddWithValue("off", (page - 1) * limit);

        var items = new List<AnalysisJob>();
        await using var reader = await listCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(ReadAnalysisJob(reader));

        return (items, total);
    }

    public async Task UpdateAnalysisStatusAsync(int analysisId, string status, string? stageProgressJson = null, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_analyses
            SET status = @st, stage_progress = @sp::jsonb, updated_at = NOW()
            WHERE id = @aid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("st", status);
        cmd.Parameters.AddWithValue("sp", (object?)stageProgressJson ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAnalysisTotalsAsync(int analysisId, int totalMessages, int totalConversations, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_analyses
            SET total_messages = @tm, total_conversations = @tc, updated_at = NOW()
            WHERE id = @aid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tm", totalMessages);
        cmd.Parameters.AddWithValue("tc", totalConversations);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task CompleteAnalysisAsync(int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_analyses
            SET status = 'completed', completed_at = NOW(), updated_at = NOW(),
                stage_progress = NULL
            WHERE id = @aid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task FailAnalysisAsync(int analysisId, string errorMessage, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_analyses
            SET status = 'error', error_message = @err, completed_at = NOW(), updated_at = NOW()
            WHERE id = @aid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("err", errorMessage);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> DeleteAnalysisAsync(int tenantId, int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM wa_analyses WHERE id = @aid AND tenant_id = @tid RETURNING id";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct);
    }

    /// <summary>
    /// Atomically claim analyses stuck in non-terminal state for restart recovery.
    /// Uses UPDATE ... RETURNING with FOR UPDATE SKIP LOCKED to prevent double-processing.
    /// Stale timeout (30 min) ensures actively-processing analyses are not re-claimed.
    /// </summary>
    public async Task<List<AnalysisJob>> ClaimPendingAnalysesAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_analyses
            SET status = 'recovering', updated_at = NOW()
            WHERE id IN (
                SELECT id FROM wa_analyses
                WHERE status NOT IN ('completed', 'error')
                AND updated_at < NOW() - INTERVAL '30 minutes'
                ORDER BY created_at ASC
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, tenant_id, status, source_file_name, config_json::text,
                      total_messages, total_conversations, stage_progress::text,
                      error_message, started_at, completed_at, created_at, updated_at";

        var items = new List<AnalysisJob>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(ReadAnalysisJob(reader));

        return items;
    }

    // ============================================================
    // wa_messages batch insert
    // ============================================================

    public async Task<int> BatchInsertMessagesAsync(int analysisId, int tenantId, List<CleanedMessage> messages, CancellationToken ct = default)
    {
        if (messages.Count == 0) return 0;

        var inserted = 0;
        await using var conn = await _db.OpenConnectionAsync(ct);

        for (var i = 0; i < messages.Count; i += BatchSize)
        {
            var count = Math.Min(BatchSize, messages.Count - i);
            var batch = messages.GetRange(i, count);
            await using var cmd = conn.CreateCommand();

            var sql = new StringBuilder(
                "INSERT INTO wa_messages (analysis_id, tenant_id, conversation_id, business_phone, timestamp, message_text, sender_type, agent_name, message_hash) VALUES ");

            var values = new List<string>();
            for (var j = 0; j < batch.Count; j++)
            {
                values.Add($"(@aid, @tid, @cid{j}, @bp{j}, @ts{j}, @mt{j}, @st{j}, @an{j}, @mh{j})");
                cmd.Parameters.AddWithValue($"cid{j}", batch[j].ConversationId);
                cmd.Parameters.AddWithValue($"bp{j}", batch[j].BusinessPhone);
                cmd.Parameters.AddWithValue($"ts{j}", batch[j].Timestamp);
                cmd.Parameters.AddWithValue($"mt{j}", batch[j].MessageText);
                cmd.Parameters.AddWithValue($"st{j}", batch[j].SenderType);
                cmd.Parameters.AddWithValue($"an{j}", batch[j].AgentName);
                cmd.Parameters.AddWithValue($"mh{j}", batch[j].MessageHash);
            }

            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);

            sql.Append(string.Join(", ", values));
            cmd.CommandText = sql.ToString();

            inserted += await cmd.ExecuteNonQueryAsync(ct);
        }

        return inserted;
    }

    // ============================================================
    // wa_conversations batch insert
    // ============================================================

    public async Task<int> BatchInsertConversationsAsync(int analysisId, int tenantId, List<Conversation> conversations, CancellationToken ct = default)
    {
        if (conversations.Count == 0) return 0;

        var inserted = 0;
        await using var conn = await _db.OpenConnectionAsync(ct);

        for (var i = 0; i < conversations.Count; i += BatchSize)
        {
            var count = Math.Min(BatchSize, conversations.Count - i);
            var batch = conversations.GetRange(i, count);
            await using var cmd = conn.CreateCommand();

            var sql = new StringBuilder(@"
                INSERT INTO wa_conversations (analysis_id, tenant_id, conversation_id, business_phone,
                    start_time, end_time, duration_minutes, message_count, customer_message_count,
                    agent_message_count, primary_agent, first_response_minutes, outcome,
                    product_codes, first_customer_msg, last_agent_msg) VALUES ");

            var values = new List<string>();
            for (var j = 0; j < batch.Count; j++)
            {
                values.Add($"(@aid, @tid, @cid{j}, @bp{j}, @st{j}, @et{j}, @dm{j}, @mc{j}, @cmc{j}, @amc{j}, @pa{j}, @frm{j}, @oc{j}, @pc{j}, @fcm{j}, @lam{j})");
                cmd.Parameters.AddWithValue($"cid{j}", batch[j].ConversationId);
                cmd.Parameters.AddWithValue($"bp{j}", batch[j].BusinessPhone);
                cmd.Parameters.AddWithValue($"st{j}", batch[j].StartTime);
                cmd.Parameters.AddWithValue($"et{j}", batch[j].EndTime);
                cmd.Parameters.AddWithValue($"dm{j}", batch[j].DurationMinutes);
                cmd.Parameters.AddWithValue($"mc{j}", batch[j].MessageCount);
                cmd.Parameters.AddWithValue($"cmc{j}", batch[j].CustomerMessageCount);
                cmd.Parameters.AddWithValue($"amc{j}", batch[j].AgentMessageCount);
                cmd.Parameters.AddWithValue($"pa{j}", batch[j].PrimaryAgent);
                cmd.Parameters.AddWithValue($"frm{j}", (float)batch[j].FirstResponseMinutes);
                cmd.Parameters.AddWithValue($"oc{j}", batch[j].Outcome);
                cmd.Parameters.AddWithValue($"pc{j}", batch[j].ProductCodes);
                cmd.Parameters.AddWithValue($"fcm{j}", batch[j].FirstCustomerMsg);
                cmd.Parameters.AddWithValue($"lam{j}", batch[j].LastAgentMsg);
            }

            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);

            sql.Append(string.Join(", ", values));
            cmd.CommandText = sql.ToString();

            inserted += await cmd.ExecuteNonQueryAsync(ct);
        }

        return inserted;
    }

    // ============================================================
    // wa_metadata
    // ============================================================

    public async Task InsertMetadataAsync(int analysisId, int tenantId, string metadataJson, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO wa_metadata (analysis_id, tenant_id, metadata_json)
            VALUES (@aid, @tid, @mj::jsonb)
            ON CONFLICT (analysis_id) DO UPDATE
            SET metadata_json = @mj::jsonb";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("mj", metadataJson);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<string?> GetMetadataAsync(int tenantId, int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT metadata_json::text FROM wa_metadata
            WHERE analysis_id = @aid AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    // ============================================================
    // Phase B: Reader methods (for NLP stages to consume)
    // ============================================================

    /// <summary>Stage 4: Get all CUSTOMER messages for intent classification.</summary>
    public async Task<List<(string ConversationId, string MessageText)>> GetCustomerMessagesAsync(
        int analysisId, int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT conversation_id, message_text
            FROM wa_messages
            WHERE analysis_id = @aid AND tenant_id = @tid AND sender_type = 'CUSTOMER'
            AND message_text IS NOT NULL AND LENGTH(message_text) > 0
            ORDER BY timestamp";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var results = new List<(string, string)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add((reader.GetString(0), reader.GetString(1)));

        return results;
    }

    /// <summary>Stage 6: Get aggregated customer text per conversation (concatenated).</summary>
    public async Task<List<(string ConversationId, string CustomerText)>> GetCustomerTextPerConversationAsync(
        int analysisId, int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT conversation_id, STRING_AGG(message_text, ' ' ORDER BY timestamp) AS customer_text
            FROM wa_messages
            WHERE analysis_id = @aid AND tenant_id = @tid AND sender_type = 'CUSTOMER'
            AND message_text IS NOT NULL AND LENGTH(message_text) > 0
            GROUP BY conversation_id";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var results = new List<(string, string)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add((reader.GetString(0), reader.IsDBNull(1) ? "" : reader.GetString(1)));

        return results;
    }

    /// <summary>Stage 7: Get conversations with all message text for product analysis.</summary>
    public async Task<List<(string ConversationId, string AllText, string Outcome, string PrimaryAgent)>> GetConversationsWithTextAsync(
        int analysisId, int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT c.conversation_id,
                   COALESCE(m.all_text, '') AS all_text,
                   COALESCE(c.outcome, '') AS outcome,
                   COALESCE(c.primary_agent, '') AS primary_agent
            FROM wa_conversations c
            LEFT JOIN (
                SELECT conversation_id, STRING_AGG(message_text, ' ' ORDER BY timestamp) AS all_text
                FROM wa_messages
                WHERE analysis_id = @aid AND tenant_id = @tid
                AND message_text IS NOT NULL AND LENGTH(message_text) > 0
                GROUP BY conversation_id
            ) m ON m.conversation_id = c.conversation_id
            WHERE c.analysis_id = @aid AND c.tenant_id = @tid";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var results = new List<(string, string, string, string)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));

        return results;
    }

    /// <summary>Stage 5: Get messages grouped by conversation (ordered by timestamp).</summary>
    public async Task<List<(string ConversationId, List<(string SenderType, string Text)> Messages)>> GetMessagesGroupedByConversationAsync(
        int analysisId, int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT conversation_id, sender_type, message_text
            FROM wa_messages
            WHERE analysis_id = @aid AND tenant_id = @tid
            AND message_text IS NOT NULL AND LENGTH(message_text) > 0
            ORDER BY conversation_id, timestamp";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var grouped = new List<(string, List<(string, string)>)>();
        string? currentConvId = null;
        var currentMessages = new List<(string, string)>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var convId = reader.GetString(0);
            var senderType = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var text = reader.GetString(2);

            if (convId != currentConvId)
            {
                if (currentConvId != null && currentMessages.Count > 0)
                    grouped.Add((currentConvId, currentMessages));
                currentConvId = convId;
                currentMessages = new List<(string, string)>();
            }
            currentMessages.Add((senderType, text));
        }

        if (currentConvId != null && currentMessages.Count > 0)
            grouped.Add((currentConvId, currentMessages));

        return grouped;
    }

    // ============================================================
    // Phase B: Batch INSERT for NLP tables
    // ============================================================

    /// <summary>Stage 4: Batch insert intent classifications.</summary>
    public async Task BatchInsertIntentsAsync(int analysisId, int tenantId, List<IntentResult> intents, CancellationToken ct = default)
    {
        if (intents.Count == 0) return;
        await using var conn = await _db.OpenConnectionAsync(ct);

        for (var i = 0; i < intents.Count; i += BatchSize)
        {
            var count = Math.Min(BatchSize, intents.Count - i);
            var batch = intents.GetRange(i, count);
            await using var cmd = conn.CreateCommand();

            var sql = new StringBuilder("INSERT INTO wa_intents (analysis_id, tenant_id, conversation_id, message_text, intent, confidence, method) VALUES ");
            var values = new List<string>();
            for (var j = 0; j < batch.Count; j++)
            {
                values.Add($"(@aid, @tid, @cid{j}, @mt{j}, @int{j}, @conf{j}, @mth{j})");
                cmd.Parameters.AddWithValue($"cid{j}", batch[j].ConversationId);
                cmd.Parameters.AddWithValue($"mt{j}", batch[j].MessageText.Length > 500 ? batch[j].MessageText[..500] : batch[j].MessageText);
                cmd.Parameters.AddWithValue($"int{j}", batch[j].Intent);
                cmd.Parameters.AddWithValue($"conf{j}", batch[j].Confidence);
                cmd.Parameters.AddWithValue($"mth{j}", batch[j].Method);
            }
            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);
            sql.Append(string.Join(", ", values));
            cmd.CommandText = sql.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>Stage 5: Batch insert FAQ pairs.</summary>
    public async Task BatchInsertFaqPairsAsync(int analysisId, int tenantId, List<FaqPair> pairs, CancellationToken ct = default)
    {
        if (pairs.Count == 0) return;
        await using var conn = await _db.OpenConnectionAsync(ct);

        for (var i = 0; i < pairs.Count; i += BatchSize)
        {
            var count = Math.Min(BatchSize, pairs.Count - i);
            var batch = pairs.GetRange(i, count);
            await using var cmd = conn.CreateCommand();

            var sql = new StringBuilder("INSERT INTO wa_faq_pairs (analysis_id, tenant_id, conversation_id, question, answer, question_len, answer_len, cluster_id) VALUES ");
            var values = new List<string>();
            for (var j = 0; j < batch.Count; j++)
            {
                values.Add($"(@aid, @tid, @cid{j}, @q{j}, @a{j}, @ql{j}, @al{j}, @cl{j})");
                cmd.Parameters.AddWithValue($"cid{j}", batch[j].ConversationId);
                cmd.Parameters.AddWithValue($"q{j}", batch[j].Question);
                cmd.Parameters.AddWithValue($"a{j}", batch[j].Answer);
                cmd.Parameters.AddWithValue($"ql{j}", batch[j].QuestionLen);
                cmd.Parameters.AddWithValue($"al{j}", batch[j].AnswerLen);
                cmd.Parameters.AddWithValue($"cl{j}", (object?)batch[j].ClusterId ?? DBNull.Value);
            }
            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);
            sql.Append(string.Join(", ", values));
            cmd.CommandText = sql.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>Stage 5: Batch insert FAQ clusters.</summary>
    public async Task BatchInsertFaqClustersAsync(int analysisId, int tenantId, List<FaqCluster> clusters, CancellationToken ct = default)
    {
        if (clusters.Count == 0) return;
        await using var conn = await _db.OpenConnectionAsync(ct);

        for (var i = 0; i < clusters.Count; i += BatchSize)
        {
            var count = Math.Min(BatchSize, clusters.Count - i);
            var batch = clusters.GetRange(i, count);
            await using var cmd = conn.CreateCommand();

            var sql = new StringBuilder("INSERT INTO wa_faq_clusters (analysis_id, tenant_id, cluster_label, representative_question, question_count, sample_questions, sample_answers) VALUES ");
            var values = new List<string>();
            for (var j = 0; j < batch.Count; j++)
            {
                values.Add($"(@aid, @tid, @cl{j}, @rq{j}, @qc{j}, @sq{j}::jsonb, @sa{j}::jsonb)");
                cmd.Parameters.AddWithValue($"cl{j}", batch[j].ClusterLabel);
                cmd.Parameters.AddWithValue($"rq{j}", batch[j].RepresentativeQuestion);
                cmd.Parameters.AddWithValue($"qc{j}", batch[j].QuestionCount);
                cmd.Parameters.AddWithValue($"sq{j}", JsonSerializer.Serialize(batch[j].SampleQuestions));
                cmd.Parameters.AddWithValue($"sa{j}", JsonSerializer.Serialize(batch[j].SampleAnswers));
            }
            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);
            sql.Append(string.Join(", ", values));
            cmd.CommandText = sql.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>Stage 6: Batch insert sentiment results.</summary>
    public async Task BatchInsertSentimentsAsync(int analysisId, int tenantId, List<SentimentResult> sentiments, CancellationToken ct = default)
    {
        if (sentiments.Count == 0) return;
        await using var conn = await _db.OpenConnectionAsync(ct);

        for (var i = 0; i < sentiments.Count; i += BatchSize)
        {
            var count = Math.Min(BatchSize, sentiments.Count - i);
            var batch = sentiments.GetRange(i, count);
            await using var cmd = conn.CreateCommand();

            var sql = new StringBuilder("INSERT INTO wa_sentiments (analysis_id, tenant_id, conversation_id, sentiment, score, method) VALUES ");
            var values = new List<string>();
            for (var j = 0; j < batch.Count; j++)
            {
                values.Add($"(@aid, @tid, @cid{j}, @s{j}, @sc{j}, @m{j})");
                cmd.Parameters.AddWithValue($"cid{j}", batch[j].ConversationId);
                cmd.Parameters.AddWithValue($"s{j}", batch[j].Sentiment);
                cmd.Parameters.AddWithValue($"sc{j}", batch[j].Score);
                cmd.Parameters.AddWithValue($"m{j}", batch[j].Method);
            }
            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);
            sql.Append(string.Join(", ", values));
            cmd.CommandText = sql.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>Stage 7: Batch insert product analysis results.</summary>
    public async Task BatchInsertProductsAsync(int analysisId, int tenantId, List<ProductResult> products, CancellationToken ct = default)
    {
        if (products.Count == 0) return;
        await using var conn = await _db.OpenConnectionAsync(ct);

        for (var i = 0; i < products.Count; i += BatchSize)
        {
            var count = Math.Min(BatchSize, products.Count - i);
            var batch = products.GetRange(i, count);
            await using var cmd = conn.CreateCommand();

            var sql = new StringBuilder("INSERT INTO wa_products (analysis_id, tenant_id, conversation_id, product_codes, product_count, prices_mentioned, price_count, outcome, primary_agent) VALUES ");
            var values = new List<string>();
            for (var j = 0; j < batch.Count; j++)
            {
                values.Add($"(@aid, @tid, @cid{j}, @pc{j}, @pcnt{j}, @pm{j}, @pmcnt{j}, @oc{j}, @pa{j})");
                cmd.Parameters.AddWithValue($"cid{j}", batch[j].ConversationId);
                cmd.Parameters.AddWithValue($"pc{j}", batch[j].ProductCodes);
                cmd.Parameters.AddWithValue($"pcnt{j}", batch[j].ProductCount);
                cmd.Parameters.AddWithValue($"pm{j}", batch[j].PricesMentioned);
                cmd.Parameters.AddWithValue($"pmcnt{j}", batch[j].PriceCount);
                cmd.Parameters.AddWithValue($"oc{j}", batch[j].Outcome);
                cmd.Parameters.AddWithValue($"pa{j}", batch[j].PrimaryAgent);
            }
            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);
            sql.Append(string.Join(", ", values));
            cmd.CommandText = sql.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>Stage 7: Batch insert unique price entries.</summary>
    public async Task BatchInsertPricesAsync(int analysisId, int tenantId, List<PriceEntry> prices, CancellationToken ct = default)
    {
        if (prices.Count == 0) return;
        await using var conn = await _db.OpenConnectionAsync(ct);

        for (var i = 0; i < prices.Count; i += BatchSize)
        {
            var count = Math.Min(BatchSize, prices.Count - i);
            var batch = prices.GetRange(i, count);
            await using var cmd = conn.CreateCommand();

            var sql = new StringBuilder("INSERT INTO wa_prices (analysis_id, tenant_id, price, mention_count, likely_tl) VALUES ");
            var values = new List<string>();
            for (var j = 0; j < batch.Count; j++)
            {
                values.Add($"(@aid, @tid, @p{j}, @mc{j}, @lt{j})");
                cmd.Parameters.AddWithValue($"p{j}", batch[j].Price);
                cmd.Parameters.AddWithValue($"mc{j}", batch[j].MentionCount);
                cmd.Parameters.AddWithValue($"lt{j}", batch[j].LikelyTl);
            }
            cmd.Parameters.AddWithValue("aid", analysisId);
            cmd.Parameters.AddWithValue("tid", tenantId);
            sql.Append(string.Join(", ", values));
            cmd.CommandText = sql.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    // ============================================================
    // Phase B: Query layer (for API endpoints)
    // ============================================================

    /// <summary>Intent distribution for an analysis.</summary>
    public async Task<string> GetIntentDistributionAsync(int tenantId, int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT intent, COUNT(*) AS cnt, ROUND(AVG(confidence)::numeric, 2) AS avg_conf,
                   COUNT(*) FILTER (WHERE method = 'keyword') AS keyword_cnt,
                   COUNT(*) FILTER (WHERE method IN ('claude','claude_low_conf')) AS claude_cnt
            FROM wa_intents
            WHERE analysis_id = @aid AND tenant_id = @tid
            GROUP BY intent
            ORDER BY cnt DESC";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var items = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new
            {
                intent = reader.GetString(0),
                count = reader.GetInt64(1),
                avgConfidence = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                keywordCount = reader.GetInt64(3),
                claudeCount = reader.GetInt64(4)
            });
        }
        return JsonSerializer.Serialize(new { analysisId, tenantId, intents = items });
    }

    /// <summary>Sentiment summary for an analysis.</summary>
    public async Task<string> GetSentimentSummaryAsync(int tenantId, int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT sentiment, COUNT(*) AS cnt, ROUND(AVG(score)::numeric, 2) AS avg_score,
                   COUNT(*) FILTER (WHERE method = 'keyword') AS keyword_cnt,
                   COUNT(*) FILTER (WHERE method = 'claude') AS claude_cnt,
                   COUNT(*) FILTER (WHERE method = 'empty') AS empty_cnt
            FROM wa_sentiments
            WHERE analysis_id = @aid AND tenant_id = @tid
            GROUP BY sentiment
            ORDER BY cnt DESC";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var items = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new
            {
                sentiment = reader.GetString(0),
                count = reader.GetInt64(1),
                avgScore = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                keywordCount = reader.GetInt64(3),
                claudeCount = reader.GetInt64(4),
                emptyCount = reader.GetInt64(5)
            });
        }
        return JsonSerializer.Serialize(new { analysisId, tenantId, sentiments = items });
    }

    /// <summary>Top products for an analysis (by mention count across conversations).</summary>
    public async Task<string> GetTopProductsAsync(int tenantId, int analysisId, int limit = 50, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT product_codes, SUM(product_count) AS total_products,
                   SUM(price_count) AS total_prices, COUNT(*) AS conversation_count
            FROM wa_products
            WHERE analysis_id = @aid AND tenant_id = @tid AND product_codes <> ''
            GROUP BY product_codes
            ORDER BY total_products DESC
            LIMIT @lim";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lim", limit);

        var items = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new
            {
                productCodes = reader.GetString(0),
                totalProducts = reader.GetInt64(1),
                totalPrices = reader.GetInt64(2),
                conversationCount = reader.GetInt64(3)
            });
        }
        return JsonSerializer.Serialize(new { analysisId, tenantId, products = items });
    }

    /// <summary>Top prices for an analysis.</summary>
    public async Task<string> GetTopPricesAsync(int tenantId, int analysisId, int limit = 30, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT price, mention_count, likely_tl
            FROM wa_prices
            WHERE analysis_id = @aid AND tenant_id = @tid
            ORDER BY mention_count DESC
            LIMIT @lim";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lim", limit);

        var items = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new
            {
                price = reader.GetDecimal(0),
                mentionCount = reader.GetInt32(1),
                likelyTl = reader.IsDBNull(2) ? "" : reader.GetString(2)
            });
        }
        return JsonSerializer.Serialize(new { analysisId, tenantId, prices = items });
    }

    /// <summary>FAQ clusters for an analysis (top N by question count).</summary>
    public async Task<string> GetFaqClustersAsync(int tenantId, int analysisId, int limit = 50, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT cluster_label, representative_question, question_count,
                   sample_questions::text, sample_answers::text
            FROM wa_faq_clusters
            WHERE analysis_id = @aid AND tenant_id = @tid
            ORDER BY question_count DESC
            LIMIT @lim";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lim", limit);

        var items = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new
            {
                clusterLabel = reader.GetInt32(0),
                representativeQuestion = reader.GetString(1),
                questionCount = reader.GetInt32(2),
                sampleQuestions = reader.IsDBNull(3) ? "[]" : reader.GetString(3),
                sampleAnswers = reader.IsDBNull(4) ? "[]" : reader.GetString(4)
            });
        }
        return JsonSerializer.Serialize(new { analysisId, tenantId, clusters = items });
    }

    /// <summary>NLP summary: counts of intents, sentiments, products, FAQ pairs for an analysis.</summary>
    public async Task<string> GetNlpSummaryAsync(int tenantId, int analysisId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                (SELECT COUNT(*) FROM wa_intents WHERE analysis_id = @aid AND tenant_id = @tid) AS intent_count,
                (SELECT COUNT(*) FROM wa_sentiments WHERE analysis_id = @aid AND tenant_id = @tid) AS sentiment_count,
                (SELECT COUNT(*) FROM wa_products WHERE analysis_id = @aid AND tenant_id = @tid) AS product_count,
                (SELECT COUNT(*) FROM wa_faq_pairs WHERE analysis_id = @aid AND tenant_id = @tid) AS faq_pair_count,
                (SELECT COUNT(*) FROM wa_faq_clusters WHERE analysis_id = @aid AND tenant_id = @tid) AS faq_cluster_count,
                (SELECT COUNT(*) FROM wa_prices WHERE analysis_id = @aid AND tenant_id = @tid) AS price_count";
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return JsonSerializer.Serialize(new { analysisId, tenantId, error = "No data" });

        return JsonSerializer.Serialize(new
        {
            analysisId,
            tenantId,
            intentCount = reader.GetInt64(0),
            sentimentCount = reader.GetInt64(1),
            productCount = reader.GetInt64(2),
            faqPairCount = reader.GetInt64(3),
            faqClusterCount = reader.GetInt64(4),
            priceCount = reader.GetInt64(5)
        });
    }

    // ============================================================
    // Helper
    // ============================================================

    private static AnalysisJob ReadAnalysisJob(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        TenantId = reader.GetInt32(1),
        Status = reader.GetString(2),
        SourceFileName = reader.IsDBNull(3) ? "" : reader.GetString(3),
        ConfigJson = reader.IsDBNull(4) ? null : reader.GetString(4),
        TotalMessages = reader.IsDBNull(5) ? null : reader.GetInt32(5),
        TotalConversations = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        StageProgress = reader.IsDBNull(7) ? null : reader.GetString(7),
        ErrorMessage = reader.IsDBNull(8) ? null : reader.GetString(8),
        StartedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
        CompletedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
        CreatedAt = reader.GetDateTime(11),
        UpdatedAt = reader.GetDateTime(12)
    };
}
