using System.Text.Json;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Outbound.Data;

public sealed class OutboundRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public OutboundRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ================================================================
    // Templates
    // ================================================================

    public async Task<List<TemplateDto>> GetActiveTemplatesAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, name, trigger_event, message_template, variables_json,
                   is_active, created_at, updated_at
            FROM outbound_templates
            WHERE tenant_id = @tid AND is_active = TRUE
            ORDER BY created_at DESC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var templates = new List<TemplateDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            templates.Add(ReadTemplateDto(reader));
        }
        return templates;
    }

    public async Task<TemplateDto?> GetTemplateByIdAsync(
        int tenantId, int templateId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, name, trigger_event, message_template, variables_json,
                   is_active, created_at, updated_at
            FROM outbound_templates
            WHERE tenant_id = @tid AND id = @id AND is_active = TRUE";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", templateId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadTemplateDto(reader);
        return null;
    }

    public async Task<TemplateDto?> GetTriggerTemplateAsync(
        int tenantId, string triggerEvent, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, name, trigger_event, message_template, variables_json,
                   is_active, created_at, updated_at
            FROM outbound_templates
            WHERE tenant_id = @tid AND trigger_event = @evt AND is_active = TRUE
            ORDER BY updated_at DESC
            LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("evt", triggerEvent);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadTemplateDto(reader);
        return null;
    }

    public async Task<int> CreateTemplateAsync(
        int tenantId, string name, string triggerEvent,
        string messageTemplate, Dictionary<string, string>? variablesJson,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO outbound_templates
                (tenant_id, name, trigger_event, message_template, variables_json)
            VALUES (@tid, @name, @evt, @tpl, @vars::jsonb)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("evt", triggerEvent);
        cmd.Parameters.AddWithValue("tpl", messageTemplate);
        cmd.Parameters.AddWithValue("vars",
            variablesJson != null ? (object)JsonSerializer.Serialize(variablesJson) : DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(id);
    }

    public async Task<bool> UpdateTemplateAsync(
        int tenantId, int templateId, TemplateUpdateRequest req,
        CancellationToken ct = default)
    {
        var setClauses = new List<string>();
        var parameters = new List<NpgsqlParameter>
        {
            new("tid", tenantId),
            new("id", templateId)
        };

        if (req.Name != null)
        {
            setClauses.Add("name = @name");
            parameters.Add(new NpgsqlParameter("name", req.Name));
        }
        if (req.TriggerEvent != null)
        {
            setClauses.Add("trigger_event = @evt");
            parameters.Add(new NpgsqlParameter("evt", req.TriggerEvent));
        }
        if (req.MessageTemplate != null)
        {
            setClauses.Add("message_template = @tpl");
            parameters.Add(new NpgsqlParameter("tpl", req.MessageTemplate));
        }
        if (req.VariablesJson != null)
        {
            setClauses.Add("variables_json = @vars::jsonb");
            parameters.Add(new NpgsqlParameter("vars", JsonSerializer.Serialize(req.VariablesJson)));
        }

        if (setClauses.Count == 0) return false;
        setClauses.Add("updated_at = NOW()");

        var sql = $"UPDATE outbound_templates SET {string.Join(", ", setClauses)} WHERE tenant_id = @tid AND id = @id AND is_active = TRUE";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters.ToArray());

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> DeactivateTemplateAsync(
        int tenantId, int templateId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE outbound_templates
            SET is_active = FALSE, updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id AND is_active = TRUE";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", templateId);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    // ================================================================
    // Broadcasts
    // ================================================================

    public async Task<Guid> CreateBroadcastAsync(
        int tenantId, int templateId, int totalRecipients, int queued,
        DateTime? scheduledAt, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO outbound_broadcasts
                (tenant_id, template_id, total_recipients, queued, status, scheduled_at)
            VALUES (@tid, @tmpl, @total, @queued, 'queued', @sched)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("tmpl", templateId);
        cmd.Parameters.AddWithValue("total", totalRecipients);
        cmd.Parameters.AddWithValue("queued", queued);
        cmd.Parameters.AddWithValue("sched", scheduledAt.HasValue ? (object)scheduledAt.Value : DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(ct);
        return (Guid)id!;
    }

    public async Task<BroadcastStatusResponse?> GetBroadcastStatusAsync(
        int tenantId, Guid broadcastId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, status, total_recipients, queued, sent, delivered, read, failed,
                   created_at, started_at, completed_at
            FROM outbound_broadcasts
            WHERE tenant_id = @tid AND id = @bid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("bid", broadcastId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new BroadcastStatusResponse
        {
            BroadcastId = reader.GetGuid(0),
            Status = reader.GetString(1),
            TotalRecipients = reader.GetInt32(2),
            Queued = reader.GetInt32(3),
            Sent = reader.GetInt32(4),
            Delivered = reader.GetInt32(5),
            Read = reader.GetInt32(6),
            Failed = reader.GetInt32(7),
            CreatedAt = reader.GetDateTime(8),
            StartedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            CompletedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
        };
    }

    public async Task UpdateBroadcastStatusAsync(
        Guid broadcastId, string status, CancellationToken ct = default)
    {
        var extraSet = status switch
        {
            "processing" => ", started_at = COALESCE(started_at, NOW())",
            "completed" or "failed" => ", completed_at = NOW()",
            _ => ""
        };

        var sql = $"UPDATE outbound_broadcasts SET status = @st{extraSet} WHERE id = @bid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("st", status);
        cmd.Parameters.AddWithValue("bid", broadcastId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task IncrementBroadcastCounterAsync(
        Guid broadcastId, string counterColumn, CancellationToken ct = default)
    {
        // Whitelist valid columns to prevent SQL injection
        if (counterColumn is not ("sent" or "delivered" or "read" or "failed"))
            throw new ArgumentException($"Invalid counter column: {counterColumn}");

        // Decrement queued when message leaves queue
        var queuedDecrement = counterColumn == "sent" || counterColumn == "failed"
            ? ", queued = GREATEST(queued - 1, 0)" : "";

        var sql = $"UPDATE outbound_broadcasts SET {counterColumn} = {counterColumn} + 1{queuedDecrement} WHERE id = @bid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("bid", broadcastId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Check if all messages in a broadcast are processed (no more queued/sending).</summary>
    public async Task<bool> IsBroadcastCompleteAsync(
        Guid broadcastId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*) FROM outbound_messages
            WHERE broadcast_id = @bid AND status IN ('queued', 'sending')";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("bid", broadcastId);

        var count = (long)(await cmd.ExecuteScalarAsync(ct))!;
        return count == 0;
    }

    // ================================================================
    // Messages
    // ================================================================

    public async Task<long> InsertMessageAsync(
        int tenantId, Guid? broadcastId, int? templateId,
        string recipientPhone, string messageText,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO outbound_messages
                (tenant_id, broadcast_id, template_id, recipient_phone, message_text, status)
            VALUES (@tid, @bid, @tmpl, @phone, @msg, 'queued')
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("bid", broadcastId.HasValue ? (object)broadcastId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("tmpl", templateId.HasValue ? (object)templateId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("phone", recipientPhone);
        cmd.Parameters.AddWithValue("msg", messageText);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    /// <summary>Dequeue next batch of messages to send, respecting rate limit.</summary>
    public async Task<List<QueuedMessage>> DequeueMessagesAsync(
        int batchSize, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE outbound_messages
            SET status = 'sending'
            WHERE id IN (
                SELECT id FROM outbound_messages
                WHERE status = 'queued'
                ORDER BY created_at
                LIMIT @batch
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, tenant_id, broadcast_id, template_id,
                      recipient_phone, message_text";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("batch", batchSize);

        var messages = new List<QueuedMessage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            messages.Add(new QueuedMessage
            {
                Id = reader.GetInt64(0),
                TenantId = reader.GetInt32(1),
                BroadcastId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                TemplateId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                RecipientPhone = reader.GetString(4),
                MessageText = reader.GetString(5)
            });
        }
        return messages;
    }

    public async Task UpdateMessageStatusAsync(
        long messageId, string status, string? externalMessageId = null,
        string? failedReason = null, CancellationToken ct = default)
    {
        var timestampCol = status switch
        {
            "sent" => ", sent_at = NOW()",
            "delivered" => ", delivered_at = NOW()",
            "read" => ", read_at = NOW()",
            _ => ""
        };

        var extIdSet = externalMessageId != null ? ", external_message_id = @eid" : "";
        var failSet = failedReason != null ? ", failed_reason = @fail" : "";

        var sql = $"UPDATE outbound_messages SET status = @st{timestampCol}{extIdSet}{failSet} WHERE id = @mid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("st", status);
        cmd.Parameters.AddWithValue("mid", messageId);

        if (externalMessageId != null)
            cmd.Parameters.AddWithValue("eid", externalMessageId);
        if (failedReason != null)
            cmd.Parameters.AddWithValue("fail", failedReason);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Find message by external_message_id (from WapCRM/WhatsApp).</summary>
    public async Task<(long messageId, Guid? broadcastId, int tenantId)?> FindMessageByExternalIdAsync(
        string externalMessageId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, broadcast_id, tenant_id
            FROM outbound_messages
            WHERE external_message_id = @eid
            LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("eid", externalMessageId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return (
                reader.GetInt64(0),
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.GetInt32(2)
            );
        }
        return null;
    }

    // ================================================================
    // Opt-outs
    // ================================================================

    public async Task<bool> IsOptedOutAsync(
        int tenantId, string phone, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT 1 FROM outbound_optouts
            WHERE tenant_id = @tid AND phone = @phone
            LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", phone);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result != null;
    }

    public async Task<DateTime?> GetOptOutDateAsync(
        int tenantId, string phone, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT created_at FROM outbound_optouts
            WHERE tenant_id = @tid AND phone = @phone
            LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", phone);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result as DateTime?;
    }

    public async Task<bool> AddOptOutAsync(
        int tenantId, string phone, string? reason,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO outbound_optouts (tenant_id, phone, reason)
            VALUES (@tid, @phone, @reason)
            ON CONFLICT (tenant_id, phone) DO NOTHING
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", phone);
        cmd.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(ct);
        return id != null;
    }

    public async Task<bool> RemoveOptOutAsync(
        int tenantId, string phone, CancellationToken ct = default)
    {
        const string sql = @"
            DELETE FROM outbound_optouts
            WHERE tenant_id = @tid AND phone = @phone";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", phone);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    // ================================================================
    // Helpers
    // ================================================================

    /// <summary>Batch check which phones are opted out for a tenant.</summary>
    public async Task<HashSet<string>> BatchCheckOptOutsAsync(
        int tenantId, List<string> phones, CancellationToken ct = default)
    {
        if (phones.Count == 0) return new HashSet<string>();

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT phone FROM outbound_optouts WHERE tenant_id = @tid AND phone = ANY(@phones)", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phones", phones.ToArray());

        var optedOut = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            optedOut.Add(reader.GetString(0));
        return optedOut;
    }

    /// <summary>Batch insert messages in a single multi-row INSERT.</summary>
    public async Task BatchInsertMessagesAsync(
        int tenantId, Guid broadcastId, int templateId,
        List<(string phone, string text)> messages,
        CancellationToken ct = default)
    {
        if (messages.Count == 0) return;

        await using var conn = await _db.OpenConnectionAsync(ct);

        // Build multi-row VALUES clause
        var valueClauses = new List<string>();
        await using var cmd = new NpgsqlCommand();
        cmd.Connection = conn;

        for (var i = 0; i < messages.Count; i++)
        {
            valueClauses.Add($"(@tid, @bid, @tmpl, @phone{i}, @msg{i}, 'queued')");
            cmd.Parameters.AddWithValue($"phone{i}", messages[i].phone);
            cmd.Parameters.AddWithValue($"msg{i}", messages[i].text);
        }

        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("bid", broadcastId);
        cmd.Parameters.AddWithValue("tmpl", templateId);

        cmd.CommandText = $@"
            INSERT INTO outbound_messages
                (tenant_id, broadcast_id, template_id, recipient_phone, message_text, status)
            VALUES {string.Join(",\n                   ", valueClauses)}";

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Reset stale 'sending' messages back to 'queued' on service shutdown.</summary>
    public async Task ResetSendingMessagesAsync(CancellationToken ct = default)
    {
        const string sql = "UPDATE outbound_messages SET status = 'queued' WHERE status = 'sending'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows > 0)
            _logger.SystemWarn($"Reset {rows} stale 'sending' messages back to 'queued' on shutdown");
    }

    private TemplateDto ReadTemplateDto(NpgsqlDataReader reader)
    {
        var variablesStr = reader.IsDBNull(4) ? null : reader.GetString(4);
        Dictionary<string, string>? variables = null;
        if (variablesStr != null)
        {
            try { variables = JsonSerializer.Deserialize<Dictionary<string, string>>(variablesStr); }
            catch (JsonException ex)
            {
                var templateId = reader.GetInt32(0);
                _logger.SystemWarn($"Malformed variables_json for template {templateId}: {ex.Message}");
            }
        }

        return new TemplateDto
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            TriggerEvent = reader.GetString(2),
            MessageTemplate = reader.GetString(3),
            VariablesJson = variables,
            IsActive = reader.GetBoolean(5),
            CreatedAt = reader.GetDateTime(6),
            UpdatedAt = reader.GetDateTime(7)
        };
    }
}

public sealed class QueuedMessage
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public Guid? BroadcastId { get; set; }
    public int? TemplateId { get; set; }
    public string RecipientPhone { get; set; } = "";
    public string MessageText { get; set; } = "";
}
