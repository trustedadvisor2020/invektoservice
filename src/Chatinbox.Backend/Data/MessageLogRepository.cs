using Chatinbox.Shared.Data;
using Chatinbox.Shared.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Chatinbox.Backend.Data;

/// <summary>
/// PostgreSQL repository for message_log table.
/// Thread-safe, register as singleton. SuperAdmin mesaj izleme.
/// Fire-and-forget insert at webhook, paginated select for ops page.
/// </summary>
public class MessageLogRepository
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
    public virtual async Task InsertAsync(
        int tenantId, string direction, string phone,
        string? senderName, string? messageText, string? messageType,
        string? chatId, string? externalMessageId, string? instanceId,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO message_log
                (tenant_id, direction, phone, sender_name, message_text,
                 message_type, chat_id, external_message_id, instance_id)
            VALUES
                (@tid, @dir, @phone, @sender, @text,
                 @type, @chatId, @extId, @instId)";

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
        cmd.Parameters.AddWithValue("instId", (object?)instanceId ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Get the last incoming message's instance_id for a tenant+phone pair.
    /// Used by WapCRM callback bridge to reply on the same WhatsApp instance.
    /// </summary>
    public virtual async Task<string?> GetLastInstanceIdAsync(int tenantId, string phone, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT instance_id FROM message_log
            WHERE tenant_id = @tid AND phone = @phone AND direction = 'in'
              AND instance_id IS NOT NULL
            ORDER BY created_at DESC LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", phone);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    /// <summary>
    /// Paginated message list for superadmin ops page.
    /// All filters optional. Returns (messages, totalCount).
    /// LEFT JOINs tenant_instances to resolve instance_name (channel).
    /// </summary>
    public virtual async Task<(List<MessageLogEntry> Messages, int Total)> GetMessagesAsync(
        int? tenantId, string? phone, string? direction,
        DateTime? from, DateTime? to,
        string? instanceId,
        int limit, int offset,
        CancellationToken ct = default)
    {
        const string whereClause = @"
            WHERE (@tid IS NULL OR ml.tenant_id = @tid)
              AND (@phone IS NULL OR ml.phone ILIKE @phone)
              AND (@dir IS NULL OR ml.direction = @dir)
              AND (@from IS NULL OR ml.created_at >= @from)
              AND (@to IS NULL OR ml.created_at <= @to)
              AND (@iid IS NULL OR ml.instance_id = @iid)";

        var countSql = $"SELECT COUNT(*) FROM message_log ml {whereClause}";

        var selectSql = $@"
            SELECT ml.id, ml.tenant_id, ml.direction, ml.phone, ml.sender_name,
                   ml.message_text, ml.message_type, ml.chat_id, ml.external_message_id,
                   ml.instance_id, ml.created_at, ti.instance_name
            FROM message_log ml
            LEFT JOIN tenant_instances ti ON ti.tenant_id = ml.tenant_id AND ti.instance_id = ml.instance_id
            {whereClause}
            ORDER BY ml.created_at DESC
            LIMIT @limit OFFSET @offset";

        await using var conn = await _db.OpenConnectionAsync(ct);

        // Count
        await using var countCmd = new NpgsqlCommand(countSql, conn);
        AddFilterParams(countCmd, tenantId, phone, direction, from, to, instanceId);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        // Select
        await using var selectCmd = new NpgsqlCommand(selectSql, conn);
        AddFilterParams(selectCmd, tenantId, phone, direction, from, to, instanceId);
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
                InstanceId = reader.IsDBNull(9) ? null : reader.GetString(9),
                CreatedAt = reader.GetDateTime(10),
                InstanceName = reader.IsDBNull(11) ? null : reader.GetString(11)
            });
        }

        return (messages, total);
    }

    /// <summary>
    /// Returns distinct channels (instance_id + instance_name) for the ops filter dropdown.
    /// Optionally filtered by tenant.
    /// </summary>
    public virtual async Task<List<ChannelEntry>> GetDistinctChannelsAsync(int? tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT DISTINCT ti.instance_id, ti.instance_name
            FROM tenant_instances ti
            INNER JOIN message_log ml ON ml.tenant_id = ti.tenant_id AND ml.instance_id = ti.instance_id
            WHERE (@tid IS NULL OR ti.tenant_id = @tid)
            ORDER BY ti.instance_name";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter("tid", NpgsqlDbType.Integer) { Value = tenantId.HasValue ? tenantId.Value : DBNull.Value });

        var channels = new List<ChannelEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            channels.Add(new ChannelEntry
            {
                InstanceId = reader.GetString(0),
                InstanceName = reader.IsDBNull(1) ? reader.GetString(0) : reader.GetString(1)
            });
        }
        return channels;
    }

    /// <summary>
    /// Get the full "story" of an incoming message: what happened after it arrived.
    /// Joins message_log + auto_reply_log + chatbot_flows to build a timeline.
    /// </summary>
    public virtual async Task<MessageStory?> GetMessageStoryAsync(long messageId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        // 1. Get the original message
        const string msgSql = @"
            SELECT id, tenant_id, direction, phone, sender_name,
                   message_text, message_type, chat_id, instance_id, created_at
            FROM message_log WHERE id = @id";

        await using var msgCmd = new NpgsqlCommand(msgSql, conn);
        msgCmd.Parameters.AddWithValue("id", messageId);

        MessageStory? story = null;
        await using (var r = await msgCmd.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct))
                return null;

            story = new MessageStory
            {
                MessageId = r.GetInt64(0),
                TenantId = r.GetInt32(1),
                Direction = r.GetString(2),
                Phone = r.GetString(3),
                SenderName = r.IsDBNull(4) ? null : r.GetString(4),
                MessageText = r.IsDBNull(5) ? null : r.GetString(5),
                MessageType = r.IsDBNull(6) ? null : r.GetString(6),
                ChatId = r.IsDBNull(7) ? null : r.GetString(7),
                InstanceId = r.IsDBNull(8) ? null : r.GetString(8),
                CreatedAt = r.GetDateTime(9)
            };
        }

        // 2. Get auto_reply_log entries within ±60 seconds
        const string replySql = @"
            SELECT intent, confidence, reply_type, reply_text, processing_time_ms, created_at
            FROM auto_reply_log
            WHERE tenant_id = @tid AND phone = @phone
              AND created_at BETWEEN @from AND @to
            ORDER BY created_at ASC";

        await using var replyCmd = new NpgsqlCommand(replySql, conn);
        replyCmd.Parameters.AddWithValue("tid", story.TenantId);
        replyCmd.Parameters.AddWithValue("phone", story.Phone);
        replyCmd.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.TimestampTz) { Value = story.CreatedAt.AddSeconds(-5) });
        replyCmd.Parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.TimestampTz) { Value = story.CreatedAt.AddSeconds(60) });

        await using (var r2 = await replyCmd.ExecuteReaderAsync(ct))
        {
            while (await r2.ReadAsync(ct))
            {
                story.AutoReplies.Add(new AutoReplyEntry
                {
                    Intent = r2.GetString(0),
                    Confidence = r2.GetDecimal(1),
                    ReplyType = r2.IsDBNull(2) ? null : r2.GetString(2),
                    ReplyText = r2.GetString(3),
                    ProcessingTimeMs = r2.GetInt32(4),
                    CreatedAt = r2.GetDateTime(5)
                });
            }
        }

        // 3. Get outgoing messages (bot replies) within 60 seconds
        const string outSql = @"
            SELECT id, message_text, sender_name, created_at
            FROM message_log
            WHERE tenant_id = @tid AND phone = @phone AND direction = 'out'
              AND created_at BETWEEN @from AND @to
            ORDER BY created_at ASC";

        await using var outCmd = new NpgsqlCommand(outSql, conn);
        outCmd.Parameters.AddWithValue("tid", story.TenantId);
        outCmd.Parameters.AddWithValue("phone", story.Phone);
        outCmd.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.TimestampTz) { Value = story.CreatedAt.AddSeconds(-5) });
        outCmd.Parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.TimestampTz) { Value = story.CreatedAt.AddSeconds(60) });

        await using (var r3 = await outCmd.ExecuteReaderAsync(ct))
        {
            while (await r3.ReadAsync(ct))
            {
                story.OutgoingMessages.Add(new OutgoingMessageEntry
                {
                    Id = r3.GetInt64(0),
                    MessageText = r3.IsDBNull(1) ? null : r3.GetString(1),
                    SenderName = r3.IsDBNull(2) ? null : r3.GetString(2),
                    CreatedAt = r3.GetDateTime(3)
                });
            }
        }

        // 4. Get flow name — instance-aware routing (mirrors Automation's GetFlowByInstanceAsync)
        bool flowResolved = false;

        if (!string.IsNullOrEmpty(story.InstanceId))
        {
            const string instFlowSql = @"
                SELECT cf.flow_id, cf.flow_name
                FROM tenant_instances ti
                JOIN chatbot_flows cf ON cf.flow_id = ti.flow_id AND cf.tenant_id = ti.tenant_id
                WHERE ti.tenant_id = @tid AND ti.instance_id = @iid
                  AND ti.is_enabled = true AND cf.is_active = true
                LIMIT 1";

            await using var instFlowCmd = new NpgsqlCommand(instFlowSql, conn);
            instFlowCmd.Parameters.AddWithValue("tid", story.TenantId);
            instFlowCmd.Parameters.AddWithValue("iid", story.InstanceId);

            await using (var r4 = await instFlowCmd.ExecuteReaderAsync(ct))
            {
                if (await r4.ReadAsync(ct))
                {
                    story.FlowId = r4.GetInt32(0);
                    story.FlowName = r4.IsDBNull(1) ? null : r4.GetString(1);
                    flowResolved = true;
                }
            }

            // If instance exists in tenant_instances but has no flow → don't show "Flow Tetiklendi"
            // If instance NOT in tenant_instances → fall through to legacy lookup below
            if (!flowResolved)
            {
                const string hasInstSql = "SELECT EXISTS(SELECT 1 FROM tenant_instances WHERE tenant_id = @tid)";
                await using var hasInstCmd = new NpgsqlCommand(hasInstSql, conn);
                hasInstCmd.Parameters.AddWithValue("tid", story.TenantId);
                var hasInstances = (bool)(await hasInstCmd.ExecuteScalarAsync(ct))!;
                if (hasInstances)
                    flowResolved = true; // tenant uses instance routing; this instance has no flow → skip
            }
        }

        if (!flowResolved)
        {
            // Legacy: no tenant_instances records or no instance_id → first active flow
            const string flowSql = @"
                SELECT flow_id, flow_name FROM chatbot_flows
                WHERE tenant_id = @tid AND is_active = true
                LIMIT 1";

            await using var flowCmd = new NpgsqlCommand(flowSql, conn);
            flowCmd.Parameters.AddWithValue("tid", story.TenantId);

            await using (var r4 = await flowCmd.ExecuteReaderAsync(ct))
            {
                if (await r4.ReadAsync(ct))
                {
                    story.FlowId = r4.GetInt32(0);
                    story.FlowName = r4.IsDBNull(1) ? null : r4.GetString(1);
                }
            }
        }

        return story;
    }

    private static void AddFilterParams(
        NpgsqlCommand cmd, int? tenantId, string? phone,
        string? direction, DateTime? from, DateTime? to,
        string? instanceId = null)
    {
        cmd.Parameters.Add(new NpgsqlParameter("tid", NpgsqlDbType.Integer) { Value = tenantId.HasValue ? tenantId.Value : DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("phone", NpgsqlDbType.Text) { Value = !string.IsNullOrEmpty(phone) ? $"%{phone}%" : DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("dir", NpgsqlDbType.Varchar) { Value = (object?)direction ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.TimestampTz) { Value = from.HasValue ? from.Value : DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.TimestampTz) { Value = to.HasValue ? to.Value : DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("iid", NpgsqlDbType.Varchar) { Value = (object?)instanceId ?? DBNull.Value });
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
    public string? InstanceId { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? InstanceName { get; init; }
}

public sealed class ChannelEntry
{
    public required string InstanceId { get; init; }
    public required string InstanceName { get; init; }
}

/// <summary>
/// Full story of an incoming message: original + auto-replies + outgoing + flow info.
/// </summary>
public sealed class MessageStory
{
    public long MessageId { get; init; }
    public int TenantId { get; init; }
    public required string Direction { get; init; }
    public required string Phone { get; init; }
    public string? SenderName { get; init; }
    public string? MessageText { get; init; }
    public string? MessageType { get; init; }
    public string? ChatId { get; init; }
    public string? InstanceId { get; init; }
    public DateTime CreatedAt { get; init; }

    public int? FlowId { get; set; }
    public string? FlowName { get; set; }

    public List<AutoReplyEntry> AutoReplies { get; } = new();
    public List<OutgoingMessageEntry> OutgoingMessages { get; } = new();
}

public sealed class AutoReplyEntry
{
    public required string Intent { get; init; }
    public decimal Confidence { get; init; }
    public string? ReplyType { get; init; }
    public required string ReplyText { get; init; }
    public int ProcessingTimeMs { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class OutgoingMessageEntry
{
    public long Id { get; init; }
    public string? MessageText { get; init; }
    public string? SenderName { get; init; }
    public DateTime CreatedAt { get; init; }
}
