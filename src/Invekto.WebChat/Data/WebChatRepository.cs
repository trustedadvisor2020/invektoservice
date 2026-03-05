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

    // ── Widget Configs ──

    public async Task<WidgetFlowConfig?> GetWidgetConfigAsync(string widgetId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT widget_id, tenant_id, flow_conversation_created,
                   flow_visitor_message, flow_conversation_closed
            FROM webchat_widget_configs
            WHERE widget_id = @wid AND is_active = TRUE";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("wid", widgetId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new WidgetFlowConfig
        {
            WidgetId = reader.GetString(0),
            TenantId = reader.GetInt32(1),
            FlowConversationCreated = reader.GetInt32(2),
            FlowVisitorMessage = reader.GetInt32(3),
            FlowConversationClosed = reader.GetInt32(4)
        };
    }

    // ── Conversations ──

    public async Task<long> CreateConversationAsync(string visitorId, string? widgetId = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO webchat_conversations (visitor_id, widget_id)
            VALUES (@vid, @wid)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("vid", visitorId);
        cmd.Parameters.AddWithValue("wid", (object?)widgetId ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    public async Task<ConversationRow?> GetConversationAsync(long conversationId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT c.id, c.visitor_id, c.status, c.started_at, c.closed_at,
                   c.last_message_at, c.ai_active, v.name as visitor_name, v.email as visitor_email,
                   c.widget_id
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
            VisitorEmail = reader.IsDBNull(8) ? null : reader.GetString(8),
            WidgetId = reader.IsDBNull(9) ? null : reader.GetString(9)
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

    public async Task<List<ConversationRow>> GetAllConversationsAsync(int closedLimit = 50, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT c.id, c.visitor_id, c.status, c.started_at, c.closed_at,
                   c.last_message_at, c.ai_active, v.name as visitor_name, v.email as visitor_email
            FROM webchat_conversations c
            JOIN webchat_visitors v ON v.id = c.visitor_id
            ORDER BY
                CASE WHEN c.status IN ('active', 'ai') THEN 0 ELSE 1 END,
                c.last_message_at DESC
            LIMIT @lim";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("lim", closedLimit);
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

    /// <summary>
    /// Closes a conversation. Returns (closed, widget_id) so the caller can trigger webhooks.
    /// </summary>
    public async Task<(bool Closed, string? WidgetId)> CloseConversationAsync(long conversationId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE webchat_conversations
            SET status = 'closed', closed_at = NOW()
            WHERE id = @id AND status != 'closed'
            RETURNING widget_id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", conversationId);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result == null) return (false, null);

        var widgetId = result == DBNull.Value ? null : (string?)result;
        return (true, widgetId);
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

    // ── Visitors (lookup) ──

    public async Task<VisitorRow?> GetVisitorByIdAsync(string visitorId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, name, email, first_seen, last_seen, page_url, user_agent
            FROM webchat_visitors WHERE id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", visitorId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new VisitorRow
        {
            Id = reader.GetString(0),
            Name = reader.IsDBNull(1) ? null : reader.GetString(1),
            Email = reader.IsDBNull(2) ? null : reader.GetString(2),
            FirstSeen = reader.GetDateTime(3),
            LastSeen = reader.GetDateTime(4),
            PageUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
            UserAgent = reader.IsDBNull(6) ? null : reader.GetString(6)
        };
    }

    // ── Messages ──

    public async Task<long> InsertMessageAsync(
        long conversationId, string senderType, string content, CancellationToken ct = default)
    {
        const string sql = @"
            WITH ins AS (
                INSERT INTO webchat_messages (conversation_id, sender_type, content)
                VALUES (@cid, @st, @content)
                RETURNING id
            )
            SELECT id FROM ins";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("cid", conversationId);
        cmd.Parameters.AddWithValue("st", senderType);
        cmd.Parameters.AddWithValue("content", content);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    /// <summary>
    /// Inserts a message and updates conversation last_message_at in a single round-trip.
    /// </summary>
    public async Task<long> InsertMessageAndTouchAsync(
        long conversationId, string senderType, string content, CancellationToken ct = default)
    {
        const string sql = @"
            WITH ins AS (
                INSERT INTO webchat_messages (conversation_id, sender_type, content)
                VALUES (@cid, @st, @content)
                RETURNING id
            )
            , upd AS (
                UPDATE webchat_conversations SET last_message_at = NOW() WHERE id = @cid
            )
            SELECT id FROM ins";

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
    public string? WidgetId { get; set; }
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

public class VisitorRow
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? Email { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public string? PageUrl { get; set; }
    public string? UserAgent { get; set; }
}

public class WidgetFlowConfig
{
    public string WidgetId { get; set; } = "";
    public int TenantId { get; set; }
    public int FlowConversationCreated { get; set; }
    public int FlowVisitorMessage { get; set; }
    public int FlowConversationClosed { get; set; }
}
