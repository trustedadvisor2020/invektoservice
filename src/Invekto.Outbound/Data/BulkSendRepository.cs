using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Invekto.Outbound.Data;

/// <summary>
/// Persistence for FEAT-OBI Phase 0 bulk-send jobs + immutable recipient snapshot.
/// Kept separate from the 1400-line OutboundRepository for single responsibility.
/// Every query is tenant_id scoped (project multi-tenant isolation rule).
/// </summary>
public class BulkSendRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public BulkSendRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Snapshot of a bulk_send_jobs row for the orchestrator.</summary>
    public sealed class JobRecord
    {
        public long Id { get; init; }
        public int TenantId { get; init; }
        public string CampaignId { get; init; } = "";
        // Content source — EXACTLY ONE is set (chk_bulk_message_source, migration 060):
        //   TemplateId       => CSV/list runs + a project's gallery_template content (template path)
        //   InlineMessageText => a project's free_text content (SS-A inline broadcast path)
        public int? TemplateId { get; init; }
        public string? InlineMessageText { get; init; }
        public string? Lang { get; init; }
        public int HardCap { get; init; }
        public string Status { get; init; } = "";
        public int TotalValid { get; init; }
        public int TotalQueued { get; init; }
        public int TotalSkippedOptout { get; init; }
        public int TotalSkippedConsent { get; init; }
        public bool DispatchError { get; init; }
        public Guid[] BroadcastIds { get; init; } = Array.Empty<Guid>();
        public DateTime CreatedAt { get; init; }
        public DateTime? ConfirmedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        // FEAT-PROJELER SS-C: the parent project (null for CSV/list runs) and that project's Cloud API
        // instance (LEFT JOIN projects in GetJobAsync; null for non-project jobs). ProjectInstanceId is
        // threaded to CreateBroadcastAsync so a project run sends from its own instance (cxapi route only).
        public long? ProjectId { get; init; }
        public int? ProjectInstanceId { get; init; }
        // PR-4 (migration 062): the HSM run snapshot — copied from the project AT PREVIEW TIME so the
        // run is immutable to later project edits. All null for plain/CSV/list jobs. WaTemplateId set
        // <=> this is an HSM run (chk_bulk_message_source 3-way XOR). InstanceId here is the SNAPSHOT
        // instance (vs ProjectInstanceId = live project JOIN, kept for the plain-text project path).
        public int? InstanceId { get; init; }
        public string? TemplateKind { get; init; }
        public string? WaTemplateId { get; init; }
        public string? ParamMappingJson { get; init; }
        public string? TemplateLanguage { get; init; }
    }

    /// <summary>
    /// PR-4: the validated HSM config <see cref="CreatePreviewJobFromProjectAsync"/> snapshots onto the
    /// job + resolves per-recipient. Built by ProjectsService AFTER the live-catalog validation passed:
    /// every <see cref="TextParamColumns"/> column is already whitelist-checked, and
    /// <see cref="RequiredParamKeys"/> is the live template's required text paramKey set.
    /// </summary>
    public sealed class HsmSnapshotInput
    {
        public required int InstanceId { get; init; }
        public required string WaTemplateId { get; init; }
        public string? TemplateLanguage { get; init; }
        /// <summary>The project's param_mapping JSONB, verbatim (opaque audit copy on the job row).</summary>
        public required string ParamMappingJson { get; init; }
        /// <summary>Text params resolved from a LIST COLUMN: paramKey -> whitelisted list_records column.</summary>
        public required IReadOnlyList<KeyValuePair<string, string>> TextParamColumns { get; init; }
        /// <summary>Text params with a LITERAL constant value (legacy mapping rows): paramKey -> value.</summary>
        public required IReadOnlyList<KeyValuePair<string, string>> TextParamLiterals { get; init; }
        /// <summary>The live template's REQUIRED text paramKeys — a recipient missing any of these is skipped.</summary>
        public required IReadOnlySet<string> RequiredParamKeys { get; init; }
    }

    /// <summary>PR-4: result of a project preview snapshot (plain or HSM).</summary>
    public sealed class ProjectSnapshotResult
    {
        public long JobId { get; init; }
        public int Snapshotted { get; init; }
        /// <summary>HSM only: recipients EXCLUDED because a required mapped param resolved empty/NULL.</summary>
        public int SkippedMissingParams { get; init; }
        /// <summary>HSM only: the capped preview_skipped_params audit JSON persisted on the job.</summary>
        public string? SkippedParamsJson { get; init; }
        public string? ErrorCode { get; init; }
    }

    /// <summary>Aggregated delivery counters across a job's child broadcasts (single query).</summary>
    public sealed class BroadcastAggregate
    {
        public int Sent { get; init; }
        public int Delivered { get; init; }
        public int Read { get; init; }
        public int Failed { get; init; }
        public bool AllTerminal { get; init; }
    }

    /// <summary>
    /// Replace any not-yet-confirmed preview for (tenant, campaign) and write a fresh
    /// snapshot in a single transaction. Returns the new job id.
    /// Caller guarantees the campaign is not already confirmed/sending (checked via GetJobAsync).
    /// </summary>
    public virtual async Task<long> CreatePreviewJobAsync(
        int tenantId, string campaignId, int templateId, string? lang, int hardCap,
        int totalInput, int totalValid, int totalDuplicate, int totalInvalid,
        IReadOnlyList<BroadcastRecipient> validRecipients, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Drop a prior preview_ready job (CASCADE removes its snapshot rows)
        await using (var del = new NpgsqlCommand(
            "DELETE FROM bulk_send_jobs WHERE tenant_id=@tid AND campaign_id=@cid AND status='preview_ready'", conn, tx))
        {
            del.Parameters.AddWithValue("tid", tenantId);
            del.Parameters.AddWithValue("cid", campaignId);
            await del.ExecuteNonQueryAsync(ct);
        }

        long jobId;
        await using (var ins = new NpgsqlCommand(@"
            INSERT INTO bulk_send_jobs
                (tenant_id, campaign_id, source, template_id, lang, hard_cap,
                 total_input, total_valid, total_duplicate, total_invalid, status)
            VALUES (@tid, @cid, 'csv', @tmpl, @lang, @cap,
                 @tin, @tval, @tdup, @tinv, 'preview_ready')
            RETURNING id", conn, tx))
        {
            ins.Parameters.AddWithValue("tid", tenantId);
            ins.Parameters.AddWithValue("cid", campaignId);
            ins.Parameters.AddWithValue("tmpl", templateId);
            ins.Parameters.AddWithValue("lang", (object?)lang ?? DBNull.Value);
            ins.Parameters.AddWithValue("cap", hardCap);
            ins.Parameters.AddWithValue("tin", totalInput);
            ins.Parameters.AddWithValue("tval", totalValid);
            ins.Parameters.AddWithValue("tdup", totalDuplicate);
            ins.Parameters.AddWithValue("tinv", totalInvalid);
            jobId = Convert.ToInt64(await ins.ExecuteScalarAsync(ct));
        }

        // Snapshot rows — single set-based insert via unnest (no N+1).
        // Empty-string vars -> NULL jsonb (phone-only recipients).
        if (validRecipients.Count > 0)
        {
            var phones = new string[validRecipients.Count];
            var vars = new string[validRecipients.Count];
            for (var i = 0; i < validRecipients.Count; i++)
            {
                phones[i] = validRecipients[i].Phone;
                vars[i] = validRecipients[i].Variables is { Count: > 0 }
                    ? JsonSerializer.Serialize(validRecipients[i].Variables)
                    : "";
            }

            await using var rc = new NpgsqlCommand(@"
                INSERT INTO bulk_send_recipients (job_id, tenant_id, normalized_phone, variables_json)
                SELECT @jid, @tid, phone, NULLIF(vars, '')::jsonb
                FROM unnest(@phones::text[], @vars::text[]) AS t(phone, vars)
                ON CONFLICT (job_id, normalized_phone) DO NOTHING", conn, tx);
            rc.Parameters.AddWithValue("jid", jobId);
            rc.Parameters.AddWithValue("tid", tenantId);
            rc.Parameters.AddWithValue("phones", phones);
            rc.Parameters.AddWithValue("vars", vars);
            await rc.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return jobId;
    }

    /// <summary>
    /// FEAT-OBI Phase 1A: build a preview snapshot directly from a contact list's sendable records,
    /// in ONE transaction (immutable snapshot identical to the CSV path). The list row is locked
    /// FOR UPDATE and re-validated (ready + not deleted + sendable count within cap) INSIDE this
    /// transaction — so a concurrent import (which also locks the row) cannot grow the list between
    /// the caller's advisory check and the snapshot, and an over-cap list is REJECTED rather than
    /// silently truncated. Returns (jobId, snapshotted count, errorCode). On errorCode != null the
    /// transaction is rolled back and (0,0) is returned. The caller guarantees the campaign is not
    /// already confirmed (checked via GetJobAsync).
    /// </summary>
    public virtual async Task<(long jobId, int snapshotted, string? errorCode)> CreatePreviewJobFromListAsync(
        int tenantId, string campaignId, int templateId, string? lang, int hardCap,
        long listId, CancellationToken ct = default)
    {
        try
        {
        // Connection-open + begin are inside the try so connection/transient failures also map to INV-OB-053.
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Lock the list row and re-validate under the lock (serializes against concurrent import,
        // which takes the same FOR UPDATE lock). Authoritative recount — never trust a stale counter.
        await using (var lk = new NpgsqlCommand(
            "SELECT status FROM data_lists WHERE tenant_id=@tid AND id=@lid AND deleted_at IS NULL FOR UPDATE", conn, tx))
        {
            lk.Parameters.AddWithValue("tid", tenantId);
            lk.Parameters.AddWithValue("lid", listId);
            var status = await lk.ExecuteScalarAsync(ct) as string;
            if (status == null)
            {
                await tx.RollbackAsync(ct);
                return (0, 0, ErrorCodes.ContactListNotFound);
            }
            if (status != "ready")
            {
                await tx.RollbackAsync(ct);
                return (0, 0, ErrorCodes.ContactListNotReady);
            }
        }

        int sendableNow;
        await using (var cnt = new NpgsqlCommand(
            "SELECT COUNT(*) FROM list_records WHERE tenant_id=@tid AND list_id=@lid AND sendable=TRUE AND normalized_phone IS NOT NULL",
            conn, tx))
        {
            cnt.Parameters.AddWithValue("tid", tenantId);
            cnt.Parameters.AddWithValue("lid", listId);
            sendableNow = Convert.ToInt32(await cnt.ExecuteScalarAsync(ct));
        }
        if (sendableNow == 0 || sendableNow > hardCap)
        {
            await tx.RollbackAsync(ct);
            return (0, 0, ErrorCodes.ContactListNoSendable);
        }

        await using (var del = new NpgsqlCommand(
            "DELETE FROM bulk_send_jobs WHERE tenant_id=@tid AND campaign_id=@cid AND status='preview_ready'", conn, tx))
        {
            del.Parameters.AddWithValue("tid", tenantId);
            del.Parameters.AddWithValue("cid", campaignId);
            await del.ExecuteNonQueryAsync(ct);
        }

        long jobId;
        await using (var ins = new NpgsqlCommand(@"
            INSERT INTO bulk_send_jobs
                (tenant_id, campaign_id, source, template_id, lang, hard_cap, status)
            VALUES (@tid, @cid, 'list', @tmpl, @lang, @cap, 'preview_ready')
            RETURNING id", conn, tx))
        {
            ins.Parameters.AddWithValue("tid", tenantId);
            ins.Parameters.AddWithValue("cid", campaignId);
            ins.Parameters.AddWithValue("tmpl", templateId);
            ins.Parameters.AddWithValue("lang", (object?)lang ?? DBNull.Value);
            ins.Parameters.AddWithValue("cap", hardCap);
            jobId = Convert.ToInt64(await ins.ExecuteScalarAsync(ct));
        }

        // Snapshot sendable recipients (immutable copy; list edits after this cannot change the send).
        // No per-recipient variables — list sends use INMA dynamic placeholders resolved server-side.
        int snapshotted;
        await using (var rc = new NpgsqlCommand(@"
            INSERT INTO bulk_send_recipients (job_id, tenant_id, normalized_phone, variables_json)
            SELECT @jid, @tid, normalized_phone, NULL
            FROM list_records
            WHERE tenant_id=@tid AND list_id=@lid AND sendable=TRUE AND normalized_phone IS NOT NULL
            ORDER BY id
            LIMIT @cap
            ON CONFLICT (job_id, normalized_phone) DO NOTHING", conn, tx))
        {
            rc.Parameters.AddWithValue("jid", jobId);
            rc.Parameters.AddWithValue("tid", tenantId);
            rc.Parameters.AddWithValue("lid", listId);
            rc.Parameters.AddWithValue("cap", hardCap);
            snapshotted = await rc.ExecuteNonQueryAsync(ct);
        }

        await using (var upd = new NpgsqlCommand(
            "UPDATE bulk_send_jobs SET total_input=@n, total_valid=@n WHERE id=@jid AND tenant_id=@tid", conn, tx))
        {
            upd.Parameters.AddWithValue("n", snapshotted);
            upd.Parameters.AddWithValue("jid", jobId);
            upd.Parameters.AddWithValue("tid", tenantId);
            await upd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return (jobId, snapshotted, null);
        }
        catch (NpgsqlException ex)
        {
            // Snapshot DB failure (server error OR connection/transient — NpgsqlException base):
            // the tx is rolled back by `await using` disposal; surface the typed code so the endpoint
            // returns 503 INV-OB-053 rather than an opaque 500. Nothing partial was committed.
            _logger.SystemError(
                $"preview-from-list DB failure: tenant={tenantId}, campaign={campaignId}, list={listId}, msg={ex.Message}");
            return (0, 0, ErrorCodes.ContactListDbError);
        }
    }

    /// <summary>
    /// FEAT-PROJELER send-exec SS-C: build a preview snapshot for a PROJECT run in ONE transaction.
    /// The audience is the UNION of the project's target data_lists, DEDUPED by normalized_phone ACROSS
    /// lists (a phone in two target lists is snapshotted once) — the snapshot is immutable like the CSV/
    /// list paths. All target lists are locked FOR UPDATE and re-validated (not deleted + status='ready')
    /// inside the tx, so a concurrent import cannot change the audience between check and snapshot, and a
    /// distinct-sendable count of 0 or over the cap is REJECTED rather than truncated. Content is the
    /// project's chosen carrier: EXACTLY ONE of <paramref name="templateId"/> (gallery_template, template
    /// path) or <paramref name="inlineText"/> (free_text, inline path) — the chk_bulk_message_source CHECK
    /// (migration 060) enforces this at the DB. source='project', project_id set. Returns (jobId,
    /// snapshotted, errorCode); on errorCode != null the tx is rolled back and (0,0) is returned. The
    /// caller (ProjectsService) guarantees the campaign is not already confirmed and ids are deduped/positive.
    /// </summary>
    /// <summary>
    /// PR-4: maps every SPA list-column key to its REAL list_records SQL column. The values below are
    /// the ONLY identifiers ever interpolated into the HSM snapshot SQL — paramKey strings always
    /// travel as bind parameters, never as SQL text (no injection surface; Codex CQ5).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> HsmListColumns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "name", ["surname"] = "surname", ["email"] = "email",
            ["field1"] = "field1", ["field2"] = "field2", ["field3"] = "field3",
            ["field4"] = "field4", ["field5"] = "field5",
            ["tags"] = "tags", ["note"] = "note"
        };

    /// <summary>True when the SPA column key maps to a real list_records column (HSM mapping validation).</summary>
    public static bool IsHsmListColumn(string column) => HsmListColumns.ContainsKey(column);

    public virtual async Task<ProjectSnapshotResult> CreatePreviewJobFromProjectAsync(
        int tenantId, string campaignId, long projectId, int? templateId, string? inlineText,
        long[] listIds, int hardCap, CancellationToken ct = default, HsmSnapshotInput? hsm = null)
    {
        if (listIds.Length == 0) return new ProjectSnapshotResult { ErrorCode = ErrorCodes.ProjectNoTargets };
        // Caller contract: EXACTLY ONE content source (plain template/inline XOR hsm) — the DB
        // chk_bulk_message_source (3-way, migration 062) is the belt; this is the suspenders.
        if (hsm != null && (templateId.HasValue || inlineText != null))
            throw new ArgumentException("An HSM snapshot cannot also carry plain content.", nameof(hsm));
        try
        {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Lock + re-validate EVERY target list under the tx (serializes against concurrent import, which
        // takes the same FOR UPDATE lock). A list archived/re-importing since the project was saved must
        // not silently drop out — found != requested => some list missing/deleted; any non-ready => block.
        var found = 0;
        var anyNotReady = false;
        await using (var lk = new NpgsqlCommand(
            "SELECT status FROM data_lists WHERE tenant_id=@tid AND id = ANY(@lids) AND deleted_at IS NULL FOR UPDATE", conn, tx))
        {
            lk.Parameters.AddWithValue("tid", tenantId);
            lk.Parameters.AddWithValue("lids", listIds);
            await using var r = await lk.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                found++;
                if (r.GetString(0) != "ready") anyNotReady = true;
            }
        }
        if (found != listIds.Length)
        {
            await tx.RollbackAsync(ct);
            return new ProjectSnapshotResult { ErrorCode = ErrorCodes.ContactListNotFound };
        }
        if (anyNotReady)
        {
            await tx.RollbackAsync(ct);
            return new ProjectSnapshotResult { ErrorCode = ErrorCodes.ContactListNotReady };
        }

        // Authoritative distinct-sendable count across all target lists (never trust a denormalized
        // counter; a phone shared by two lists counts once). 0 or over-cap => reject.
        int distinctSendable;
        await using (var cnt = new NpgsqlCommand(
            "SELECT COUNT(DISTINCT normalized_phone) FROM list_records WHERE tenant_id=@tid AND list_id = ANY(@lids) AND sendable=TRUE AND normalized_phone IS NOT NULL",
            conn, tx))
        {
            cnt.Parameters.AddWithValue("tid", tenantId);
            cnt.Parameters.AddWithValue("lids", listIds);
            distinctSendable = Convert.ToInt32(await cnt.ExecuteScalarAsync(ct));
        }
        if (distinctSendable == 0 || distinctSendable > hardCap)
        {
            await tx.RollbackAsync(ct);
            return new ProjectSnapshotResult { ErrorCode = ErrorCodes.ContactListNoSendable };
        }

        await using (var del = new NpgsqlCommand(
            "DELETE FROM bulk_send_jobs WHERE tenant_id=@tid AND campaign_id=@cid AND status='preview_ready'", conn, tx))
        {
            del.Parameters.AddWithValue("tid", tenantId);
            del.Parameters.AddWithValue("cid", campaignId);
            await del.ExecuteNonQueryAsync(ct);
        }

        long jobId;
        await using (var ins = new NpgsqlCommand(@"
            INSERT INTO bulk_send_jobs
                (tenant_id, campaign_id, source, template_id, inline_message_text, lang, hard_cap, project_id, status,
                 instance_id, template_kind, wa_template_id, param_mapping, template_language)
            VALUES (@tid, @cid, 'project', @tmpl, @inline, NULL, @cap, @pid, 'preview_ready',
                    @hinst, @hkind, @hwatid, @hmap, @hlang)
            RETURNING id", conn, tx))
        {
            ins.Parameters.AddWithValue("tid", tenantId);
            ins.Parameters.AddWithValue("cid", campaignId);
            // EXACTLY ONE of template_id / inline_message_text / wa_template_id is non-null
            // (chk_bulk_message_source, 3-way since migration 062). Typed NULLs so every bind
            // has a known type even when null. The HSM columns are the IMMUTABLE run snapshot
            // (template_language display-only — never on the wire).
            ins.Parameters.Add(new NpgsqlParameter("tmpl", NpgsqlDbType.Integer) { Value = (object?)templateId ?? DBNull.Value });
            ins.Parameters.Add(new NpgsqlParameter("inline", NpgsqlDbType.Text) { Value = (object?)inlineText ?? DBNull.Value });
            ins.Parameters.AddWithValue("cap", hardCap);
            ins.Parameters.AddWithValue("pid", projectId);
            ins.Parameters.Add(new NpgsqlParameter("hinst", NpgsqlDbType.Integer) { Value = (object?)hsm?.InstanceId ?? DBNull.Value });
            ins.Parameters.Add(new NpgsqlParameter("hkind", NpgsqlDbType.Varchar) { Value = hsm != null ? "wapcrm_template" : DBNull.Value });
            ins.Parameters.Add(new NpgsqlParameter("hwatid", NpgsqlDbType.Varchar) { Value = (object?)hsm?.WaTemplateId ?? DBNull.Value });
            ins.Parameters.Add(new NpgsqlParameter("hmap", NpgsqlDbType.Jsonb) { Value = (object?)hsm?.ParamMappingJson ?? DBNull.Value });
            ins.Parameters.Add(new NpgsqlParameter("hlang", NpgsqlDbType.Varchar) { Value = (object?)hsm?.TemplateLanguage ?? DBNull.Value });
            jobId = Convert.ToInt64(await ins.ExecuteScalarAsync(ct));
        }

        int snapshotted;
        var skippedMissing = 0;
        string? skippedJson = null;
        if (hsm == null)
        {
            // Plain path (UNCHANGED): snapshot DISTINCT sendable phones across ALL target lists
            // (cross-list dedup). No per-recipient variables — plain list/project sends use INMA
            // dynamic placeholders resolved server-side. The ON CONFLICT is a belt over the
            // DISTINCT subquery. ORDER BY + LIMIT bound the snapshot to the cap.
            await using var rc = new NpgsqlCommand(@"
                INSERT INTO bulk_send_recipients (job_id, tenant_id, normalized_phone, variables_json)
                SELECT @jid, @tid, d.normalized_phone, NULL
                FROM (
                    SELECT DISTINCT normalized_phone
                    FROM list_records
                    WHERE tenant_id=@tid AND list_id = ANY(@lids) AND sendable=TRUE AND normalized_phone IS NOT NULL
                    ORDER BY normalized_phone
                    LIMIT @cap
                ) d
                ON CONFLICT (job_id, normalized_phone) DO NOTHING", conn, tx);
            rc.Parameters.AddWithValue("jid", jobId);
            rc.Parameters.AddWithValue("tid", tenantId);
            rc.Parameters.AddWithValue("lids", listIds);
            rc.Parameters.AddWithValue("cap", hardCap);
            snapshotted = await rc.ExecuteNonQueryAsync(ct);

            await using var upd = new NpgsqlCommand(
                "UPDATE bulk_send_jobs SET total_input=@n, total_valid=@n WHERE id=@jid AND tenant_id=@tid", conn, tx);
            upd.Parameters.AddWithValue("n", snapshotted);
            upd.Parameters.AddWithValue("jid", jobId);
            upd.Parameters.AddWithValue("tid", tenantId);
            await upd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            (snapshotted, skippedMissing, skippedJson) =
                await SnapshotHsmRecipientsAsync(conn, tx, tenantId, projectId, jobId, listIds, hardCap, distinctSendable, hsm, ct);
        }

        await tx.CommitAsync(ct);
        return new ProjectSnapshotResult
        {
            JobId = jobId, Snapshotted = snapshotted,
            SkippedMissingParams = skippedMissing, SkippedParamsJson = skippedJson
        };
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError(
                $"preview-from-project DB failure: tenant={tenantId}, campaign={campaignId}, project={projectId}, msg={ex.Message}");
            return new ProjectSnapshotResult { ErrorCode = ErrorCodes.ContactListDbError };
        }
    }

    /// <summary>
    /// PR-4: HSM recipient snapshot — set-based, inside the caller's transaction.
    /// Per phone the SOURCE ROW is deterministic: the FIRST target list in the project's order
    /// (project_targets.sort_order, then data_list_id), then the lowest record id (Q decision
    /// 2026-06-10). variables_json = the FLAT resolved param dict {paramKey: value} (same shape
    /// the CSV path stores, so GetRecipientsAsync deserializes both identically); empty values are
    /// stripped (the wire omits an empty optional param). A recipient whose REQUIRED mapped column
    /// resolves empty/NULL is EXCLUDED from the snapshot and persisted into the capped
    /// preview_skipped_params audit (count + by_param + sample&lt;=100 with masked phones).
    /// SQL-injection note (Codex CQ5): paramKeys/literals bind as parameters; the ONLY interpolated
    /// identifiers are list_records column names resolved through the fixed <see cref="HsmListColumns"/>
    /// whitelist (caller pre-validated via <see cref="IsHsmListColumn"/>; re-checked here fail-loud).
    /// </summary>
    private static async Task<(int snapshotted, int skipped, string? skippedJson)> SnapshotHsmRecipientsAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long projectId, long jobId,
        long[] listIds, int hardCap, int distinctSendable, HsmSnapshotInput hsm, CancellationToken ct)
    {
        // ── params-object SQL fragments (keys + literals parameterized, columns whitelisted) ──
        var kvFragments = new List<string>();
        var binds = new List<NpgsqlParameter>();
        var bindIx = 0;
        foreach (var (paramKey, column) in hsm.TextParamColumns)
        {
            if (!HsmListColumns.TryGetValue(column, out var sqlCol))
                throw new ArgumentException($"HSM param column '{column}' is not a list column.", nameof(hsm));
            var kp = $"hk{bindIx++}";
            binds.Add(new NpgsqlParameter(kp, NpgsqlDbType.Text) { Value = paramKey });
            kvFragments.Add($"@{kp}, NULLIF(btrim(lr.{sqlCol}), '')");
        }
        foreach (var (paramKey, literal) in hsm.TextParamLiterals)
        {
            var kp = $"hk{bindIx++}";
            var vp = $"hv{bindIx++}";
            binds.Add(new NpgsqlParameter(kp, NpgsqlDbType.Text) { Value = paramKey });
            binds.Add(new NpgsqlParameter(vp, NpgsqlDbType.Text) { Value = literal });
            kvFragments.Add($"@{kp}, NULLIF(btrim(@{vp}), '')");
        }
        var paramsExpr = kvFragments.Count > 0
            ? $"jsonb_strip_nulls(jsonb_build_object({string.Join(", ", kvFragments)}))"
            : "'{}'::jsonb";

        // ── eligibility predicate + missing-param cases (REQUIRED column-mapped params only;
        //    a required LITERAL param was already validated non-empty by the service) ──
        var requiredPredicates = new List<string>();
        var missingCases = new List<string>();
        var reqIx = 0;
        foreach (var (paramKey, column) in hsm.TextParamColumns)
        {
            if (!hsm.RequiredParamKeys.Contains(paramKey)) continue;
            var sqlCol = HsmListColumns[column]; // presence proven above
            var rp = $"hr{reqIx++}";
            binds.Add(new NpgsqlParameter(rp, NpgsqlDbType.Text) { Value = paramKey });
            requiredPredicates.Add($"COALESCE(btrim(lr.{sqlCol}), '') <> ''");
            missingCases.Add($"CASE WHEN COALESCE(btrim(lr.{sqlCol}), '') = '' THEN @{rp} END");
        }
        var eligibleExpr = requiredPredicates.Count > 0 ? string.Join(" AND ", requiredPredicates) : "TRUE";

        const string sourceJoin = @"
                FROM list_records lr
                JOIN project_targets pt
                  ON pt.tenant_id = lr.tenant_id AND pt.data_list_id = lr.list_id AND pt.project_id = @pid
                WHERE lr.tenant_id = @tid AND lr.list_id = ANY(@lids)
                  AND lr.sendable = TRUE AND lr.normalized_phone IS NOT NULL
                ORDER BY lr.normalized_phone, pt.sort_order, pt.data_list_id, lr.id";

        int snapshotted;
        await using (var rc = new NpgsqlCommand($@"
            INSERT INTO bulk_send_recipients (job_id, tenant_id, normalized_phone, variables_json)
            SELECT @jid, @tid, d.normalized_phone, d.params
            FROM (
                SELECT DISTINCT ON (lr.normalized_phone)
                       lr.normalized_phone,
                       {paramsExpr} AS params,
                       ({eligibleExpr}) AS eligible
                {sourceJoin}
            ) d
            WHERE d.eligible
            ORDER BY d.normalized_phone
            LIMIT @cap
            ON CONFLICT (job_id, normalized_phone) DO NOTHING", conn, tx))
        {
            rc.Parameters.AddWithValue("jid", jobId);
            rc.Parameters.AddWithValue("tid", tenantId);
            rc.Parameters.AddWithValue("pid", projectId);
            rc.Parameters.AddWithValue("lids", listIds);
            rc.Parameters.AddWithValue("cap", hardCap);
            foreach (var p in binds) rc.Parameters.Add(p.Clone());
            snapshotted = await rc.ExecuteNonQueryAsync(ct);
        }

        // ── capped skip audit (only computable when a required column-mapped param exists) ──
        var skipped = 0;
        string skippedJson;
        if (missingCases.Count > 0)
        {
            await using var aud = new NpgsqlCommand($@"
                WITH src AS (
                    SELECT DISTINCT ON (lr.normalized_phone)
                           lr.normalized_phone, lr.list_id, lr.id AS record_id,
                           ARRAY(SELECT x FROM unnest(ARRAY[{string.Join(", ", missingCases)}]) AS x WHERE x IS NOT NULL) AS missing
                    {sourceJoin}
                ),
                skipped AS (SELECT * FROM src WHERE cardinality(missing) > 0)
                SELECT (SELECT COUNT(*) FROM skipped)::int,
                       jsonb_build_object(
                           'count', (SELECT COUNT(*) FROM skipped),
                           'by_param', COALESCE((SELECT jsonb_object_agg(k, n)
                                                 FROM (SELECT unnest(missing) AS k, COUNT(*) AS n FROM skipped GROUP BY 1) bp), '{{}}'::jsonb),
                           'sample', COALESCE((SELECT jsonb_agg(s)
                                               FROM (SELECT jsonb_build_object(
                                                         'masked_phone', left(normalized_phone, 5) || '****' || right(normalized_phone, 2),
                                                         'list_id', list_id,
                                                         'record_id', record_id,
                                                         'missing', to_jsonb(missing)) AS s
                                                     FROM skipped ORDER BY normalized_phone LIMIT 100) sm), '[]'::jsonb))::text", conn, tx);
            aud.Parameters.AddWithValue("tid", tenantId);
            aud.Parameters.AddWithValue("pid", projectId);
            aud.Parameters.AddWithValue("lids", listIds);
            foreach (var p in binds) aud.Parameters.Add(p.Clone());
            await using var r = await aud.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                skipped = r.GetInt32(0);
                skippedJson = r.GetString(1);
            }
            else
            {
                skippedJson = "{\"count\":0,\"by_param\":{},\"sample\":[]}";
            }
        }
        else
        {
            skippedJson = "{\"count\":0,\"by_param\":{},\"sample\":[]}";
        }

        await using (var upd = new NpgsqlCommand(@"
            UPDATE bulk_send_jobs
            SET total_input=@inp, total_valid=@val, preview_skipped_params=@skp
            WHERE id=@jid AND tenant_id=@tid", conn, tx))
        {
            upd.Parameters.AddWithValue("inp", distinctSendable);
            upd.Parameters.AddWithValue("val", snapshotted);
            upd.Parameters.Add(new NpgsqlParameter("skp", NpgsqlDbType.Jsonb) { Value = skippedJson });
            upd.Parameters.AddWithValue("jid", jobId);
            upd.Parameters.AddWithValue("tid", tenantId);
            await upd.ExecuteNonQueryAsync(ct);
        }

        return (snapshotted, skipped, skippedJson);
    }

    /// <summary>First N snapshot phones for a job (operator eyeball sample in the preview response).</summary>
    public virtual async Task<List<string>> GetRecipientPhonesSampleAsync(
        int tenantId, long jobId, int limit, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT normalized_phone FROM bulk_send_recipients
            WHERE tenant_id=@tid AND job_id=@jid ORDER BY id LIMIT @lim";
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("jid", jobId);
        cmd.Parameters.AddWithValue("lim", limit);
        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(reader.GetString(0));
        return list;
    }

    public virtual async Task<JobRecord?> GetJobAsync(
        int tenantId, string campaignId, CancellationToken ct = default)
    {
        // LEFT JOIN projects so a project run also carries its own Cloud API instance_id (null for
        // CSV/list jobs whose project_id is null -> unchanged behaviour). inline_message_text +
        // project_id (migration 057/060) are read so ConfirmAsync can take the inline / project-instance
        // branch. The join is keyed on the composite (tenant_id, id) so it stays tenant-scoped.
        // PR-4: the HSM snapshot columns (j.instance_id..j.template_language, migration 062) are read
        // alongside the live-project instance — an HSM run dispatches from its SNAPSHOT (immutable),
        // while the plain-text project path keeps the live p.instance_id behaviour (PR-3a, unchanged).
        const string sql = @"
            SELECT j.id, j.tenant_id, j.campaign_id, j.template_id, j.inline_message_text, j.lang,
                   j.hard_cap, j.status, j.total_valid, j.total_queued, j.total_skipped_optout,
                   j.total_skipped_consent, j.dispatch_error, j.broadcast_ids, j.created_at,
                   j.confirmed_at, j.completed_at, j.project_id, p.instance_id,
                   j.instance_id, j.template_kind, j.wa_template_id, j.param_mapping, j.template_language
            FROM bulk_send_jobs j
            LEFT JOIN projects p ON p.tenant_id = j.tenant_id AND p.id = j.project_id
            WHERE j.tenant_id=@tid AND j.campaign_id=@cid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("cid", campaignId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new JobRecord
        {
            Id = reader.GetInt64(0),
            TenantId = reader.GetInt32(1),
            CampaignId = reader.GetString(2),
            TemplateId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            InlineMessageText = reader.IsDBNull(4) ? null : reader.GetString(4),
            Lang = reader.IsDBNull(5) ? null : reader.GetString(5),
            HardCap = reader.GetInt32(6),
            Status = reader.GetString(7),
            TotalValid = reader.GetInt32(8),
            TotalQueued = reader.GetInt32(9),
            TotalSkippedOptout = reader.GetInt32(10),
            TotalSkippedConsent = reader.GetInt32(11),
            DispatchError = reader.GetBoolean(12),
            BroadcastIds = reader.IsDBNull(13) ? Array.Empty<Guid>() : (Guid[])reader.GetValue(13),
            CreatedAt = reader.GetDateTime(14),
            ConfirmedAt = reader.IsDBNull(15) ? null : reader.GetDateTime(15),
            CompletedAt = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
            ProjectId = reader.IsDBNull(17) ? null : reader.GetInt64(17),
            ProjectInstanceId = reader.IsDBNull(18) ? null : reader.GetInt32(18),
            InstanceId = reader.IsDBNull(19) ? null : reader.GetInt32(19),
            TemplateKind = reader.IsDBNull(20) ? null : reader.GetString(20),
            WaTemplateId = reader.IsDBNull(21) ? null : reader.GetString(21),
            ParamMappingJson = reader.IsDBNull(22) ? null : reader.GetString(22),
            TemplateLanguage = reader.IsDBNull(23) ? null : reader.GetString(23)
        };
    }

    public virtual async Task<List<BroadcastRecipient>> GetRecipientsAsync(
        int tenantId, long jobId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT normalized_phone, variables_json
            FROM bulk_send_recipients
            WHERE tenant_id=@tid AND job_id=@jid
            ORDER BY id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("jid", jobId);

        var list = new List<BroadcastRecipient>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            Dictionary<string, string>? vars = null;
            if (!reader.IsDBNull(1))
            {
                var json = reader.GetString(1);
                if (!string.IsNullOrWhiteSpace(json))
                    vars = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }
            list.Add(new BroadcastRecipient { Phone = reader.GetString(0), Variables = vars });
        }
        return list;
    }

    /// <summary>
    /// Atomically move a preview_ready job into 'confirming' so concurrent confirms
    /// cannot both dispatch (idempotency). Returns true if THIS call won the transition.
    /// </summary>
    public virtual async Task<bool> TryClaimForConfirmAsync(
        int tenantId, long jobId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE bulk_send_jobs
            SET status='confirming', confirmed_at=NOW()
            WHERE id=@jid AND tenant_id=@tid AND status='preview_ready'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("jid", jobId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    /// <summary>Record dispatch result: child broadcast ids + aggregated send counters + error flag.</summary>
    public virtual async Task FinalizeDispatchAsync(
        int tenantId, long jobId, IReadOnlyList<Guid> broadcastIds, int totalQueued,
        int totalSkippedOptout, int totalSkippedConsent, bool dispatchError,
        string status, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE bulk_send_jobs
            SET broadcast_ids=@bids, total_queued=@q,
                total_skipped_optout=@so, total_skipped_consent=@sc,
                dispatch_error=@derr, status=@st
            WHERE id=@jid AND tenant_id=@tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("jid", jobId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("bids", broadcastIds.ToArray());
        cmd.Parameters.AddWithValue("q", totalQueued);
        cmd.Parameters.AddWithValue("so", totalSkippedOptout);
        cmd.Parameters.AddWithValue("sc", totalSkippedConsent);
        cmd.Parameters.AddWithValue("derr", dispatchError);
        cmd.Parameters.AddWithValue("st", status);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public virtual async Task UpdateStatusAsync(
        int tenantId, long jobId, string status, bool setCompleted, CancellationToken ct = default)
    {
        var sql = setCompleted
            ? "UPDATE bulk_send_jobs SET status=@st, completed_at=NOW() WHERE id=@jid AND tenant_id=@tid"
            : "UPDATE bulk_send_jobs SET status=@st WHERE id=@jid AND tenant_id=@tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("jid", jobId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("st", status);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Single-query aggregate of a job's child broadcasts (replaces per-broadcast N+1).
    /// AllTerminal is true only when every expected broadcast exists and is completed/failed.
    /// </summary>
    public virtual async Task<BroadcastAggregate> AggregateBroadcastStatusAsync(
        int tenantId, Guid[] broadcastIds, CancellationToken ct = default)
    {
        if (broadcastIds.Length == 0)
            return new BroadcastAggregate { AllTerminal = false };

        // PostgreSQL SUM(integer) -> bigint; cast back to int4 so GetInt32 is safe.
        // Counters are bounded (<=1000/broadcast, few chunks) so ::int cannot overflow here.
        const string sql = @"
            SELECT COALESCE(SUM(sent),0)::int      AS sent,
                   COALESCE(SUM(delivered),0)::int AS delivered,
                   COALESCE(SUM(read),0)::int      AS read,
                   COALESCE(SUM(failed),0)::int    AS failed,
                   COUNT(*)                                                         AS found,
                   COUNT(*) FILTER (WHERE status NOT IN ('completed','failed'))     AS non_terminal
            FROM outbound_broadcasts
            WHERE tenant_id=@tid AND id = ANY(@ids)";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("ids", broadcastIds);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new BroadcastAggregate { AllTerminal = false };

        var found = reader.GetInt64(4);
        var nonTerminal = reader.GetInt64(5);
        return new BroadcastAggregate
        {
            Sent = reader.GetInt32(0),
            Delivered = reader.GetInt32(1),
            Read = reader.GetInt32(2),
            Failed = reader.GetInt32(3),
            AllTerminal = found == broadcastIds.Length && nonTerminal == 0
        };
    }
}
