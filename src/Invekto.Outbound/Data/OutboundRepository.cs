using System.Text.Json;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Outbound.Data;

public class OutboundRepository
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

    /// <summary>
    /// GR-2.3: Get active templates, optionally filtered by language.
    /// </summary>
    public virtual async Task<List<TemplateDto>> GetActiveTemplatesAsync(
        int tenantId, string? lang = null, CancellationToken ct = default)
    {
        var sql = @"
            SELECT id, name, trigger_event, message_template, variables_json,
                   is_active, created_at, updated_at, lang
            FROM outbound_templates
            WHERE tenant_id = @tid AND is_active = TRUE"
            + (lang != null ? " AND lang = @lang" : "")
            + " ORDER BY created_at DESC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        if (lang != null)
            cmd.Parameters.AddWithValue("lang", lang);

        var templates = new List<TemplateDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            templates.Add(ReadTemplateDto(reader));
        }
        return templates;
    }

    public virtual async Task<TemplateDto?> GetTemplateByIdAsync(
        int tenantId, int templateId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, name, trigger_event, message_template, variables_json,
                   is_active, created_at, updated_at, lang
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

    /// <summary>
    /// GR-2.3: Get trigger template, optionally filtered by language.
    /// Falls back to any language if no match found for specified lang.
    /// </summary>
    public virtual async Task<TemplateDto?> GetTriggerTemplateAsync(
        int tenantId, string triggerEvent, string? lang = null, CancellationToken ct = default)
    {
        var sql = @"
            SELECT id, name, trigger_event, message_template, variables_json,
                   is_active, created_at, updated_at, lang
            FROM outbound_templates
            WHERE tenant_id = @tid AND trigger_event = @evt AND is_active = TRUE"
            + (lang != null ? " AND lang = @lang" : "")
            + @" ORDER BY updated_at DESC
            LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("evt", triggerEvent);
        if (lang != null)
            cmd.Parameters.AddWithValue("lang", lang);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadTemplateDto(reader);
        return null;
    }

    /// <summary>
    /// GR-2.3: Create template with language tag.
    /// </summary>
    public virtual async Task<int> CreateTemplateAsync(
        int tenantId, string name, string triggerEvent,
        string messageTemplate, Dictionary<string, string>? variablesJson,
        string lang = "tr", CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO outbound_templates
                (tenant_id, name, trigger_event, message_template, variables_json, lang)
            VALUES (@tid, @name, @evt, @tpl, @vars::jsonb, @lang)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("evt", triggerEvent);
        cmd.Parameters.AddWithValue("tpl", messageTemplate);
        cmd.Parameters.AddWithValue("vars",
            variablesJson != null ? (object)JsonSerializer.Serialize(variablesJson) : DBNull.Value);
        cmd.Parameters.AddWithValue("lang", lang);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(id);
    }

    public virtual async Task<bool> UpdateTemplateAsync(
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
        if (req.Lang != null)
        {
            setClauses.Add("lang = @lang");
            parameters.Add(new NpgsqlParameter("lang", req.Lang));
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

    public virtual async Task<bool> DeactivateTemplateAsync(
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

    /// <summary>
    /// GR-2.3: Create broadcast with optional language tag.
    /// </summary>
    public virtual async Task<Guid> CreateBroadcastAsync(
        int tenantId, int templateId, int totalRecipients, int queued,
        DateTime? scheduledAt, string? lang = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO outbound_broadcasts
                (tenant_id, template_id, total_recipients, queued, status, scheduled_at, lang)
            VALUES (@tid, @tmpl, @total, @queued, 'queued', @sched, @lang)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("tmpl", templateId);
        cmd.Parameters.AddWithValue("total", totalRecipients);
        cmd.Parameters.AddWithValue("queued", queued);
        cmd.Parameters.AddWithValue("sched", scheduledAt.HasValue ? (object)scheduledAt.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("lang", (object?)lang ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(ct);
        return (Guid)id!;
    }

    public virtual async Task<BroadcastStatusResponse?> GetBroadcastStatusAsync(
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

    public virtual async Task UpdateBroadcastStatusAsync(
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

    public virtual async Task IncrementBroadcastCounterAsync(
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
    public virtual async Task<bool> IsBroadcastCompleteAsync(
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

    /// <summary>
    /// GR-2.3: Insert message with optional language tag.
    /// </summary>
    public virtual async Task<long> InsertMessageAsync(
        int tenantId, Guid? broadcastId, int? templateId,
        string recipientPhone, string messageText,
        string? lang = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO outbound_messages
                (tenant_id, broadcast_id, template_id, recipient_phone, message_text, status, lang)
            VALUES (@tid, @bid, @tmpl, @phone, @msg, 'queued', @lang)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("bid", broadcastId.HasValue ? (object)broadcastId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("tmpl", templateId.HasValue ? (object)templateId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("phone", recipientPhone);
        cmd.Parameters.AddWithValue("msg", messageText);
        cmd.Parameters.AddWithValue("lang", (object?)lang ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    /// <summary>Dequeue next batch of messages to send, respecting rate limit.</summary>
    public virtual async Task<List<QueuedMessage>> DequeueMessagesAsync(
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

    public virtual async Task UpdateMessageStatusAsync(
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
    public virtual async Task<(long messageId, Guid? broadcastId, int tenantId)?> FindMessageByExternalIdAsync(
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

    public virtual async Task<bool> IsOptedOutAsync(
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

    public virtual async Task<DateTime?> GetOptOutDateAsync(
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

    public virtual async Task<bool> AddOptOutAsync(
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

    public virtual async Task<bool> RemoveOptOutAsync(
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
    public virtual async Task<HashSet<string>> BatchCheckOptOutsAsync(
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

    /// <summary>
    /// GR-2.3: Batch insert messages with optional language tag.
    /// </summary>
    public virtual async Task BatchInsertMessagesAsync(
        int tenantId, Guid broadcastId, int templateId,
        List<(string phone, string text)> messages,
        string? lang = null, CancellationToken ct = default)
    {
        if (messages.Count == 0) return;

        await using var conn = await _db.OpenConnectionAsync(ct);

        // Build multi-row VALUES clause
        var valueClauses = new List<string>();
        await using var cmd = new NpgsqlCommand();
        cmd.Connection = conn;

        for (var i = 0; i < messages.Count; i++)
        {
            valueClauses.Add($"(@tid, @bid, @tmpl, @phone{i}, @msg{i}, 'queued', @lang)");
            cmd.Parameters.AddWithValue($"phone{i}", messages[i].phone);
            cmd.Parameters.AddWithValue($"msg{i}", messages[i].text);
        }

        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("bid", broadcastId);
        cmd.Parameters.AddWithValue("tmpl", templateId);
        cmd.Parameters.AddWithValue("lang", (object?)lang ?? DBNull.Value);

        cmd.CommandText = $@"
            INSERT INTO outbound_messages
                (tenant_id, broadcast_id, template_id, recipient_phone, message_text, status, lang)
            VALUES {string.Join(",\n                   ", valueClauses)}";

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Reset stale 'sending' messages back to 'queued' on service shutdown.</summary>
    public virtual async Task ResetSendingMessagesAsync(CancellationToken ct = default)
    {
        const string sql = "UPDATE outbound_messages SET status = 'queued' WHERE status = 'sending'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows > 0)
            _logger.SystemWarn($"Reset {rows} stale 'sending' messages back to 'queued' on shutdown");
    }

    // ================================================================
    // Consent Records (GR-3.26)
    // ================================================================

    public virtual async Task<bool> HasMarketingConsentAsync(
        int tenantId, string customerPhone, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT 1 FROM consent_records
            WHERE tenant_id = @tid AND customer_phone = @phone
              AND consent_type IN ('marketing', 'all')
              AND opted_in = TRUE
            LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", customerPhone);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result != null;
    }

    public virtual async Task<HashSet<string>> BatchCheckMarketingConsentAsync(
        int tenantId, List<string> phones, CancellationToken ct = default)
    {
        if (phones.Count == 0) return new HashSet<string>();

        const string sql = @"
            SELECT DISTINCT customer_phone FROM consent_records
            WHERE tenant_id = @tid AND customer_phone = ANY(@phones)
              AND consent_type IN ('marketing', 'all')
              AND opted_in = TRUE";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phones", phones.ToArray());

        var consented = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            consented.Add(reader.GetString(0));
        return consented;
    }

    public virtual async Task UpsertConsentAsync(
        int tenantId, string customerPhone, string consentType,
        string channel, string? source, bool optedIn,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO consent_records
                (tenant_id, customer_phone, consent_type, channel, source, opted_in, opted_in_at)
            VALUES
                (@tid, @phone, @type, @channel, @source, @optedIn, CASE WHEN @optedIn THEN NOW() ELSE NULL END)
            ON CONFLICT (tenant_id, customer_phone, consent_type)
            DO UPDATE SET
                opted_in = EXCLUDED.opted_in,
                channel = EXCLUDED.channel,
                source = EXCLUDED.source,
                opted_in_at = CASE WHEN EXCLUDED.opted_in THEN NOW() ELSE consent_records.opted_in_at END,
                opted_out_at = CASE WHEN NOT EXCLUDED.opted_in THEN NOW() ELSE consent_records.opted_out_at END,
                updated_at = NOW()";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", customerPhone);
        cmd.Parameters.AddWithValue("type", consentType);
        cmd.Parameters.AddWithValue("channel", channel);
        cmd.Parameters.AddWithValue("source", (object?)source ?? DBNull.Value);
        cmd.Parameters.AddWithValue("optedIn", optedIn);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public virtual async Task<List<(int Id, string ConsentType, string Channel, bool OptedIn, DateTime? OptedInAt, DateTime? OptedOutAt)>>
        GetConsentRecordsAsync(int tenantId, string customerPhone, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, consent_type, channel, opted_in, opted_in_at, opted_out_at
            FROM consent_records
            WHERE tenant_id = @tid AND customer_phone = @phone
            ORDER BY created_at DESC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", customerPhone);

        var records = new List<(int, string, string, bool, DateTime?, DateTime?)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            records.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                reader.IsDBNull(5) ? null : reader.GetDateTime(5)
            ));
        }
        return records;
    }

    // ================================================================
    // Template Audit Trail (GR-3.29)
    // ================================================================

    public virtual async Task InsertAuditTrailAsync(
        int tenantId, int? templateId, int? campaignId,
        string recipientPhone, string templateContent,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO template_audit_trail
                (tenant_id, template_id, campaign_id, recipient_phone, template_content)
            VALUES (@tid, @tmpl, @campaign, @phone, @content)";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("tmpl", (object?)templateId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("campaign", (object?)campaignId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone", recipientPhone);
        cmd.Parameters.AddWithValue("content", templateContent);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// GR-3.29: Batch insert audit trail records (single multi-row INSERT for broadcast perf).
    /// </summary>
    public virtual async Task BatchInsertAuditTrailAsync(
        int tenantId, int? templateId, int? campaignId,
        List<(string phone, string content)> records,
        CancellationToken ct = default)
    {
        if (records.Count == 0) return;

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var batch = new NpgsqlBatch(conn);

        foreach (var (phone, content) in records)
        {
            var cmd = new NpgsqlBatchCommand(@"
                INSERT INTO template_audit_trail
                    (tenant_id, template_id, campaign_id, recipient_phone, template_content)
                VALUES ($1, $2, $3, $4, $5)");
            cmd.Parameters.AddWithValue(tenantId);
            cmd.Parameters.AddWithValue((object?)templateId ?? DBNull.Value);
            cmd.Parameters.AddWithValue((object?)campaignId ?? DBNull.Value);
            cmd.Parameters.AddWithValue(phone);
            cmd.Parameters.AddWithValue(content);
            batch.BatchCommands.Add(cmd);
        }

        await batch.ExecuteNonQueryAsync(ct);
    }

    // ================================================================
    // Data Deletion (GR-3.29)
    // ================================================================

    public virtual async Task<int> CreateDeletionRequestAsync(
        int tenantId, string customerPhone, string? requestedBy,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO data_deletion_requests (tenant_id, customer_phone, requested_by)
            VALUES (@tid, @phone, @by)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", customerPhone);
        cmd.Parameters.AddWithValue("by", (object?)requestedBy ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public virtual async Task<List<string>> ExecuteDataDeletionAsync(
        int tenantId, string customerPhone, CancellationToken ct = default)
    {
        var cleaned = new List<string>();

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // Delete consent records
            await using (var cmd = new NpgsqlCommand(
                "DELETE FROM consent_records WHERE tenant_id = @tid AND customer_phone = @phone", conn, tx))
            {
                cmd.Parameters.AddWithValue("tid", tenantId);
                cmd.Parameters.AddWithValue("phone", customerPhone);
                var rows = await cmd.ExecuteNonQueryAsync(ct);
                if (rows > 0) cleaned.Add("consent_records");
            }

            // Delete template audit trail
            await using (var cmd = new NpgsqlCommand(
                "DELETE FROM template_audit_trail WHERE tenant_id = @tid AND recipient_phone = @phone", conn, tx))
            {
                cmd.Parameters.AddWithValue("tid", tenantId);
                cmd.Parameters.AddWithValue("phone", customerPhone);
                var rows = await cmd.ExecuteNonQueryAsync(ct);
                if (rows > 0) cleaned.Add("template_audit_trail");
            }

            // Delete opt-out records
            await using (var cmd = new NpgsqlCommand(
                "DELETE FROM outbound_optouts WHERE tenant_id = @tid AND phone = @phone", conn, tx))
            {
                cmd.Parameters.AddWithValue("tid", tenantId);
                cmd.Parameters.AddWithValue("phone", customerPhone);
                var rows = await cmd.ExecuteNonQueryAsync(ct);
                if (rows > 0) cleaned.Add("outbound_optouts");
            }

            // Anonymize outbound messages (nullify phone, keep stats)
            await using (var cmd = new NpgsqlCommand(
                "UPDATE outbound_messages SET recipient_phone = 'DELETED', message_text = '[DELETED]' WHERE tenant_id = @tid AND recipient_phone = @phone", conn, tx))
            {
                cmd.Parameters.AddWithValue("tid", tenantId);
                cmd.Parameters.AddWithValue("phone", customerPhone);
                var rows = await cmd.ExecuteNonQueryAsync(ct);
                if (rows > 0) cleaned.Add("outbound_messages");
            }

            await tx.CommitAsync(ct);
        }
        catch (NpgsqlException)
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return cleaned;
    }

    public virtual async Task UpdateDeletionRequestAsync(
        int tenantId, int requestId, string status, List<string> servicesCleaned,
        string? errorMessage, CancellationToken ct = default)
    {
        var sql = @"
            UPDATE data_deletion_requests
            SET status = @status,
                services_cleaned = @services::jsonb,
                error_message = @error,
                completed_at = CASE WHEN @status IN ('completed', 'failed') THEN NOW() ELSE NULL END
            WHERE id = @id AND tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", requestId);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("services", System.Text.Json.JsonSerializer.Serialize(servicesCleaned));
        cmd.Parameters.AddWithValue("error", (object?)errorMessage ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ============================================================
    // Campaigns (GR-3.15)
    // ============================================================

    public virtual async Task<int> CreateCampaignAsync(
        int tenantId, string name, string triggerType, int templateId,
        int? abTemplateId, int abSplitPct,
        string? targetCriteriaJson, string? scheduleJson,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO outbound_campaigns
                (tenant_id, name, trigger_type, template_id,
                 ab_template_id, ab_split_pct, target_criteria_json, schedule_json, status)
            VALUES
                (@tid, @name, @type, @tmpl,
                 @abTmpl, @abPct, @criteria::jsonb, @schedule::jsonb, 'draft')
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("type", triggerType);
        cmd.Parameters.AddWithValue("tmpl", templateId);
        cmd.Parameters.AddWithValue("abTmpl", (object?)abTemplateId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("abPct", abSplitPct);
        cmd.Parameters.AddWithValue("criteria", (object?)targetCriteriaJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("schedule", (object?)scheduleJson ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public virtual async Task<CampaignResponse?> GetCampaignAsync(
        int tenantId, int campaignId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, name, trigger_type, template_id, ab_template_id,
                   ab_split_pct, status, stats_json::text, created_at, updated_at
            FROM outbound_campaigns
            WHERE tenant_id = @tid AND id = @cid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("cid", campaignId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ReadCampaignResponse(reader);
    }

    public virtual async Task<List<CampaignResponse>> ListCampaignsAsync(
        int tenantId, string? status, CancellationToken ct = default)
    {
        var conditions = new List<string> { "tenant_id = @tid" };
        var parameters = new List<NpgsqlParameter> { new("tid", tenantId) };

        if (status != null)
        {
            conditions.Add("status = @status");
            parameters.Add(new("status", status));
        }

        var sql = $@"
            SELECT id, name, trigger_type, template_id, ab_template_id,
                   ab_split_pct, status, stats_json::text, created_at, updated_at
            FROM outbound_campaigns
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY created_at DESC LIMIT 100";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters.ToArray());

        var campaigns = new List<CampaignResponse>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            campaigns.Add(ReadCampaignResponse(reader));
        return campaigns;
    }

    public virtual async Task<bool> UpdateCampaignStatusAsync(
        int tenantId, int campaignId, string newStatus, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE outbound_campaigns SET status = @status, updated_at = NOW()
            WHERE tenant_id = @tid AND id = @cid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("cid", campaignId);
        cmd.Parameters.AddWithValue("status", newStatus);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public virtual async Task UpdateCampaignStatsAsync(
        int tenantId, int campaignId, string statsJson, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE outbound_campaigns SET stats_json = @stats::jsonb, updated_at = NOW()
            WHERE id = @cid AND tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("cid", campaignId);
        cmd.Parameters.AddWithValue("stats", statsJson);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private CampaignResponse ReadCampaignResponse(NpgsqlDataReader reader)
    {
        CampaignStats? stats = null;
        var statsStr = reader.IsDBNull(7) ? null : reader.GetString(7);
        if (statsStr != null)
        {
            try { stats = JsonSerializer.Deserialize<CampaignStats>(statsStr); }
            catch (JsonException) { _logger.SystemWarn($"ReadCampaignResponse: malformed stats_json for campaign, skipping deserialization"); }
        }

        return new CampaignResponse
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            TriggerType = reader.GetString(2),
            TemplateId = reader.GetInt32(3),
            AbTemplateId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            AbSplitPct = reader.GetInt32(5),
            Status = reader.GetString(6),
            Stats = stats,
            CreatedAt = reader.GetDateTime(8),
            UpdatedAt = reader.GetDateTime(9)
        };
    }

    // ============================================================
    // Conversions (GR-3.15)
    // ============================================================

    public virtual async Task<int> RecordConversionAsync(
        int tenantId, long? messageId, int? campaignId,
        string conversionType, decimal? valueAmount,
        string? metadataJson, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO outbound_conversions
                (tenant_id, message_id, campaign_id, conversion_type, value_amount, metadata_json)
            VALUES
                (@tid, @msg, @cmp, @type, @value, @meta::jsonb)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("msg", (object?)messageId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cmp", (object?)campaignId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("type", conversionType);
        cmd.Parameters.AddWithValue("value", (object?)valueAmount ?? DBNull.Value);
        cmd.Parameters.AddWithValue("meta", (object?)metadataJson ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public virtual async Task<CampaignRoiResponse?> GetCampaignRoiAsync(
        int tenantId, int campaignId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT c.id, c.name,
                   COALESCE((c.stats_json->>'sent')::int, 0),
                   COALESCE((c.stats_json->>'delivered')::int, 0),
                   COUNT(cv.id),
                   COALESCE(SUM(cv.value_amount), 0)
            FROM outbound_campaigns c
            LEFT JOIN outbound_conversions cv ON cv.campaign_id = c.id AND cv.tenant_id = c.tenant_id
            WHERE c.tenant_id = @tid AND c.id = @cid
            GROUP BY c.id, c.name, c.stats_json";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("cid", campaignId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var totalSent = reader.GetInt32(2);
        var totalConversions = reader.GetInt32(4);

        return new CampaignRoiResponse
        {
            CampaignId = reader.GetInt32(0),
            CampaignName = reader.GetString(1),
            TotalSent = totalSent,
            TotalDelivered = reader.GetInt32(3),
            TotalConversions = totalConversions,
            ConversionRate = totalSent > 0 ? Math.Round((double)totalConversions / totalSent * 100, 2) : 0,
            TotalRevenue = reader.GetDecimal(5)
        };
    }

    // ============================================================
    // KVKK health tenant check (GR-2.6)
    // ============================================================

    public virtual async Task<(string? settingsJson, string? sector)> GetTenantHealthInfoAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT settings_json::text, sector
            FROM tenant_registry
            WHERE tenant_id = @tid AND is_active = TRUE";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var settingsJson = reader.IsDBNull(0) ? null : reader.GetString(0);
            var sector = reader.IsDBNull(1) ? null : reader.GetString(1);
            return (settingsJson, sector);
        }
        return (null, null);
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
            UpdatedAt = reader.GetDateTime(7),
            Lang = reader.FieldCount > 8 && !reader.IsDBNull(8) ? reader.GetString(8) : "tr"
        };
    }

    // ================================================================
    // FEAT-J2: INMA opt-out outbox (migration 017)
    // ================================================================

    /// <summary>
    /// Idempotent enqueue — ON CONFLICT DO NOTHING on
    /// (tenant_id, phone, event_type, date_trunc('second', created_at)).
    /// Returns true when a new row was inserted, false when deduped by the unique index.
    /// </summary>
    public virtual async Task<bool> EnqueueOptOutSyncAsync(
        int tenantId, string phone, int instanceId, string eventType,
        string scope, string? reason, string? source, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO inma_optout_outbox (tenant_id, phone, instance_id, event_type, scope, reason, source)
            VALUES (@tid, @phone, @iid, @et, @scope, @reason, @source)
            ON CONFLICT (tenant_id, phone, event_type, (date_trunc('second', created_at))) DO NOTHING
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", phone);
        cmd.Parameters.AddWithValue("iid", instanceId);
        cmd.Parameters.AddWithValue("et", eventType);
        cmd.Parameters.AddWithValue("scope", scope);
        cmd.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("source", (object?)source ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(ct);
        return id != null;
    }

    /// <summary>
    /// Atomically claims up to <paramref name="limit"/> pending rows by flipping
    /// their status to 'processing' in a single UPDATE ... RETURNING. The inner
    /// SELECT uses FOR UPDATE SKIP LOCKED, so parallel workers (multi-instance
    /// Outbound deployments) never pick the same row — the DB serialises access.
    /// Using a simple FOR UPDATE + external transaction was rejected because the
    /// caller's connection/transaction scope would have to span the whole
    /// outbound HTTP call to INMA, blocking other drainers for seconds.
    /// </summary>
    public virtual async Task<List<OutboxRow>> FetchPendingOutboxBatchAsync(
        int limit, int maxAttempts, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE inma_optout_outbox AS o
            SET status = 'processing',
                attempted_at = NOW()
            WHERE o.id IN (
                SELECT id FROM inma_optout_outbox
                WHERE status = 'pending' AND attempts < @maxAttempts
                ORDER BY created_at
                LIMIT @limit
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, tenant_id, phone, instance_id, event_type, scope, reason, source, attempts";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("limit", limit);
        cmd.Parameters.AddWithValue("maxAttempts", maxAttempts);

        var rows = new List<OutboxRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new OutboxRow
            {
                Id = reader.GetInt64(0),
                TenantId = reader.GetInt32(1),
                Phone = reader.GetString(2),
                InstanceId = reader.GetInt32(3),
                EventType = reader.GetString(4),
                Scope = reader.GetString(5),
                Reason = reader.IsDBNull(6) ? null : reader.GetString(6),
                Source = reader.IsDBNull(7) ? null : reader.GetString(7),
                Attempts = reader.GetInt32(8),
            });
        }
        return rows;
    }

    /// <summary>
    /// Recovers rows that the worker claimed ('processing') but never reached a
    /// terminal state — e.g. process crashed between claim and Mark*. Called at
    /// startup so the next tick picks them back up. Older than <paramref name="staleSeconds"/>
    /// to avoid stealing in-flight rows from a concurrent worker.
    /// </summary>
    public virtual async Task<int> RecoverStuckProcessingAsync(
        int staleSeconds, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE inma_optout_outbox
            SET status = 'pending'
            WHERE status = 'processing'
              AND attempted_at IS NOT NULL
              AND attempted_at < NOW() - make_interval(secs => @stale)";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("stale", staleSeconds);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Terminal success transition: status='processed', last_status_code recorded.</summary>
    public virtual async Task MarkOutboxProcessedAsync(
        long id, string lastStatusCode, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE inma_optout_outbox
            SET status = 'processed',
                last_status_code = @code,
                attempted_at = NOW(),
                processed_at = NOW()
            WHERE id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("code", lastStatusCode);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Failure transition: attempts++; when isFinal=true OR attempts>=maxAttempts the
    /// row moves to status='failed' (retry stops). isFinal=true is used for
    /// INMA 908 contact-not-found so we do not waste retries. Otherwise the row
    /// returns to 'pending' so the next tick retries.
    /// </summary>
    public virtual async Task MarkOutboxFailedAsync(
        long id, string lastStatusCode, string? rawError, bool isFinal, int maxAttempts,
        CancellationToken ct = default)
    {
        // Note: we flip 'processing'→'pending'/'failed' here. attempts is
        // incremented before the CASE so the threshold matches post-increment.
        const string sql = @"
            UPDATE inma_optout_outbox
            SET attempts = attempts + 1,
                status = CASE
                    WHEN @isFinal OR attempts + 1 >= @maxAttempts THEN 'failed'
                    ELSE 'pending'
                END,
                last_status_code = @code,
                last_error = @err,
                attempted_at = NOW()
            WHERE id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("code", lastStatusCode);
        cmd.Parameters.AddWithValue("err", (object?)rawError ?? DBNull.Value);
        cmd.Parameters.AddWithValue("isFinal", isFinal);
        cmd.Parameters.AddWithValue("maxAttempts", maxAttempts);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>NoOp-mode parking: row moves to 'skipped_noop' for later drain.</summary>
    public virtual async Task MarkOutboxSkippedNoOpAsync(long id, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE inma_optout_outbox
            SET status = 'skipped_noop',
                last_status_code = 'SKIPPED-NOOP',
                attempted_at = NOW()
            WHERE id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Admin ops drain: promotes 'skipped_noop' rows back to 'pending' (attempts reset to 0)
    /// after Mode is flipped from NoOp to Http. tenantId=null drains all tenants;
    /// sinceUtc=null drains all history.
    /// </summary>
    public virtual async Task<int> RetrySkippedNoOpAsync(
        int? tenantId, DateTime? sinceUtc, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE inma_optout_outbox
            SET status = 'pending',
                attempts = 0,
                last_status_code = NULL,
                last_error = NULL,
                attempted_at = NULL
            WHERE status = 'skipped_noop'
              AND (@tid::int IS NULL OR tenant_id = @tid)
              AND (@since::timestamptz IS NULL OR created_at >= @since)";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", (object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("since", (object?)sinceUtc ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Diagnostics: count outbox rows by status for ops dashboard.</summary>
    public virtual async Task<Dictionary<string, long>> GetOutboxStatusCountsAsync(
        int? tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT status, COUNT(*)
            FROM inma_optout_outbox
            WHERE (@tid::int IS NULL OR tenant_id = @tid)
            GROUP BY status";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", (object?)tenantId ?? DBNull.Value);

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            counts[reader.GetString(0)] = reader.GetInt64(1);
        }
        return counts;
    }
}

/// <summary>FEAT-J2: single outbox row handed to InmaOptOutSyncJob.</summary>
public sealed class OutboxRow
{
    public long Id { get; init; }
    public int TenantId { get; init; }
    public string Phone { get; init; } = "";
    public int InstanceId { get; init; }
    public string EventType { get; init; } = "";
    public string Scope { get; init; } = "";
    public string? Reason { get; init; }
    public string? Source { get; init; }
    public int Attempts { get; init; }
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
