using Invekto.Shared.Data;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.AgentAI.Data;

public sealed class AgentAIRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public AgentAIRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string> LogSuggestionAsync(
        Guid suggestionId, int tenantId, int agentId, int chatId,
        string? channel, string language, string messageText,
        int conversationLength, string? suggestedReply,
        string? intent, double? confidence, string? model,
        int processingTimeMs, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO suggest_reply_log
                (suggestion_id, tenant_id, agent_id, chat_id, channel, language,
                 message_text, conversation_length, suggested_reply,
                 intent, confidence, model, processing_time_ms)
            VALUES
                (@sid, @tid, @aid, @cid, @ch, @lang,
                 @msg, @clen, @reply,
                 @intent, @conf, @model, @ptms)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("sid", suggestionId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("aid", agentId);
        cmd.Parameters.AddWithValue("cid", chatId);
        cmd.Parameters.AddWithValue("ch", (object?)channel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("lang", language);
        cmd.Parameters.AddWithValue("msg", messageText);
        cmd.Parameters.AddWithValue("clen", conversationLength);
        cmd.Parameters.AddWithValue("reply", (object?)suggestedReply ?? DBNull.Value);
        cmd.Parameters.AddWithValue("intent", (object?)intent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("conf", confidence.HasValue ? (object)confidence.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("model", (object?)model ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ptms", processingTimeMs);

        var id = await cmd.ExecuteScalarAsync(ct);
        return id?.ToString() ?? "";
    }

    public async Task<bool> UpdateFeedbackAsync(
        Guid suggestionId, int tenantId,
        string agentAction, string? finalReplyText,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE suggest_reply_log
            SET agent_action = @action,
                final_reply_text = @final,
                feedback_received_at = NOW()
            WHERE suggestion_id = @sid AND tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("sid", suggestionId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("action", agentAction);
        cmd.Parameters.AddWithValue("final", (object?)finalReplyText ?? DBNull.Value);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<List<FeedbackRecord>> GetRecentFeedbackAsync(
        int tenantId, int agentId, int limit = 20,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT suggestion_id, intent, confidence, suggested_reply,
                   agent_action, final_reply_text, message_text, created_at
            FROM suggest_reply_log
            WHERE tenant_id = @tid AND agent_id = @aid AND agent_action != 'pending'
            ORDER BY created_at DESC
            LIMIT @lim";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("aid", agentId);
        cmd.Parameters.AddWithValue("lim", limit);

        var records = new List<FeedbackRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            records.Add(new FeedbackRecord
            {
                SuggestionId = reader.GetGuid(0).ToString(),
                Intent = reader.IsDBNull(1) ? null : reader.GetString(1),
                Confidence = reader.IsDBNull(2) ? null : (double)reader.GetDecimal(2),
                SuggestedReply = reader.IsDBNull(3) ? null : reader.GetString(3),
                AgentAction = reader.GetString(4),
                FinalReplyText = reader.IsDBNull(5) ? null : reader.GetString(5),
                MessageText = reader.GetString(6),
                CreatedAt = reader.GetDateTime(7)
            });
        }

        return records;
    }
}

public sealed class FeedbackRecord
{
    public string SuggestionId { get; set; } = "";
    public string? Intent { get; set; }
    public double? Confidence { get; set; }
    public string? SuggestedReply { get; set; }
    public string AgentAction { get; set; } = "";
    public string? FinalReplyText { get; set; }
    public string MessageText { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
