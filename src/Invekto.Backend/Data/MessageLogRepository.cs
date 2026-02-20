using Invekto.Shared.Data;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Backend.Data;

/// <summary>
/// PostgreSQL repository for message_log table.
/// Thread-safe, register as singleton. SuperAdmin mesaj izleme.
/// Fire-and-forget insert at webhook, paginated select for ops page.
/// </summary>
public sealed class MessageLogRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public MessageLogRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Fire-and-forget insert. Called from webhook endpoint.
    /// No exception propagation — caller uses ContinueWith for logging.
    /// </summary>
    public async Task InsertAsync(
        int tenantId, string direction, string phone,
        string? senderName, string? messageText, string? messageType,
        string? chatId, string? externalMessageId,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO message_log
                (tenant_id, direction, phone, sender_name, message_text,
                 message_type, chat_id, external_message_id)
            VALUES
                (@tid, @dir, @phone, @sender, @text,
                 @type, @chatId, @extId)";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("dir", direction);
        cmd.Parameters.AddWithValue("phone", phone);
        cmd.Parameters.AddWithValue("sender", (object?)senderName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("text", (object?)messageText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("type", messageType ?? "text");
        cmd.Parameters.AddWithValue("chatId", (object?)chatId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("extId", (object?)externalMessageId ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Paginated message list for superadmin ops page.
    /// All filters optional. Returns (messages, totalCount).
    /// </summary>
    public async Task<(List<MessageLogEntry> Messages, int Total)> GetMessagesAsync(
        int? tenantId, string? phone, string? direction,
        DateTime? from, DateTime? to,
        int limit, int offset,
        CancellationToken ct = default)
    {
        const string countSql = @"
            SELECT COUNT(*)
            FROM message_log
            WHERE (@tid IS NULL OR tenant_id = @tid)
              AND (@phone IS NULL OR phone ILIKE @phone)
              AND (@dir IS NULL OR direction = @dir)
              AND (@from IS NULL OR created_at >= @from)
              AND (@to IS NULL OR created_at <= @to)";

        const string selectSql = @"
            SELECT id, tenant_id, direction, phone, sender_name,
                   message_text, message_type, chat_id, external_message_id, created_at
            FROM message_log
            WHERE (@tid IS NULL OR tenant_id = @tid)
              AND (@phone IS NULL OR phone ILIKE @phone)
              AND (@dir IS NULL OR direction = @dir)
              AND (@from IS NULL OR created_at >= @from)
              AND (@to IS NULL OR created_at <= @to)
            ORDER BY created_at DESC
            LIMIT @limit OFFSET @offset";

        await using var conn = await _db.OpenConnectionAsync(ct);

        // Count
        await using var countCmd = new NpgsqlCommand(countSql, conn);
        AddFilterParams(countCmd, tenantId, phone, direction, from, to);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        // Select
        await using var selectCmd = new NpgsqlCommand(selectSql, conn);
        AddFilterParams(selectCmd, tenantId, phone, direction, from, to);
        selectCmd.Parameters.AddWithValue("limit", Math.Min(limit, 200));
        selectCmd.Parameters.AddWithValue("offset", Math.Max(offset, 0));

        var messages = new List<MessageLogEntry>();
        await using var reader = await selectCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            messages.Add(new MessageLogEntry
            {
                Id = reader.GetInt64(0),
                TenantId = reader.GetInt32(1),
                Direction = reader.GetString(2),
                Phone = reader.GetString(3),
                SenderName = reader.IsDBNull(4) ? null : reader.GetString(4),
                MessageText = reader.IsDBNull(5) ? null : reader.GetString(5),
                MessageType = reader.IsDBNull(6) ? null : reader.GetString(6),
                ChatId = reader.IsDBNull(7) ? null : reader.GetString(7),
                ExternalMessageId = reader.IsDBNull(8) ? null : reader.GetString(8),
                CreatedAt = reader.GetDateTime(9)
            });
        }

        return (messages, total);
    }

    private static void AddFilterParams(
        NpgsqlCommand cmd, int? tenantId, string? phone,
        string? direction, DateTime? from, DateTime? to)
    {
        cmd.Parameters.AddWithValue("tid", tenantId.HasValue ? tenantId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("phone", !string.IsNullOrEmpty(phone) ? $"%{phone}%" : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("dir", (object?)direction ?? DBNull.Value);
        cmd.Parameters.AddWithValue("from", from.HasValue ? from.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("to", to.HasValue ? to.Value : DBNull.Value);
    }
}

/// <summary>
/// Response model for message_log entries.
/// </summary>
public sealed class MessageLogEntry
{
    public long Id { get; init; }
    public int TenantId { get; init; }
    public required string Direction { get; init; }
    public required string Phone { get; init; }
    public string? SenderName { get; init; }
    public string? MessageText { get; init; }
    public string? MessageType { get; init; }
    public string? ChatId { get; init; }
    public string? ExternalMessageId { get; init; }
    public DateTime CreatedAt { get; init; }
}
