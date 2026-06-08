using System.Text.Json;
using Invekto.Shared.Contracts.Inma.Dtos;
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
    /// <summary>
    /// FEAT-PROJELER / cxapi (PR-3a): <paramref name="sendRoute"/> + <paramref name="instanceId"/> are
    /// AFTER <paramref name="ct"/> so existing positional callers compile unchanged; they default to the
    /// bridge / NULL (no-op). A cxapi-allowlisted tenant passes 'wapcrm_cxapi' + the tenant-default instance.
    /// </summary>
    public virtual async Task<Guid> CreateBroadcastAsync(
        int tenantId, int templateId, int totalRecipients, int queued,
        DateTime? scheduledAt, string? lang = null, CancellationToken ct = default,
        string sendRoute = "mainapp_bridge", int? instanceId = null)
    {
        const string sql = @"
            INSERT INTO outbound_broadcasts
                (tenant_id, template_id, total_recipients, queued, status, scheduled_at, lang, send_route, instance_id)
            VALUES (@tid, @tmpl, @total, @queued, 'queued', @sched, @lang, @route, @inst)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("tmpl", templateId);
        cmd.Parameters.AddWithValue("total", totalRecipients);
        cmd.Parameters.AddWithValue("queued", queued);
        cmd.Parameters.AddWithValue("sched", scheduledAt.HasValue ? (object)scheduledAt.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("lang", (object?)lang ?? DBNull.Value);
        cmd.Parameters.AddWithValue("route", sendRoute);
        cmd.Parameters.AddWithValue("inst", instanceId.HasValue ? (object)instanceId.Value : DBNull.Value);

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

    /// <summary>
    /// Check if all messages in a broadcast are processed (no more queued/sending/posting).
    /// FEAT-PROJELER / cxapi (PR-3a): 'posting' is non-terminal (a cxapi POST may be in flight),
    /// so a broadcast is NOT complete while any row is 'posting'. 'ambiguous' is terminal
    /// (manual/ops) and does not block completion. Bridge rows never reach 'posting'.
    /// </summary>
    public virtual async Task<bool> IsBroadcastCompleteAsync(
        Guid broadcastId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*) FROM outbound_messages
            WHERE broadcast_id = @bid AND status IN ('queued', 'sending', 'posting')";

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
    /// GR-2.3: Insert message with optional language tag. FEAT-DMP adds <paramref name="dynamicFields"/>
    /// for INMA DynamicMessage mode — non-null, non-empty list signals the bridge to forward the
    /// placeholder list to <c>wapPayload.dynamicMessageFields</c> and MessageText ships raw.
    /// <paramref name="dynamicFields"/> is AFTER <paramref name="ct"/> so existing positional
    /// callers (<c>tenantId, ..., lang, ct</c>) compile unchanged \u2014 iter 3 fix for CQ8.
    /// </summary>
    public virtual async Task<long> InsertMessageAsync(
        int tenantId, Guid? broadcastId, int? templateId,
        string recipientPhone, string messageText,
        string? lang = null, CancellationToken ct = default, string[]? dynamicFields = null)
    {
        // PR-1 (migration 055) NO-OP: send_route/message_kind/attempt_count are
        // intentionally OMITTED here so they take their DB column DEFAULTs
        // ('mainapp_bridge' / 'plain_text' / 0); the cxapi template/provider
        // columns stay NULL. The bridge send path is unchanged.
        const string sql = @"
            INSERT INTO outbound_messages
                (tenant_id, broadcast_id, template_id, recipient_phone, message_text, status, lang, dynamic_fields)
            VALUES (@tid, @bid, @tmpl, @phone, @msg, 'queued', @lang, @df)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("bid", broadcastId.HasValue ? (object)broadcastId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("tmpl", templateId.HasValue ? (object)templateId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("phone", recipientPhone);
        cmd.Parameters.AddWithValue("msg", messageText);
        cmd.Parameters.AddWithValue("lang", (object?)lang ?? DBNull.Value);
        cmd.Parameters.AddWithValue("df", (object?)dynamicFields ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    /// <summary>
    /// Dequeue next batch of messages to send, respecting rate limit.
    /// <para>
    /// <b>Tenant-scope note (iter 6, pre-existing design preserved):</b> this UPDATE...RETURNING
    /// runs in <see cref="MessageSenderService"/>, a SINGLETON background worker that drains the
    /// outbound queue ACROSS ALL tenants. The tenant_id is stored PER-ROW on
    /// <c>outbound_messages</c> and returned in the projection; downstream per-message handling
    /// (<see cref="MessageSenderService.SendMessageAsync"/>) reads it from the claimed row and
    /// respects tenant-scope at the callback layer. This intentionally bypasses the "filter by
    /// tenant_id in WHERE" rule because there is no tenant context at dequeue time — the worker
    /// pulls whichever N queued rows are ready. FEAT-DMP (iter 6) only adds <c>dynamic_fields</c>
    /// to the RETURNING list; cross-tenant claim behaviour is unchanged from GR-2.3.
    /// </para>
    /// </summary>
    public virtual async Task<List<QueuedMessage>> DequeueMessagesAsync(
        int batchSize, CancellationToken ct = default)
    {
        // FEAT-PROJELER / cxapi (PR-1, migration 055): the projection now also reads
        // the new route/template/provider columns into QueuedMessage. This is READ
        // wiring ONLY — MessageSenderService keeps routing on the existing
        // broadcast_id/bridge logic and does NOT consume these fields in PR-1. PR-3
        // switches routing onto send_route/message_kind; the columns are surfaced now
        // so that change stays a one-file edit (and to prove the schema is live, not
        // a dead field). All new columns carry safe defaults / NULL today.
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
                      recipient_phone, message_text, dynamic_fields,
                      send_route, message_kind, instance_id, template_ref,
                      template_params, template_language, template_header_media,
                      provider_status_code, provider_status, provider_request_id,
                      provider_error_message, last_attempt_at, attempt_count";

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
                MessageText = reader.GetString(5),
                DynamicFields = reader.IsDBNull(6) ? null : (string[])reader.GetValue(6),
                // PR-1 reserved columns (read-only groundwork; not consumed yet)
                SendRoute = reader.GetString(7),
                MessageKind = reader.GetString(8),
                InstanceId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                TemplateRef = reader.IsDBNull(10) ? null : reader.GetString(10),
                TemplateParams = reader.IsDBNull(11) ? null : reader.GetString(11),
                TemplateLanguage = reader.IsDBNull(12) ? null : reader.GetString(12),
                TemplateHeaderMedia = reader.IsDBNull(13) ? null : reader.GetString(13),
                ProviderStatusCode = reader.IsDBNull(14) ? null : reader.GetString(14),
                ProviderStatus = reader.IsDBNull(15) ? null : reader.GetBoolean(15),
                ProviderRequestId = reader.IsDBNull(16) ? null : reader.GetString(16),
                ProviderErrorMessage = reader.IsDBNull(17) ? null : reader.GetString(17),
                LastAttemptAt = reader.IsDBNull(18) ? null : reader.GetDateTime(18),
                AttemptCount = reader.GetInt32(19)
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

    /// <summary>
    /// Outcome of <see cref="ApplyDeliveryStatusAsync"/>. <see cref="Matched"/>=false -> no row for
    /// (tenant_id, external_message_id) (caller returns 404). <see cref="Applied"/>=false on a matched
    /// row whose status did not advance (idempotent / out-of-order no-op; caller returns 200).
    /// <see cref="PreviousStatus"/>/<see cref="RowInstanceId"/> are the matched row's pre-apply values
    /// (always populated when Matched) so the caller can run the soft InstanceID cross-check log.
    /// </summary>
    public readonly record struct DeliveryStatusApplyResult(
        bool Matched, bool Applied, string? PreviousStatus, int? RowInstanceId);

    /// <summary>
    /// PR-3b-2: apply a delivery-status ack to exactly one tenant-scoped message ATOMICALLY (single CTE,
    /// mirrors <see cref="MarkCxapiOutcomeAsync"/>). The lookup is tenant-scoped on
    /// (tenant_id, external_message_id) — backed by the partial UNIQUE uq_outbound_messages_tenant_external_id
    /// (migration 058) — so a wamid resolves to exactly ONE tenant's row (no cross-tenant mis-attribution).
    /// Enforces a monotonic downgrade guard (sent&lt;delivered&lt;read, forward-only; 'failed' applies only
    /// over queued/sending/posting/sent; never regress read-&gt;delivered, never override a delivered/read with
    /// failed) and keeps outbound_broadcasts counters PARTITION-correct: on a REAL transition (and only then)
    /// with a broadcast_id, the new bucket is incremented and the LIVE old bucket decremented, so a duplicate
    /// or out-of-order ack is a no-op that never double-counts (the invariant
    /// sent+delivered+read+failed+ambiguous+queued = total_recipients holds). FOR UPDATE serializes
    /// concurrent acks for the same row so the bucket-move reads the committed prior status, not a stale
    /// snapshot. All values are parameterized and the SQL is fully static (no dynamic column names).
    /// </summary>
    public virtual async Task<DeliveryStatusApplyResult> ApplyDeliveryStatusAsync(
        int tenantId, string externalMessageId, string status, string? failedReason = null, CancellationToken ct = default)
    {
        const string sql = @"
            WITH cur AS (
                SELECT id, status AS old_status, broadcast_id, instance_id
                FROM outbound_messages
                WHERE tenant_id = @tid AND external_message_id = @eid
                FOR UPDATE
            ),
            upd AS (
                UPDATE outbound_messages m
                SET status = @new,
                    sent_at       = CASE WHEN @new = 'sent'      AND m.sent_at      IS NULL THEN NOW() ELSE m.sent_at END,
                    delivered_at  = CASE WHEN @new = 'delivered' AND m.delivered_at IS NULL THEN NOW() ELSE m.delivered_at END,
                    read_at       = CASE WHEN @new = 'read'      AND m.read_at      IS NULL THEN NOW() ELSE m.read_at END,
                    failed_reason = CASE WHEN @new = 'failed' THEN @fail ELSE m.failed_reason END
                FROM cur
                WHERE m.id = cur.id
                  AND (
                        (@new IN ('sent','delivered','read')
                           AND cur.old_status IN ('queued','sending','posting','sent','delivered')
                           AND (CASE cur.old_status WHEN 'sent' THEN 1 WHEN 'delivered' THEN 2 WHEN 'read' THEN 3 ELSE 0 END)
                               < (CASE @new WHEN 'sent' THEN 1 WHEN 'delivered' THEN 2 WHEN 'read' THEN 3 END))
                     OR (@new = 'failed' AND cur.old_status IN ('queued','sending','posting','sent'))
                      )
                RETURNING cur.old_status AS old_status, m.broadcast_id
            ),
            cnt AS (
                UPDATE outbound_broadcasts b
                SET sent      = GREATEST(b.sent      + (CASE WHEN @new = 'sent'      THEN 1 ELSE 0 END) - (CASE WHEN u.old_status = 'sent'      THEN 1 ELSE 0 END), 0),
                    delivered = GREATEST(b.delivered + (CASE WHEN @new = 'delivered' THEN 1 ELSE 0 END) - (CASE WHEN u.old_status = 'delivered' THEN 1 ELSE 0 END), 0),
                    read      = GREATEST(b.read      + (CASE WHEN @new = 'read'      THEN 1 ELSE 0 END) - (CASE WHEN u.old_status = 'read'      THEN 1 ELSE 0 END), 0),
                    failed    = b.failed + (CASE WHEN @new = 'failed' THEN 1 ELSE 0 END),
                    queued    = GREATEST(b.queued - (CASE WHEN u.old_status IN ('queued','sending','posting') THEN 1 ELSE 0 END), 0)
                FROM upd u
                WHERE b.id = u.broadcast_id AND b.tenant_id = @tid
                RETURNING b.id
            )
            SELECT (SELECT old_status  FROM cur) AS prev_status,
                   (SELECT instance_id FROM cur) AS row_instance_id,
                   EXISTS (SELECT 1 FROM cur) AS matched,
                   EXISTS (SELECT 1 FROM upd) AS applied";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("eid", externalMessageId);
        cmd.Parameters.AddWithValue("new", status);
        cmd.Parameters.AddWithValue("fail", (object?)failedReason ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new DeliveryStatusApplyResult(false, false, null, null);

        var prevStatus = reader.IsDBNull(0) ? null : reader.GetString(0);
        var rowInstanceId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
        var matched = reader.GetBoolean(2);
        var applied = reader.GetBoolean(3);
        return new DeliveryStatusApplyResult(matched, applied, prevStatus, rowInstanceId);
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
    /// GR-2.3 legacy overload: forwards to the FEAT-DMP-aware signature with
    /// <c>dynamicFields=null</c> on every row so existing callers compiled against the
    /// original <c>(phone, text)</c> tuple shape stay source-compatible (iter 3 CQ8 fix).
    /// </summary>
    public virtual Task BatchInsertMessagesAsync(
        int tenantId, Guid broadcastId, int templateId,
        List<(string phone, string text)> messages,
        string? lang = null, CancellationToken ct = default)
        => BatchInsertMessagesAsync(
            tenantId, broadcastId, templateId,
            messages.Select(m => (m.phone, m.text, (string[]?)null)).ToList(),
            lang, ct);

    /// <summary>
    /// GR-2.3: Batch insert messages with optional language tag. FEAT-DMP: each message
    /// carries an optional <c>dynamicFields</c> array (INMA placeholder keys) that the
    /// bridge forwards to <c>chatoperation.dynamicMessageFields</c> on dispatch. Null =
    /// legacy INSE-substituted text (backward-compat).
    /// </summary>
    public virtual async Task BatchInsertMessagesAsync(
        int tenantId, Guid broadcastId, int templateId,
        List<(string phone, string text, string[]? dynamicFields)> messages,
        string? lang = null, CancellationToken ct = default,
        string sendRoute = "mainapp_bridge", int? instanceId = null)
    {
        if (messages.Count == 0) return;

        await using var conn = await _db.OpenConnectionAsync(ct);

        // Build multi-row VALUES clause
        var valueClauses = new List<string>();
        await using var cmd = new NpgsqlCommand();
        cmd.Connection = conn;

        for (var i = 0; i < messages.Count; i++)
        {
            valueClauses.Add($"(@tid, @bid, @tmpl, @phone{i}, @msg{i}, 'queued', @lang, @df{i}, @route, @inst)");
            cmd.Parameters.AddWithValue($"phone{i}", messages[i].phone);
            cmd.Parameters.AddWithValue($"msg{i}", messages[i].text);
            cmd.Parameters.AddWithValue($"df{i}", (object?)messages[i].dynamicFields ?? DBNull.Value);
        }

        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("bid", broadcastId);
        cmd.Parameters.AddWithValue("tmpl", templateId);
        cmd.Parameters.AddWithValue("lang", (object?)lang ?? DBNull.Value);
        // FEAT-PROJELER / cxapi (PR-3a): immutable per-message route + instance, written at create.
        // Defaults ('mainapp_bridge' / NULL) keep the bridge path a no-op; message_kind/attempt_count
        // still take their DB DEFAULTs ('plain_text' / 0).
        cmd.Parameters.AddWithValue("route", sendRoute);
        cmd.Parameters.AddWithValue("inst", instanceId.HasValue ? (object)instanceId.Value : DBNull.Value);

        cmd.CommandText = $@"
            INSERT INTO outbound_messages
                (tenant_id, broadcast_id, template_id, recipient_phone, message_text, status, lang, dynamic_fields, send_route, instance_id)
            VALUES {string.Join(",\n                   ", valueClauses)}";

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// FEAT-DMP: Read <c>tenant_settings.enable_dynamic_message</c>. Missing row (tenant
    /// has never written settings) yields <c>true</c> — matches the column default so
    /// new tenants get feature-enabled behaviour automatically. Tight DB hit (no cache);
    /// callers (BroadcastOrchestrator) should memoize per-broadcast.
    /// </summary>
    public virtual async Task<bool> GetEnableDynamicMessageAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT enable_dynamic_message
            FROM tenant_settings
            WHERE tenant_id = @tid
            LIMIT 1";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is bool b ? b : true;
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
    // FEAT-PROJELER / cxapi send engine (PR-3a) — credentials + state machine
    // ================================================================

    /// <summary>
    /// Load WapCRM settings (settings_json->'wapcrm') for a single tenant. Same shape as
    /// Backend's GetWapCrmSettingsAsync; Outbound reads the same shared Postgres.
    /// Returns null if the tenant is missing/inactive or has no wapcrm settings.
    /// </summary>
    public virtual async Task<WapCrmSettings?> GetWapCrmSettingsAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT settings_json->'wapcrm' AS wapcrm
            FROM tenant_registry
            WHERE tenant_id = @tid AND is_active = true";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return ParseWapCrmSettings(result, tenantId);
    }

    /// <summary>
    /// Batch-load WapCRM settings for the DISTINCT tenants in a dequeue cycle (Codex P0 #8: no N+1).
    /// One query for all ids; tenants with missing/unparseable wapcrm settings are simply absent from
    /// the dictionary (the sender treats them as misconfigured, INV-OB-062). The secret stays in memory
    /// only for the send and is never logged or persisted.
    /// </summary>
    public virtual async Task<Dictionary<int, WapCrmSettings>> GetWapCrmSettingsBatchAsync(
        IReadOnlyCollection<int> tenantIds, CancellationToken ct = default)
    {
        var result = new Dictionary<int, WapCrmSettings>();
        if (tenantIds.Count == 0) return result;

        const string sql = @"
            SELECT tenant_id, settings_json->'wapcrm' AS wapcrm
            FROM tenant_registry
            WHERE tenant_id = ANY(@ids) AND is_active = true";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ids", tenantIds.Distinct().ToArray());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var tenantId = reader.GetInt32(0);
            var settings = ParseWapCrmSettings(reader.IsDBNull(1) ? null : reader.GetValue(1), tenantId);
            if (settings != null)
                result[tenantId] = settings;
        }
        return result;
    }

    private WapCrmSettings? ParseWapCrmSettings(object? raw, int tenantId)
    {
        if (raw is null or DBNull) return null;
        var json = raw.ToString();
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<WapCrmSettings>(json);
        }
        catch (JsonException ex)
        {
            // INV-OB-062: an unparseable wapcrm settings block makes the tenant cxapi-misconfigured
            // (treated as missing creds downstream). Literal code (this repo file has no ErrorCodes import).
            _logger.SystemWarn($"[INV-OB-062] WapCRM settings parse failed for tenant {tenantId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// cxapi state machine: compare-and-swap a leased message ('sending') into 'posting' immediately
    /// BEFORE the cxapi POST (Codex P0 #1). Returns false (caller MUST NOT POST) when 0 rows change —
    /// the row was reset to 'queued' by a concurrent shutdown or claimed elsewhere. Uses NOW() for
    /// last_attempt_at so the stale-posting sweep keys on database time. tenant_id is included in the
    /// predicate (defense-in-depth) even though the message id is a globally-unique PK.
    /// </summary>
    public virtual async Task<bool> SetMessagePostingAsync(long messageId, int tenantId, int attemptCount, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE outbound_messages
            SET status = 'posting', attempt_count = @att, last_attempt_at = NOW()
            WHERE id = @id AND tenant_id = @tid AND status = 'sending'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", messageId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("att", attemptCount);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    /// <summary>
    /// Persist a cxapi send outcome ATOMICALLY (Codex P0-1/P0-2/P1-2): a single statement updates the
    /// message (compare-and-swap on <paramref name="fromStatus"/>, default 'posting') AND — when
    /// <paramref name="counterColumn"/> is non-null (sent|failed|ambiguous) — increments that
    /// outbound_broadcasts counter and decrements queued, so a mid-write crash can never leave a
    /// terminal message with a non-terminal counter. The CAS prevents a late return from resurrecting a
    /// row already swept to 'ambiguous' (returns applied=false). Returns the broadcast id (if any) so the
    /// caller can attempt idempotent completion. On a requeue (RateLimited under cap) pass
    /// counterColumn=null + status 'queued' — no counter changes, attempt_count persists.
    /// </summary>
    public virtual async Task<(bool applied, Guid? broadcastId)> MarkCxapiOutcomeAsync(
        long messageId, int tenantId, string status, string? providerStatusCode, bool? providerStatus,
        string? providerRequestId, string? providerErrorMessage, int attemptCount,
        string? counterColumn, string? externalMessageId = null, string fromStatus = "posting", CancellationToken ct = default)
    {
        // Identifier (column name) cannot be a SQL parameter — whitelist + interpolate, exactly like the
        // established IncrementBroadcastCounterAsync. Values stay parameterized.
        if (counterColumn != null && counterColumn is not ("sent" or "failed" or "ambiguous"))
            throw new ArgumentException($"Invalid counter column: {counterColumn}");

        // Data-modifying CTEs always execute (even if not referenced by the final SELECT), so the
        // counter UPDATE runs atomically with the message UPDATE in one round-trip. The broadcast counter
        // is tenant-scoped too (defense-in-depth; the broadcast_id already comes from this tenant's row).
        var counterCte = counterColumn != null
            ? $@", bc AS (
                UPDATE outbound_broadcasts b
                SET {counterColumn} = b.{counterColumn} + 1,
                    queued = GREATEST(b.queued - 1, 0)
                FROM upd WHERE b.id = upd.broadcast_id AND b.tenant_id = @tid
                RETURNING b.id)"
            : "";

        var sql = $@"
            WITH upd AS (
                UPDATE outbound_messages
                SET status = @status,
                    provider_status_code = @psc,
                    provider_status = @ps,
                    provider_request_id = @prid,
                    provider_error_message = @perr,
                    attempt_count = @att,
                    external_message_id = COALESCE(@eid, external_message_id),
                    last_attempt_at = NOW(),
                    sent_at = CASE WHEN @status = 'sent' THEN NOW() ELSE sent_at END,
                    failed_reason = CASE WHEN @status IN ('failed', 'ambiguous') THEN @perr ELSE failed_reason END
                WHERE id = @id AND tenant_id = @tid AND status = @from
                RETURNING broadcast_id
            ){counterCte}
            SELECT (SELECT broadcast_id FROM upd) AS broadcast_id,
                   EXISTS (SELECT 1 FROM upd) AS applied";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", messageId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("from", fromStatus);
        cmd.Parameters.AddWithValue("psc", (object?)providerStatusCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ps", providerStatus.HasValue ? (object)providerStatus.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("prid", (object?)providerRequestId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("perr", (object?)providerErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("att", attemptCount);
        // PR-3b-1: wamid (envelope 'data') captured at send time -> external_message_id. COALESCE keeps any
        // existing value when null (non-Submitted outcomes / non-string data) so a row is never blanked.
        cmd.Parameters.AddWithValue("eid", (object?)externalMessageId ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return (false, null);

        var broadcastId = reader.IsDBNull(0) ? (Guid?)null : reader.GetGuid(0);
        var applied = reader.GetBoolean(1);
        return (applied, broadcastId);
    }

    /// <summary>
    /// Startup recovery for cxapi rows leased ('sending') but crashed BEFORE the posting CAS — they
    /// were never POSTed (no external side effect), so it is safe to requeue them. Route-scoped to
    /// 'wapcrm_cxapi' so bridge 'sending' rows (which have no pre-POST marker) are NOT touched. No
    /// counter change (a 'sending' row never decremented queued).
    /// <para>Intentionally tenant-BLIND, like the sibling <see cref="ResetSendingMessagesAsync"/>: this
    /// is a SINGLETON worker startup sweep across the whole outbound queue — there is no tenant context
    /// at boot, and each row carries its own tenant_id (same documented exception as DequeueMessagesAsync).</para>
    /// </summary>
    public virtual async Task<int> ResetStrandedCxapiSendingAsync(CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE outbound_messages SET status = 'queued'
            WHERE send_route = 'wapcrm_cxapi' AND status = 'sending'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows > 0)
            _logger.SystemWarn($"[cxapi-send] reset {rows} stranded cxapi 'sending' rows to 'queued' on startup");
        return rows;
    }

    /// <summary>
    /// Startup recovery for cxapi rows that crashed mid-POST ('posting'): a POST may have reached the
    /// server, so they are resolved to 'ambiguous' (manual/ops, never auto-retried). Stale-TTL guarded
    /// (last_attempt_at older than <paramref name="stalePostingMinutes"/>) so a genuinely in-flight POST
    /// is never swept (Codex P0-2). ONE atomic CTE: messages -> 'ambiguous' + per-broadcast ambiguous++ /
    /// queued-- so a mid-sweep crash cannot leave terminal messages with non-terminal counters
    /// (Codex P1-2). Returns the distinct affected broadcast ids so the caller can complete them.
    /// <para>Intentionally tenant-BLIND (singleton worker startup, no tenant context — same documented
    /// exception as <see cref="ResetSendingMessagesAsync"/> / DequeueMessagesAsync); each row carries its
    /// own tenant_id and the per-broadcast counter update is keyed by the message's own broadcast_id.</para>
    /// </summary>
    public virtual async Task<List<Guid>> SweepStrandedPostingAsync(int stalePostingMinutes, CancellationToken ct = default)
    {
        const string sql = @"
            WITH swept AS (
                UPDATE outbound_messages
                SET status = 'ambiguous',
                    provider_error_message = COALESCE(provider_error_message, '[INV-OB-065] stranded posting recovered to ambiguous on startup'),
                    failed_reason = COALESCE(failed_reason, '[INV-OB-065] stranded posting recovered to ambiguous on startup'),
                    last_attempt_at = NOW()
                WHERE send_route = 'wapcrm_cxapi'
                  AND status = 'posting'
                  AND last_attempt_at < NOW() - make_interval(mins => @mins)
                RETURNING id, broadcast_id, tenant_id
            ), grp AS (
                SELECT broadcast_id, tenant_id, COUNT(*) AS cnt
                FROM swept
                WHERE broadcast_id IS NOT NULL
                GROUP BY broadcast_id, tenant_id
            ), bc AS (
                UPDATE outbound_broadcasts b
                SET ambiguous = b.ambiguous + grp.cnt,
                    queued = GREATEST(b.queued - grp.cnt, 0)
                FROM grp WHERE b.id = grp.broadcast_id AND b.tenant_id = grp.tenant_id
                RETURNING b.id
            )
            SELECT DISTINCT broadcast_id FROM swept WHERE broadcast_id IS NOT NULL";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("mins", stalePostingMinutes);

        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetGuid(0));

        if (ids.Count > 0)
            _logger.SystemWarn($"[cxapi-send] swept stranded 'posting' rows to 'ambiguous' across {ids.Count} broadcast(s) on startup");
        return ids;
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
    /// Enqueue a pending opt-out/opt-in sync row. Always inserts — DB-layer
    /// dedup is deliberately absent because a second-granularity unique index
    /// cannot be built on TIMESTAMPTZ in Postgres (date_trunc / extract(epoch)
    /// are STABLE, not IMMUTABLE). Instead, duplicate enqueues from a webhook
    /// retry storm are tolerated: the drain job pushes both rows to INMA and
    /// the second one gets StatusCode='909' (AlreadyOptedOut), which we treat
    /// as a processed success. Net behaviour: no lost opt-outs, a marginally
    /// fuller audit trail, and zero user-visible duplication.
    /// </summary>
    public virtual async Task<long> EnqueueOptOutSyncAsync(
        int tenantId, string phone, int instanceId, string eventType,
        string scope, string? reason, string? source, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO inma_optout_outbox (tenant_id, phone, instance_id, event_type, scope, reason, source)
            VALUES (@tid, @phone, @iid, @et, @scope, @reason, @source)
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
        return id is long l ? l : 0L;
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

    /// <summary>FEAT-DMP: INMA placeholder keys (e.g. ["name","cf1"]). Null = INSE legacy path.</summary>
    public string[]? DynamicFields { get; set; }

    // ---------------------------------------------------------
    // FEAT-PROJELER / cxapi (PR-1, migration 055) — reserved, read-only.
    // Surfaced from the dequeue projection so PR-3 can route on them; NOT
    // consumed by MessageSenderService in PR-1. Defaults mirror the DB column
    // defaults ('mainapp_bridge' / 'plain_text' / 0); the rest are NULL today.
    // ---------------------------------------------------------

    /// <summary>Immutable send route: <c>mainapp_bridge</c> | <c>wapcrm_cxapi</c>. PR-1: always the bridge.</summary>
    public string SendRoute { get; set; } = "mainapp_bridge";

    /// <summary>Send kind: <c>plain_text</c> | <c>wapcrm_template</c>. PR-1: always plain_text.</summary>
    public string MessageKind { get; set; } = "plain_text";

    /// <summary>Target WapCRM instance (PR-3). NULL in PR-1.</summary>
    public int? InstanceId { get; set; }

    /// <summary>Approved-template reference / HSM id (PR-4). NULL in PR-1.</summary>
    public string? TemplateRef { get; set; }

    /// <summary>Resolved approved-template params (raw JSONB text, PR-4). NULL in PR-1.</summary>
    public string? TemplateParams { get; set; }

    /// <summary>Approved-template language (PR-4). NULL in PR-1.</summary>
    public string? TemplateLanguage { get; set; }

    /// <summary>Approved-template header media (raw JSONB text, PR-4). NULL in PR-1.</summary>
    public string? TemplateHeaderMedia { get; set; }

    /// <summary>Provider (WapCRM) status code, e.g. 906/907/909 (PR-3). NULL in PR-1.</summary>
    public string? ProviderStatusCode { get; set; }

    /// <summary>Provider success flag from the cxapi envelope (PR-3). NULL in PR-1.</summary>
    public bool? ProviderStatus { get; set; }

    /// <summary>Provider requestID for ext_id correlation (PR-3). NULL in PR-1.</summary>
    public string? ProviderRequestId { get; set; }

    /// <summary>Provider error message (PR-3). NULL in PR-1.</summary>
    public string? ProviderErrorMessage { get; set; }

    /// <summary>Last send attempt timestamp (PR-3 state machine). NULL in PR-1.</summary>
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>Send attempt counter (PR-3 idempotency). 0 in PR-1.</summary>
    public int AttemptCount { get; set; }
}
