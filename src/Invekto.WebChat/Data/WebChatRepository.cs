using Invekto.Shared.Data;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.WebChat.Data;

public class WebChatRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public WebChatRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Visitors ──

    public async Task<bool> UpsertVisitorAsync(
        string visitorId, string? name, string? email,
        string? pageUrl, string? userAgent, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO webchat_visitors (id, name, email, page_url, user_agent)
            VALUES (@id, @name, @email, @url, @ua)
            ON CONFLICT (id) DO UPDATE SET
                name = COALESCE(EXCLUDED.name, webchat_visitors.name),
                email = COALESCE(EXCLUDED.email, webchat_visitors.email),
                last_seen = NOW(),
                page_url = COALESCE(EXCLUDED.page_url, webchat_visitors.page_url),
                user_agent = COALESCE(EXCLUDED.user_agent, webchat_visitors.user_agent)";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", visitorId);
        cmd.Parameters.AddWithValue("name", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("email", (object?)email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("url", (object?)pageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ua", (object?)userAgent ?? DBNull.Value);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> VisitorExistsAsync(string visitorId, CancellationToken ct = default)
    {
        const string sql = "SELECT 1 FROM webchat_visitors WHERE id = @id LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", visitorId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result != null;
    }

    // ── Conversations ──

    public async Task<long> CreateConversationAsync(string visitorId, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO webchat_conversations (visitor_id)
            VALUES (@vid)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("vid", visitorId);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    public async Task<ConversationRow?> GetConversationAsync(long conversationId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT c.id, c.visitor_id, c.status, c.started_at, c.closed_at,
                   c.last_message_at, c.ai_active, v.name as visitor_name, v.email as visitor_email
            FROM webchat_conversations c
            JOIN webchat_visitors v ON v.id = c.visitor_id
            WHERE c.id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", conversationId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new ConversationRow
        {
            Id = reader.GetInt64(0),
            VisitorId = reader.GetString(1),
            Status = reader.GetString(2),
            StartedAt = reader.GetDateTime(3),
            ClosedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
            LastMessageAt = reader.GetDateTime(5),
            AiActive = reader.GetBoolean(6),
            VisitorName = reader.IsDBNull(7) ? null : reader.GetString(7),
            VisitorEmail = reader.IsDBNull(8) ? null : reader.GetString(8)
        };
    }

    public async Task<List<ConversationRow>> GetActiveConversationsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT c.id, c.visitor_id, c.status, c.started_at, c.closed_at,
                   c.last_message_at, c.ai_active, v.name as visitor_name, v.email as visitor_email
            FROM webchat_conversations c
            JOIN webchat_visitors v ON v.id = c.visitor_id
            WHERE c.status IN ('active', 'ai')
            ORDER BY c.last_message_at DESC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<ConversationRow>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ConversationRow
            {
                Id = reader.GetInt64(0),
                VisitorId = reader.GetString(1),
                Status = reader.GetString(2),
                StartedAt = reader.GetDateTime(3),
                ClosedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                LastMessageAt = reader.GetDateTime(5),
                AiActive = reader.GetBoolean(6),
                VisitorName = reader.IsDBNull(7) ? null : reader.GetString(7),
                VisitorEmail = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }
        return list;
    }

    public async Task<bool> CloseConversationAsync(long conversationId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE webchat_conversations
            SET status = 'closed', closed_at = NOW()
            WHERE id = @id AND status != 'closed'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", conversationId);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task UpdateConversationStatusAsync(long conversationId, string status, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE webchat_conversations
            SET status = @status
            WHERE id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", conversationId);
        cmd.Parameters.AddWithValue("status", status);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateLastMessageAtAsync(long conversationId, CancellationToken ct = default)
    {
        const string sql = "UPDATE webchat_conversations SET last_message_at = NOW() WHERE id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", conversationId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Messages ──

    public async Task<long> InsertMessageAsync(
        long conversationId, string senderType, string content, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO webchat_messages (conversation_id, sender_type, content)
            VALUES (@cid, @st, @content)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("cid", conversationId);
        cmd.Parameters.AddWithValue("st", senderType);
        cmd.Parameters.AddWithValue("content", content);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    public async Task<List<MessageRow>> GetMessagesAsync(
        long conversationId, int limit = 100, long? afterId = null, CancellationToken ct = default)
    {
        var sql = afterId.HasValue
            ? @"SELECT id, conversation_id, sender_type, content, created_at
                FROM webchat_messages
                WHERE conversation_id = @cid AND id > @afterId
                ORDER BY created_at ASC LIMIT @lim"
            : @"SELECT id, conversation_id, sender_type, content, created_at
                FROM webchat_messages
                WHERE conversation_id = @cid
                ORDER BY created_at ASC LIMIT @lim";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("cid", conversationId);
        cmd.Parameters.AddWithValue("lim", limit);
        if (afterId.HasValue)
            cmd.Parameters.AddWithValue("afterId", afterId.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<MessageRow>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new MessageRow
            {
                Id = reader.GetInt64(0),
                ConversationId = reader.GetInt64(1),
                SenderType = reader.GetString(2),
                Content = reader.GetString(3),
                CreatedAt = reader.GetDateTime(4)
            });
        }
        return list;
    }

    public async Task<MessageRow?> GetLastMessageAsync(long conversationId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, conversation_id, sender_type, content, created_at
            FROM webchat_messages
            WHERE conversation_id = @cid
            ORDER BY created_at DESC LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("cid", conversationId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new MessageRow
        {
            Id = reader.GetInt64(0),
            ConversationId = reader.GetInt64(1),
            SenderType = reader.GetString(2),
            Content = reader.GetString(3),
            CreatedAt = reader.GetDateTime(4)
        };
    }

    // ── Push Tokens ──

    public async Task UpsertPushTokenAsync(string token, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO webchat_push_tokens (token)
            VALUES (@token)
            ON CONFLICT (token) DO NOTHING";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("token", token);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<string>> GetPushTokensAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT token FROM webchat_push_tokens";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var tokens = new List<string>();
        while (await reader.ReadAsync(ct))
            tokens.Add(reader.GetString(0));
        return tokens;
    }

    public async Task DeletePushTokenAsync(string token, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM webchat_push_tokens WHERE token = @token";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("token", token);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}

// ── Row DTOs ──

public class ConversationRow
{
    public long Id { get; set; }
    public string VisitorId { get; set; } = "";
    public string Status { get; set; } = "active";
    public DateTime StartedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public bool AiActive { get; set; }
    public string? VisitorName { get; set; }
    public string? VisitorEmail { get; set; }
}

public class MessageRow
{
    public long Id { get; set; }
    public long ConversationId { get; set; }
    public string SenderType { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
