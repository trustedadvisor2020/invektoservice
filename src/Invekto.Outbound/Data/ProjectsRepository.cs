using System.Globalization;
using System.Text.Json;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs.Outbound;
using Npgsql;
using NpgsqlTypes;

namespace Invekto.Outbound.Data;

/// <summary>
/// Persistence for FEAT-PROJELER (PKT-14) slice S2 projects CRUD (projects +
/// project_targets). Every query is tenant_id scoped (multi-tenant isolation rule); the
/// composite FKs (tenant_id, *_id) make a cross-tenant child row impossible. Create/Update
/// run in a single transaction so a project, its target set, AND the returned detail are
/// read/committed atomically (no fragile post-commit reload). Soft-delete-as-archive: a
/// project is never hard-deleted (the bulk_send_jobs.project_id FK is ON DELETE RESTRICT) —
/// it is archived instead.
///
/// Schema (migration 057 / canonical arch/db/outbound.sql, shipped in slice S1):
///   projects(uq_projects_tenant_id UNIQUE(tenant_id,id);
///            uq_projects_tenant_name_active UNIQUE(tenant_id, lower(btrim(name))) WHERE archived_at IS NULL;
///            chk_project_status; GRANT ALL)
///   project_targets(fk_project_targets_data_list (tenant_id,data_list_id)->data_lists(tenant_id,id);
///            fk_project_targets_project (tenant_id,project_id)->projects(tenant_id,id) ON DELETE CASCADE;
///            uq_project_target_project_list UNIQUE(project_id,data_list_id))
///   data_lists has the (tenant_id,id) composite unique + soft-delete deleted_at (migration 052).
/// Register as singleton.
/// </summary>
public class ProjectsRepository
{
    private readonly PostgresConnectionFactory _db;

    public ProjectsRepository(PostgresConnectionFactory db)
    {
        _db = db;
    }

    /// <summary>
    /// Validated + normalized send config to persist. The SERVICE owns validation/normalization
    /// (kind consistency, plain_text clears template fields, param_mapping serialized to JSON text);
    /// the repository just writes these values verbatim. param_mapping is pre-serialized so the repo
    /// stays free of JSON concerns. Used by Create (always applied) and Update (applied only when the
    /// caller sets setSendConfig=true).
    /// </summary>
    public sealed class ProjectSendConfigInput
    {
        public int? InstanceId { get; init; }
        public string? TemplateKind { get; init; }
        public string? WaTemplateId { get; init; }
        public string? TemplateLanguage { get; init; }
        /// <summary>Serialized JSONB text (object/array) or null. null => stored param_mapping is SQL NULL.</summary>
        public string? ParamMappingJson { get; init; }
        // plain_text content (migration 059). The service normalizes these so exactly one carrier is set
        // for the chosen content_mode (gallery_template => OutboundTemplateId; free_text => PlainTextBody);
        // the repo writes them verbatim. content_mode NULL => both carriers NULL.
        public string? ContentMode { get; init; }
        public int? OutboundTemplateId { get; init; }
        public string? PlainTextBody { get; init; }
    }

    /// <summary>Outcome of a Create/Update write (exactly one of the failure flags, else success+Detail).</summary>
    public sealed class ProjectWriteResult
    {
        public bool Found { get; init; } = true;     // false => project id not found for this tenant (update)
        public bool NameConflict { get; init; }       // true => active name collided (normalized partial-unique)
        public bool InvalidTargets { get; init; }     // true => a target list id is missing/not-owned/deleted
        public long ProjectId { get; init; }
        public ProjectDetail? Detail { get; init; }   // set on success (read in-tx, reflects the commit)
    }

    // ------------------------------------------------------------------
    // Read
    // ------------------------------------------------------------------
    // includeArchived (GR-9): TRUE also returns archived (soft-deleted) rows for the dashboard's
    // Arşivli filter; FALSE keeps the original active-only shape (parameterized, tenant-scoped).
    public virtual async Task<List<ProjectSummary>> ListAsync(
        int tenantId, CancellationToken ct = default, bool includeArchived = false)
    {
        const string sql = @"
            SELECT p.id, p.name, p.description, p.status,
                   (SELECT COUNT(*) FROM project_targets pt
                      WHERE pt.tenant_id = p.tenant_id AND pt.project_id = p.id)::int AS target_count,
                   p.run_count, p.total_targets, p.sent_count, p.delivered_count,
                   p.read_count, p.failed_count, p.ambiguous_count,
                   p.created_at, p.updated_at, p.started_at, p.completed_at,
                   p.instance_id, p.template_kind, p.wa_template_id, p.template_language, p.param_mapping,
                   p.content_mode, p.outbound_template_id, p.plain_text_body,
                   p.cancelled_count
            FROM projects p
            WHERE p.tenant_id = @tid AND (@includeArchived OR p.archived_at IS NULL)
            ORDER BY p.created_at DESC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("includeArchived", includeArchived);

        var list = new List<ProjectSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(MapSummary(reader));
        return list;
    }

    /// <summary>Project + its ordered target lists. Null when no active project with this id for the tenant.</summary>
    public virtual async Task<ProjectDetail?> GetAsync(int tenantId, long projectId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        return await LoadDetailAsync(conn, null, tenantId, projectId, ct);
    }

    /// <summary>
    /// Load a project + its ordered targets on the given connection and OPTIONAL transaction. Write
    /// paths pass their own tx so they read their own uncommitted rows and return the result before
    /// commit — removing the fragile "commit then reload on a fresh connection" failure mode where a
    /// reload error would be reported as a rolled-back INV-OB-071 after a committed write.
    /// Null when no active project with this id exists for the tenant.
    /// </summary>
    private static async Task<ProjectDetail?> LoadDetailAsync(
        NpgsqlConnection conn, NpgsqlTransaction? tx, int tenantId, long projectId, CancellationToken ct)
    {
        ProjectSummary summary;
        await using (var cmd = new NpgsqlCommand(@"
            SELECT p.id, p.name, p.description, p.status,
                   (SELECT COUNT(*) FROM project_targets pt
                      WHERE pt.tenant_id = p.tenant_id AND pt.project_id = p.id)::int AS target_count,
                   p.run_count, p.total_targets, p.sent_count, p.delivered_count,
                   p.read_count, p.failed_count, p.ambiguous_count,
                   p.created_at, p.updated_at, p.started_at, p.completed_at,
                   p.instance_id, p.template_kind, p.wa_template_id, p.template_language, p.param_mapping,
                   p.content_mode, p.outbound_template_id, p.plain_text_body,
                   p.cancelled_count
            FROM projects p
            WHERE p.tenant_id = @tid AND p.id = @pid AND p.archived_at IS NULL", conn, tx))
        {
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("pid", projectId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            summary = MapSummary(reader);
        }

        var targets = new List<ProjectTargetDto>();
        await using (var cmd = new NpgsqlCommand(@"
            SELECT pt.data_list_id, dl.name, dl.total_records, dl.sendable_count, pt.sort_order
            FROM project_targets pt
            JOIN data_lists dl ON dl.tenant_id = pt.tenant_id AND dl.id = pt.data_list_id
            WHERE pt.tenant_id = @tid AND pt.project_id = @pid
            ORDER BY pt.sort_order, pt.data_list_id", conn, tx))
        {
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("pid", projectId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                targets.Add(new ProjectTargetDto
                {
                    DataListId = reader.GetInt64(0),
                    ListName = reader.GetString(1),
                    TotalRecords = reader.GetInt32(2),
                    SendableCount = reader.GetInt32(3),
                    SortOrder = reader.GetInt32(4)
                });
        }

        return new ProjectDetail { Project = summary, Targets = targets };
    }

    // ------------------------------------------------------------------
    // Write (transactional — project + target set + returned detail are atomic)
    // ------------------------------------------------------------------
    /// <summary>
    /// Create a 'draft' project and its initial target set in one transaction, returning the committed
    /// detail. Returns NameConflict on a normalized active-name collision, InvalidTargets when any
    /// target id is not an active list for this tenant (enforced by a validating INSERT that joins
    /// data_lists WHERE deleted_at IS NULL, so a concurrent soft-delete cannot slip a deleted list in),
    /// else success with ProjectId + Detail. ids must be deduped + positive (caller-normalized).
    /// </summary>
    public virtual async Task<ProjectWriteResult> CreateAsync(
        int tenantId, int createdBy, string name, string? description, long[] targetListIds,
        ProjectSendConfigInput? config, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Validate + FOR SHARE row-lock the targets before any write: rejects bad ids early AND blocks a
        // concurrent soft-delete of a validated list until this tx commits (closes the TOCTOU race).
        if (targetListIds.Length > 0 && !await AllTargetsValidAndLockedAsync(conn, tx, tenantId, targetListIds, ct))
        {
            await tx.RollbackAsync(ct);
            return new ProjectWriteResult { InvalidTargets = true };
        }

        long projectId;
        await using (var ins = new NpgsqlCommand(@"
            INSERT INTO projects (tenant_id, name, description, status, created_by,
                                  instance_id, template_kind, wa_template_id, template_language, param_mapping,
                                  content_mode, outbound_template_id, plain_text_body)
            VALUES (@tid, @name, @desc, 'draft', @by,
                    @inst, @kind, @tmpl, @lang, @pm,
                    @cmode, @otid, @body)
            RETURNING id", conn, tx))
        {
            ins.Parameters.AddWithValue("tid", tenantId);
            ins.Parameters.AddWithValue("name", name);
            ins.Parameters.AddWithValue("desc", (object?)description ?? DBNull.Value);
            ins.Parameters.AddWithValue("by", createdBy);
            AddSendConfigParams(ins, config);
            try
            {
                projectId = Convert.ToInt64(await ins.ExecuteScalarAsync(ct));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await tx.RollbackAsync(ct);
                return new ProjectWriteResult { NameConflict = true };
            }
        }

        if (targetListIds.Length > 0)
        {
            // The FOR SHARE lock above already guarantees the targets stay active through commit; this
            // validating insert (JOIN deleted_at IS NULL) + count check is the atomic backstop.
            var inserted = await InsertTargetsAsync(conn, tx, tenantId, projectId, targetListIds, ct);
            if (inserted != targetListIds.Length)
            {
                await tx.RollbackAsync(ct);
                return new ProjectWriteResult { InvalidTargets = true };
            }
        }

        var detail = await LoadDetailAsync(conn, tx, tenantId, projectId, ct);
        await tx.CommitAsync(ct);
        return new ProjectWriteResult { ProjectId = projectId, Detail = detail };
    }

    /// <summary>
    /// Partial-update a project's metadata and optionally REPLACE its target set in one transaction,
    /// returning the committed detail. name/description use COALESCE (null = unchanged). targetListIds
    /// == null leaves targets as-is; a non-null array (incl. empty) replaces the whole set. The
    /// replacement uses the same validating INSERT + count check as create (race-safe). Archived
    /// projects are not editable. ids must be deduped + positive (caller-normalized).
    /// </summary>
    public virtual async Task<ProjectWriteResult> UpdateAsync(
        int tenantId, long projectId, string? name, string? description, long[]? targetListIds,
        ProjectSendConfigInput? config, bool setSendConfig, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Validate + FOR SHARE row-lock the targets before any write: rejects bad ids early AND blocks a
        // concurrent soft-delete of a validated list until this tx commits (closes the TOCTOU race).
        if (targetListIds is { Length: > 0 } && !await AllTargetsValidAndLockedAsync(conn, tx, tenantId, targetListIds, ct))
        {
            await tx.RollbackAsync(ct);
            return new ProjectWriteResult { InvalidTargets = true };
        }

        int rc;
        await using (var upd = new NpgsqlCommand(@"
            UPDATE projects SET
                name = COALESCE(@name, name),
                description = COALESCE(@desc, description),
                instance_id       = CASE WHEN @setcfg THEN @inst ELSE instance_id END,
                template_kind     = CASE WHEN @setcfg THEN @kind ELSE template_kind END,
                wa_template_id    = CASE WHEN @setcfg THEN @tmpl ELSE wa_template_id END,
                template_language = CASE WHEN @setcfg THEN @lang ELSE template_language END,
                param_mapping     = CASE WHEN @setcfg THEN @pm   ELSE param_mapping END,
                content_mode         = CASE WHEN @setcfg THEN @cmode ELSE content_mode END,
                outbound_template_id = CASE WHEN @setcfg THEN @otid  ELSE outbound_template_id END,
                plain_text_body      = CASE WHEN @setcfg THEN @body  ELSE plain_text_body END,
                updated_at = NOW()
            WHERE tenant_id = @tid AND id = @pid AND archived_at IS NULL", conn, tx))
        {
            upd.Parameters.AddWithValue("tid", tenantId);
            upd.Parameters.AddWithValue("pid", projectId);
            upd.Parameters.AddWithValue("name", (object?)name ?? DBNull.Value);
            upd.Parameters.AddWithValue("desc", (object?)description ?? DBNull.Value);
            upd.Parameters.Add(new NpgsqlParameter("setcfg", NpgsqlDbType.Boolean) { Value = setSendConfig });
            // When setSendConfig is false the CASE selects the existing column, so the bound values are
            // unused; pass null then. When true the (service-normalized) config is written verbatim.
            AddSendConfigParams(upd, setSendConfig ? config : null);
            try
            {
                rc = await upd.ExecuteNonQueryAsync(ct);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await tx.RollbackAsync(ct);
                return new ProjectWriteResult { NameConflict = true };
            }
        }

        if (rc == 0)
        {
            await tx.RollbackAsync(ct);
            return new ProjectWriteResult { Found = false };
        }

        if (targetListIds != null)
        {
            await using (var del = new NpgsqlCommand(
                "DELETE FROM project_targets WHERE tenant_id = @tid AND project_id = @pid", conn, tx))
            {
                del.Parameters.AddWithValue("tid", tenantId);
                del.Parameters.AddWithValue("pid", projectId);
                await del.ExecuteNonQueryAsync(ct);
            }
            if (targetListIds.Length > 0)
            {
                // Validating insert + count check (race-safe), same as create.
                var inserted = await InsertTargetsAsync(conn, tx, tenantId, projectId, targetListIds, ct);
                if (inserted != targetListIds.Length)
                {
                    await tx.RollbackAsync(ct);
                    return new ProjectWriteResult { InvalidTargets = true };
                }
            }
        }

        var detail = await LoadDetailAsync(conn, tx, tenantId, projectId, ct);
        await tx.CommitAsync(ct);
        return new ProjectWriteResult { ProjectId = projectId, Detail = detail };
    }

    /// <summary>
    /// Soft-delete = archive (status='archived' + archived_at=NOW()). Frees the name (the active-name
    /// partial-unique index is scoped to archived_at IS NULL) and preserves the row + any run history.
    /// Returns false when no active project with this id exists for the tenant.
    /// </summary>
    public virtual async Task<bool> ArchiveAsync(int tenantId, long projectId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE projects SET status = 'archived', archived_at = NOW(), updated_at = NOW()
            WHERE tenant_id = @tid AND id = @pid AND archived_at IS NULL";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", projectId);
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    // ------------------------------------------------------------------
    // Run lifecycle + roll-up counters (FEAT-PROJELER send-exec SS-C)
    // ------------------------------------------------------------------
    /// <summary>
    /// Mark a project 'running' when a run is dispatched: sets status='running', started_at=NOW(), clears
    /// completed_at (a fresh run reopens the lifecycle) and bumps updated_at. Idempotent (a second confirm
    /// re-marks running harmlessly). Only an active (non-archived) project is touched. Returns true when
    /// exactly one (active) row was updated; false means the project was archived/removed mid-confirm — the
    /// caller logs that rather than silently dropping it. The denormalized counters are NOT written here —
    /// <see cref="RecomputeRollupAsync"/> refreshes them live on status read.
    /// </summary>
    public virtual async Task<bool> SetRunningAsync(int tenantId, long projectId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE projects
            SET status = 'running', started_at = NOW(), completed_at = NULL, updated_at = NOW()
            WHERE tenant_id = @tid AND id = @pid AND archived_at IS NULL";
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", projectId);
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    /// <summary>
    /// Recompute a project's denormalized roll-up counters + lifecycle status LIVE from its runs, and
    /// persist them (idempotent UPDATE). Aggregates the project's bulk_send_jobs -> broadcast_ids ->
    /// outbound_broadcasts (tenant-scoped via project ownership; no cross-tenant read):
    ///   run_count       = number of the project's bulk_send_jobs
    ///   total_targets   = SUM(broadcast.total_recipients)
    ///   sent/delivered/read/failed/ambiguous = SUM of the matching broadcast counters
    /// Status derives from the live queue: 'running' while any of the project's broadcasts still has
    /// queued > 0, else 'completed' (run_count > 0) — completed_at is stamped once when it first drains and
    /// cleared again if a new run re-queues. Only an active project in a run-managed state (draft/running/
    /// completed) is transitioned; archived/paused/cancelled are left as-is (forward-compatible with SS-D).
    /// </summary>
    public virtual async Task RecomputeRollupAsync(int tenantId, long projectId, CancellationToken ct = default)
    {
        const string sql = @"
            WITH bcasts AS (
                SELECT DISTINCT b.id, b.sent, b.delivered, b.read, b.failed, b.ambiguous, b.cancelled, b.queued, b.total_recipients
                FROM bulk_send_jobs j
                JOIN outbound_broadcasts b
                  ON b.tenant_id = j.tenant_id AND b.id = ANY(j.broadcast_ids)
                WHERE j.tenant_id = @tid AND j.project_id = @pid
            ),
            agg AS (
                SELECT COALESCE(SUM(sent),0)::int             AS sent,
                       COALESCE(SUM(delivered),0)::int        AS delivered,
                       COALESCE(SUM(read),0)::int             AS read,
                       COALESCE(SUM(failed),0)::int           AS failed,
                       COALESCE(SUM(ambiguous),0)::int        AS ambiguous,
                       COALESCE(SUM(cancelled),0)::int        AS cancelled,
                       COALESCE(SUM(queued),0)::int           AS queued,
                       COALESCE(SUM(total_recipients),0)::int AS total_targets
                FROM bcasts
            ),
            runs AS (
                SELECT COUNT(*)::int AS run_count
                FROM bulk_send_jobs
                WHERE tenant_id = @tid AND project_id = @pid
            )
            UPDATE projects p SET
                run_count       = runs.run_count,
                total_targets   = agg.total_targets,
                sent_count      = agg.sent,
                delivered_count = agg.delivered,
                read_count      = agg.read,
                failed_count    = agg.failed,
                ambiguous_count = agg.ambiguous,
                cancelled_count = agg.cancelled,
                status = CASE
                    WHEN p.status NOT IN ('draft','running','completed') THEN p.status
                    WHEN runs.run_count = 0 THEN p.status
                    WHEN agg.queued > 0 THEN 'running'
                    ELSE 'completed'
                END,
                completed_at = CASE
                    -- paused/cancelled/archived own their own completed_at (set by the SS-D lifecycle op);
                    -- do NOT let queue depth clear it (in-flight 'sending'/'posting' still sit in queued).
                    WHEN p.status NOT IN ('draft','running','completed') THEN p.completed_at
                    WHEN runs.run_count > 0 AND agg.queued = 0
                         AND p.status IN ('running','completed') AND p.completed_at IS NULL THEN NOW()
                    WHEN agg.queued > 0 THEN NULL
                    ELSE p.completed_at
                END,
                updated_at = NOW()
            FROM agg, runs
            WHERE p.tenant_id = @tid AND p.id = @pid AND p.archived_at IS NULL";
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", projectId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ------------------------------------------------------------------
    // Run lifecycle — pause / resume / cancel (FEAT-PROJELER send-exec SS-D)
    // ------------------------------------------------------------------
    /// <summary>
    /// Outcome of a pause/resume/cancel lifecycle op. The status transition is an ATOMIC claim
    /// (UPDATE ... WHERE status=&lt;expected&gt; RETURNING) so a 0-row result is never a silent no-op:
    /// <see cref="Found"/>=false -&gt; no active project for this tenant (404); <see cref="StateConflict"/>=true
    /// -&gt; the project exists but was not in the required state (<see cref="CurrentStatus"/> tells the
    /// operator why, 409). On success <see cref="AffectedMessages"/> is the number of messages flipped.
    /// </summary>
    public readonly record struct RunLifecycleResult(bool Found, bool StateConflict, string? CurrentStatus, int AffectedMessages);

    // Flip a project's messages from one PRE-TERMINAL status to another, scoped to the project's runs'
    // broadcasts. Used by pause (queued->paused) and resume (paused->queued): both source+target are
    // pre-terminal so the broadcast.queued counter is unchanged (no counter math). Tenant-scoped on the
    // message row too (defense-in-depth; the broadcast set already comes from this tenant's jobs).
    private const string FlipProjectMessagesSql = @"
        UPDATE outbound_messages m
        SET status = @to
        WHERE m.tenant_id = @tid AND m.status = @from
          AND m.broadcast_id IN (
              SELECT bid
              FROM bulk_send_jobs j
              CROSS JOIN LATERAL unnest(j.broadcast_ids) AS bid
              WHERE j.tenant_id = @tid AND j.project_id = @pid
          )";

    /// <summary>
    /// PAUSE a running project: atomically claim status 'running'->'paused', flip the project's still-'queued'
    /// messages to 'paused' (the dequeue worker only claims 'queued', so they halt; in-flight 'sending'/'posting'
    /// rows are left to finish — already handed off, not recallable), and mark the active run 'paused'. All in
    /// one transaction. Returns a typed result (never a silent no-op): not-found, wrong-state, or success+count.
    /// </summary>
    public virtual async Task<RunLifecycleResult> PauseRunAsync(int tenantId, long projectId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var claimed = await ClaimProjectStatusAsync(conn, tx, tenantId, projectId,
            "UPDATE projects SET status = 'paused', updated_at = NOW() WHERE tenant_id = @tid AND id = @pid AND archived_at IS NULL AND status = 'running'", ct);
        if (claimed == 0)
        {
            var miss = await ClassifyClaimMissAsync(conn, tx, tenantId, projectId, ct);
            await tx.RollbackAsync(ct);
            return miss;
        }

        var paused = await FlipMessagesAsync(conn, tx, tenantId, projectId, from: "queued", to: "paused", ct);
        // Reflect on the active run row. Reachable non-terminal states when project='running' are 'sending'
        // (normal) and 'confirming' (the sub-second window of an in-flight confirm). Best-effort: a confirm
        // still mid-dispatch may finalize the job AFTER this — project status (claimed above) is authoritative.
        await SetRunStatusAsync(conn, tx, tenantId, projectId, from: new[] { "confirming", "sending" }, to: "paused", stampCompleted: false, ct);

        await tx.CommitAsync(ct);
        return new RunLifecycleResult(Found: true, StateConflict: false, CurrentStatus: "paused", AffectedMessages: paused);
    }

    /// <summary>
    /// RESUME a paused project: atomically claim status 'paused'->'running' (clear completed_at), flip the
    /// project's 'paused' messages back to 'queued' (the worker picks them up again, in created_at order, reusing
    /// the original snapshot), and mark the run 'sending'. One transaction; typed result.
    /// </summary>
    public virtual async Task<RunLifecycleResult> ResumeRunAsync(int tenantId, long projectId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var claimed = await ClaimProjectStatusAsync(conn, tx, tenantId, projectId,
            "UPDATE projects SET status = 'running', completed_at = NULL, updated_at = NOW() WHERE tenant_id = @tid AND id = @pid AND archived_at IS NULL AND status = 'paused'", ct);
        if (claimed == 0)
        {
            var miss = await ClassifyClaimMissAsync(conn, tx, tenantId, projectId, ct);
            await tx.RollbackAsync(ct);
            return miss;
        }

        var resumed = await FlipMessagesAsync(conn, tx, tenantId, projectId, from: "paused", to: "queued", ct);
        await SetRunStatusAsync(conn, tx, tenantId, projectId, from: new[] { "paused" }, to: "sending", stampCompleted: false, ct);

        await tx.CommitAsync(ct);
        return new RunLifecycleResult(Found: true, StateConflict: false, CurrentStatus: "running", AffectedMessages: resumed);
    }

    /// <summary>
    /// CANCEL a running/paused project: atomically claim status -> 'cancelled' (stamp completed_at), terminalize
    /// every remaining 'queued'+'paused' message to 'cancelled' with a per-broadcast cancelled++/queued-- (ONE
    /// atomic CTE, the SweepStrandedPostingAsync pattern, so a mid-write crash can't leave a terminal message
    /// with a stale counter), mark the run 'cancelled', and complete any now-drained broadcast (a SEPARATE
    /// command — a sibling data-modifying CTE would still see the pre-cancel snapshot). In-flight 'sending'/
    /// 'posting' rows are NOT recalled (already at the provider); they complete naturally. One transaction.
    /// </summary>
    public virtual async Task<RunLifecycleResult> CancelRunAsync(int tenantId, long projectId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var claimed = await ClaimProjectStatusAsync(conn, tx, tenantId, projectId,
            "UPDATE projects SET status = 'cancelled', completed_at = NOW(), updated_at = NOW() WHERE tenant_id = @tid AND id = @pid AND archived_at IS NULL AND status IN ('running','paused')", ct);
        if (claimed == 0)
        {
            var miss = await ClassifyClaimMissAsync(conn, tx, tenantId, projectId, ct);
            await tx.RollbackAsync(ct);
            return miss;
        }

        int cancelledCount;
        Guid[] affected;
        await using (var term = new NpgsqlCommand(@"
            WITH proj_bcasts AS (
                SELECT DISTINCT bid
                FROM bulk_send_jobs j
                CROSS JOIN LATERAL unnest(j.broadcast_ids) AS bid
                WHERE j.tenant_id = @tid AND j.project_id = @pid
            ),
            cancelled AS (
                UPDATE outbound_messages m
                SET status = 'cancelled',
                    failed_reason = COALESCE(m.failed_reason, '[INV-OB-079] run cancelled by operator')
                WHERE m.tenant_id = @tid AND m.status IN ('queued','paused')
                  AND m.broadcast_id IN (SELECT bid FROM proj_bcasts)
                RETURNING m.broadcast_id
            ),
            grp AS (
                SELECT broadcast_id, COUNT(*) AS cnt
                FROM cancelled WHERE broadcast_id IS NOT NULL
                GROUP BY broadcast_id
            ),
            bc AS (
                UPDATE outbound_broadcasts b
                SET cancelled = b.cancelled + grp.cnt,
                    queued = GREATEST(b.queued - grp.cnt, 0)
                FROM grp WHERE b.id = grp.broadcast_id AND b.tenant_id = @tid
                RETURNING b.id
            )
            SELECT (SELECT COUNT(*) FROM cancelled)::int AS cnt,
                   ARRAY(SELECT id FROM bc) AS bids", conn, tx))
        {
            term.Parameters.AddWithValue("tid", tenantId);
            term.Parameters.AddWithValue("pid", projectId);
            await using var reader = await term.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            cancelledCount = reader.GetInt32(0);
            // ARRAY(SELECT ...) yields an empty array (never NULL) for 0 rows; the IsDBNull guard is
            // belt-and-braces so a no-op cancel (nothing queued/paused) reads a clean empty list.
            affected = reader.IsDBNull(1) ? Array.Empty<Guid>() : (Guid[])reader.GetValue(1);
        }

        // Reflect on the active run row. Reachable non-terminal states when project is running/paused are
        // 'sending', 'confirming' (in-flight confirm window) and 'paused'. Best-effort (see PauseRunAsync note).
        await SetRunStatusAsync(conn, tx, tenantId, projectId, from: new[] { "confirming", "sending", "paused" }, to: "cancelled", stampCompleted: true, ct);

        // Complete any broadcast the cancel just drained (no remaining non-terminal rows). Separate command so
        // it observes the CTE's committed-to-tx changes; broadcasts with in-flight rows stay as-is and complete
        // naturally when the worker finishes them.
        if (affected.Length > 0)
        {
            await using var complete = new NpgsqlCommand(@"
                UPDATE outbound_broadcasts b
                SET status = 'completed', completed_at = NOW()
                WHERE b.id = ANY(@bids) AND b.tenant_id = @tid AND b.status NOT IN ('completed','failed')
                  AND NOT EXISTS (
                      SELECT 1 FROM outbound_messages m
                      WHERE m.broadcast_id = b.id AND m.tenant_id = @tid
                        AND m.status IN ('queued','sending','posting','paused'))", conn, tx);
            complete.Parameters.AddWithValue("tid", tenantId);
            complete.Parameters.AddWithValue("bids", affected);
            await complete.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return new RunLifecycleResult(Found: true, StateConflict: false, CurrentStatus: "cancelled", AffectedMessages: cancelledCount);
    }

    /// <summary>Run the atomic project-status claim UPDATE on the tx; returns rows affected (0 or 1).</summary>
    private static async Task<int> ClaimProjectStatusAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long projectId, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", projectId);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Flip the project's messages from -&gt; to (both pre-terminal) and return the count flipped.</summary>
    private static async Task<int> FlipMessagesAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long projectId, string from, string to, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(FlipProjectMessagesSql, conn, tx);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", projectId);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("to", to);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Reflect the lifecycle change on the active run row(s) (bulk_send_jobs), tenant + project scoped.</summary>
    private static async Task SetRunStatusAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long projectId, string[] from, string to, bool stampCompleted, CancellationToken ct)
    {
        // Fully parameterized (no SQL string interpolation): completed_at is stamped via a CASE on the
        // @stamp flag rather than concatenating a fragment into the statement.
        const string sql = @"
            UPDATE bulk_send_jobs
            SET status = @to,
                completed_at = CASE WHEN @stamp THEN NOW() ELSE completed_at END
            WHERE tenant_id = @tid AND project_id = @pid AND status = ANY(@from)";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", projectId);
        cmd.Parameters.AddWithValue("to", to);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("stamp", stampCompleted);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Classify a 0-row status claim: a follow-up tenant-scoped existence read distinguishes "no active project"
    /// (Found=false -&gt; 404) from "wrong state" (StateConflict + the current status -&gt; 409). Runs inside the
    /// caller's tx (before rollback) so it reads a consistent view.
    /// </summary>
    private static async Task<RunLifecycleResult> ClassifyClaimMissAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long projectId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT status FROM projects WHERE tenant_id = @tid AND id = @pid AND archived_at IS NULL", conn, tx);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", projectId);
        var status = await cmd.ExecuteScalarAsync(ct) as string;
        return status == null
            ? new RunLifecycleResult(Found: false, StateConflict: false, CurrentStatus: null, AffectedMessages: 0)
            : new RunLifecycleResult(Found: true, StateConflict: true, CurrentStatus: status, AffectedMessages: 0);
    }

    // ------------------------------------------------------------------
    // Send-config authorization
    // ------------------------------------------------------------------
    /// <summary>
    /// Authoritative server-side guard for a project's send channel: true ONLY if <paramref name="instanceId"/>
    /// is a WhatsApp Cloud API line (instance_type = 1) owned by THIS tenant, per the shared
    /// <c>tenant_instances</c> cache (populated by Backend's /settings/instances; the same shared Postgres
    /// Outbound already reads for wapcrm settings — sanctioned shared-read, not a cross-service call).
    /// Closes the gap where the SPA filters Cloud-API channels client-side only: a direct API caller could
    /// otherwise persist a foreign-tenant or non-Cloud instance_id into projects.instance_id (Codex CQ5/CQ9).
    /// tenant_instances stores the WapCRM instanceID as its string form, so the int is compared as text.
    /// </summary>
    public virtual async Task<bool> IsCloudApiInstanceOwnedAsync(int tenantId, int instanceId, CancellationToken ct = default)
    {
        // STRICT WABA gate (migration 063): cxapi gives instance_type=1 to BOTH WABA and QR-Code
        // lines, so type alone is not a Cloud-API proof — connection_type='WABA' is. NULL does NOT
        // authorize (a stale pre-063 cache row must be backfilled/refreshed, never trusted): this
        // guard is the authoritative server-side check for direct API callers, so unknown = reject.
        const string sql = @"
            SELECT EXISTS(
                SELECT 1 FROM tenant_instances
                WHERE tenant_id = @tid AND instance_id = @inst AND instance_type = 1
                  AND UPPER(TRIM(connection_type)) = 'WABA')";
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("inst", instanceId.ToString(CultureInfo.InvariantCulture));
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    /// <summary>
    /// Authoritative server-side guard for a project's gallery-template content (migration 059): true ONLY
    /// if <paramref name="templateId"/> is an ACTIVE Şablon Galerisi template owned by THIS tenant. Mirrors
    /// the bulk-send template gate (BroadcastOrchestrator.GetTemplateByIdAsync only accepts is_active rows)
    /// so a project can never persist a foreign-tenant or deactivated template id as its content (the SPA
    /// filters the picker client-side; a direct API caller could otherwise submit any id). outbound_templates
    /// is the same shared Postgres Outbound already owns — not a cross-service call.
    /// </summary>
    public virtual async Task<bool> IsOutboundTemplateOwnedActiveAsync(int tenantId, int templateId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT EXISTS(
                SELECT 1 FROM outbound_templates
                WHERE tenant_id = @tid AND id = @oid AND is_active = TRUE)";
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("oid", templateId);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    // ------------------------------------------------------------------
    // Target helpers (set-based)
    // ------------------------------------------------------------------
    /// <summary>Validate AND row-lock the targets: true only if EVERY id is an active (non-deleted)
    /// data_list owned by this tenant. The <c>FOR SHARE</c> lock holds each matched data_lists row for
    /// the lifetime of the caller's transaction, so a concurrent soft-delete (a non-key UPDATE of
    /// deleted_at — FOR KEY SHARE would NOT block it, FOR SHARE does) BLOCKS until this project tx
    /// commits/rolls back. That closes the TOCTOU window: a list validated here cannot be soft-deleted
    /// before the target row commits, so the committed project never references a deleted list. ids must
    /// be deduped by the caller (each id is a PK, so it matches at most once).</summary>
    private static async Task<bool> AllTargetsValidAndLockedAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) return true;
        await using var cmd = new NpgsqlCommand(
            "SELECT id FROM data_lists WHERE tenant_id = @tid AND id = ANY(@ids) AND deleted_at IS NULL FOR SHARE",
            conn, tx);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("ids", ids);
        var found = 0;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) found++;
        return found == ids.Length;
    }

    /// <summary>
    /// Validating insert: inserts a target row ONLY for an id that is an active (non-deleted) data_list
    /// owned by the tenant (JOIN data_lists ... deleted_at IS NULL), with sort_order = input position.
    /// Returns the number of rows inserted; the caller compares it to the requested count to detect any
    /// invalid/deleted/cross-tenant id ATOMICALLY (no FK-violation reliance, no TOCTOU race). ids must
    /// be deduped + positive and the target set empty for this project (create / post-DELETE update),
    /// so every requested-but-active id inserts exactly one row.
    /// </summary>
    private static async Task<int> InsertTargetsAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long projectId, long[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) return 0;
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO project_targets (tenant_id, project_id, data_list_id, sort_order)
            SELECT @tid, @pid, t.data_list_id, (t.ord - 1)::int
            FROM unnest(@ids::bigint[]) WITH ORDINALITY AS t(data_list_id, ord)
            JOIN data_lists dl ON dl.tenant_id = @tid AND dl.id = t.data_list_id AND dl.deleted_at IS NULL", conn, tx);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", projectId);
        cmd.Parameters.AddWithValue("ids", ids);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Bind the 5 send-config parameters with EXPLICIT Npgsql types so a NULL value still has a known
    /// type — required because they appear in CASE branches (Postgres cannot infer the type of an
    /// untyped NULL parameter there). config == null => every parameter is SQL NULL. param_mapping is
    /// bound as jsonb (its pre-serialized JSON text, or NULL).
    /// </summary>
    private static void AddSendConfigParams(NpgsqlCommand cmd, ProjectSendConfigInput? config)
    {
        cmd.Parameters.Add(new NpgsqlParameter("inst", NpgsqlDbType.Integer) { Value = (object?)config?.InstanceId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("kind", NpgsqlDbType.Text) { Value = (object?)config?.TemplateKind ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("tmpl", NpgsqlDbType.Text) { Value = (object?)config?.WaTemplateId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("lang", NpgsqlDbType.Text) { Value = (object?)config?.TemplateLanguage ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("pm", NpgsqlDbType.Jsonb) { Value = (object?)config?.ParamMappingJson ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("cmode", NpgsqlDbType.Text) { Value = (object?)config?.ContentMode ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("otid", NpgsqlDbType.Integer) { Value = (object?)config?.OutboundTemplateId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("body", NpgsqlDbType.Text) { Value = (object?)config?.PlainTextBody ?? DBNull.Value });
    }

    private static ProjectSummary MapSummary(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Name = reader.GetString(1),
        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
        Status = reader.GetString(3),
        TargetCount = reader.GetInt32(4),
        RunCount = reader.GetInt32(5),
        TotalTargets = reader.GetInt32(6),
        SentCount = reader.GetInt32(7),
        DeliveredCount = reader.GetInt32(8),
        ReadCount = reader.GetInt32(9),
        FailedCount = reader.GetInt32(10),
        AmbiguousCount = reader.GetInt32(11),
        CreatedAt = reader.GetDateTime(12),
        UpdatedAt = reader.GetDateTime(13),
        StartedAt = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
        CompletedAt = reader.IsDBNull(15) ? null : reader.GetDateTime(15),
        InstanceId = reader.IsDBNull(16) ? null : reader.GetInt32(16),
        TemplateKind = reader.IsDBNull(17) ? null : reader.GetString(17),
        WaTemplateId = reader.IsDBNull(18) ? null : reader.GetString(18),
        TemplateLanguage = reader.IsDBNull(19) ? null : reader.GetString(19),
        // param_mapping is jsonb (Npgsql returns it as text); re-parse to a JsonElement so the API
        // re-emits it as a JSON object, not a JSON-encoded string. Safe to deserialize as JsonElement.
        ParamMapping = reader.IsDBNull(20) ? null : JsonSerializer.Deserialize<JsonElement>(reader.GetString(20)),
        ContentMode = reader.IsDBNull(21) ? null : reader.GetString(21),
        OutboundTemplateId = reader.IsDBNull(22) ? null : reader.GetInt32(22),
        PlainTextBody = reader.IsDBNull(23) ? null : reader.GetString(23),
        CancelledCount = reader.GetInt32(24)
    };

    // ------------------------------------------------------------------
    // FEAT-PROJELER — Rapor (delivery report) — 2026-06-12
    // ------------------------------------------------------------------

    /// <summary>
    /// List a project's runs (bulk_send_jobs) newest-first with their LIVE partition counters (summed
    /// over each run's broadcasts). Drives the report drawer's run dropdown. Tenant + project scoped.
    /// </summary>
    public virtual async Task<List<ProjectRunDto>> GetRunsAsync(
        int tenantId, long projectId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT j.campaign_id, j.status, j.created_at,
                   COALESCE(SUM(b.total_recipients),0)::int AS total,
                   COALESCE(SUM(b.queued),0)::int    AS queued,
                   COALESCE(SUM(b.sent),0)::int      AS sent,
                   COALESCE(SUM(b.delivered),0)::int AS delivered,
                   COALESCE(SUM(b.read),0)::int      AS rd,
                   COALESCE(SUM(b.failed),0)::int    AS failed,
                   COALESCE(SUM(b.ambiguous),0)::int AS ambiguous,
                   COALESCE(SUM(b.cancelled),0)::int AS cancelled
            FROM bulk_send_jobs j
            LEFT JOIN outbound_broadcasts b ON b.tenant_id = j.tenant_id AND b.id = ANY(j.broadcast_ids)
            WHERE j.tenant_id = @tid AND j.project_id = @pid
            GROUP BY j.id, j.campaign_id, j.status, j.created_at
            ORDER BY j.created_at DESC, j.id DESC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", projectId);

        var runs = new List<ProjectRunDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            runs.Add(new ProjectRunDto
            {
                CampaignId = reader.GetString(0),
                Status = reader.GetString(1),
                CreatedAt = reader.GetDateTime(2),
                Total = reader.GetInt32(3),
                Queued = reader.GetInt32(4),
                Sent = reader.GetInt32(5),
                Delivered = reader.GetInt32(6),
                Read = reader.GetInt32(7),
                Failed = reader.GetInt32(8),
                Ambiguous = reader.GetInt32(9),
                Cancelled = reader.GetInt32(10)
            });
        }
        return runs;
    }

    /// <summary>
    /// Server-paged per-recipient report for a project, newest message first. Joins the project's runs'
    /// broadcasts to outbound_messages; optional campaign_id + phone-substring filters. The error column
    /// prefers failed_reason then provider_error_message; can_resend is true only for failed/ambiguous.
    /// Returns the page + the full filtered total (drives pagination + the export-all CSV). Tenant-scoped;
    /// fully parameterized (ILIKE arg passed as a value, no string interpolation).
    /// </summary>
    public virtual async Task<ProjectRecipientsPage> GetRecipientsAsync(
        int tenantId, long projectId, string? campaignId, string? search,
        int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 5000) pageSize = 5000; // headroom for the export-all-filtered CSV
        var offset = (page - 1) * pageSize;
        var searchArg = string.IsNullOrWhiteSpace(search) ? null : "%" + search.Trim() + "%";

        // The project's broadcasts (+ each broadcast's run campaign_id), built once and reused for both
        // the count and the page. A broadcast maps to exactly one job, so campaign_id is unambiguous.
        const string cte = @"
            WITH proj AS (
                SELECT DISTINCT bb.bid, j.campaign_id
                FROM bulk_send_jobs j
                CROSS JOIN LATERAL unnest(j.broadcast_ids) AS bb(bid)
                WHERE j.tenant_id = @tid AND j.project_id = @pid
            )";
        // ::text casts give the nullable @campaign/@search parameters an explicit type — without them a
        // DBNull parameter first seen in `@x IS NULL` leaves Postgres unable to infer its type (42P08
        // "could not determine data type of parameter"). The cast is a no-op for a real text value.
        const string filter = @"
            FROM outbound_messages m
            JOIN proj p ON p.bid = m.broadcast_id
            WHERE m.tenant_id = @tid
              AND (@campaign::text IS NULL OR p.campaign_id = @campaign::text)
              AND (@search::text IS NULL OR m.recipient_phone ILIKE @search::text)";

        await using var conn = await _db.OpenConnectionAsync(ct);

        int total;
        await using (var countCmd = new NpgsqlCommand(cte + "\nSELECT COUNT(*)::int" + filter, conn))
        {
            countCmd.Parameters.AddWithValue("tid", tenantId);
            countCmd.Parameters.AddWithValue("pid", projectId);
            countCmd.Parameters.AddWithValue("campaign", (object?)campaignId ?? DBNull.Value);
            countCmd.Parameters.AddWithValue("search", (object?)searchArg ?? DBNull.Value);
            total = (await countCmd.ExecuteScalarAsync(ct)) is int n ? n : 0; // COUNT(*)::int is never null; defensive cast (no null-forgiving)
        }

        var items = new List<ProjectRecipientDto>();
        var pageSql = cte + @"
            SELECT m.id, p.campaign_id, m.recipient_phone, m.status,
                   COALESCE(m.failed_reason, m.provider_error_message) AS error,
                   m.sent_at, m.delivered_at, m.read_at, m.last_attempt_at,
                   (m.status IN ('failed','ambiguous')) AS can_resend" + filter + @"
            ORDER BY m.id DESC
            LIMIT @limit OFFSET @offset";
        await using (var cmd = new NpgsqlCommand(pageSql, conn))
        {
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("pid", projectId);
            cmd.Parameters.AddWithValue("campaign", (object?)campaignId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("search", (object?)searchArg ?? DBNull.Value);
            cmd.Parameters.AddWithValue("limit", pageSize);
            cmd.Parameters.AddWithValue("offset", offset);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new ProjectRecipientDto
                {
                    MessageId = reader.GetInt64(0),
                    CampaignId = reader.GetString(1),
                    Phone = reader.GetString(2),
                    Status = reader.GetString(3),
                    Error = reader.IsDBNull(4) ? null : reader.GetString(4),
                    SentAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    DeliveredAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    ReadAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    LastAttemptAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    CanResend = reader.GetBoolean(9)
                });
            }
        }

        return new ProjectRecipientsPage { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    /// <summary>
    /// FEATURE B (status-pull): the project's wamid-bearing, not-yet-terminal recipients whose live status is
    /// worth re-pulling from cxapi — status IN ('sent','delivered','ambiguous') AND external_message_id IS NOT NULL.
    /// ('read'/'failed'/'cancelled' are terminal-for-display; 'queued'/'sending'/'posting' have no wamid yet.)
    /// Tenant + project scoped via the same broadcasts CTE the recipients page uses; optional campaign filter.
    /// Bounded by <paramref name="cap"/> (newest first) to keep the vendor fan-out finite. Returns
    /// (message_id, external_message_id (wamid), instance_id) tuples.
    /// </summary>
    public virtual async Task<List<(long MessageId, string ExternalMessageId, int? InstanceId)>> GetPendingForStatusPullAsync(
        int tenantId, long projectId, string? campaignId, int cap, CancellationToken ct = default)
    {
        if (cap < 1) cap = 1;
        const string sql = @"
            WITH proj AS (
                SELECT DISTINCT bb.bid, j.campaign_id
                FROM bulk_send_jobs j
                CROSS JOIN LATERAL unnest(j.broadcast_ids) AS bb(bid)
                WHERE j.tenant_id = @tid AND j.project_id = @pid
            )
            SELECT m.id, m.external_message_id, m.instance_id
            FROM outbound_messages m
            JOIN proj p ON p.bid = m.broadcast_id
            WHERE m.tenant_id = @tid
              AND m.external_message_id IS NOT NULL
              AND m.status IN ('sent','delivered','ambiguous')
              AND (@campaign::text IS NULL OR p.campaign_id = @campaign::text)
            ORDER BY m.id DESC
            LIMIT @cap";

        var rows = new List<(long, string, int?)>();
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", projectId);
        cmd.Parameters.AddWithValue("campaign", (object?)campaignId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cap", cap);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var wamid = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (string.IsNullOrWhiteSpace(wamid)) continue; // guarded by the WHERE, belt-and-suspenders
            var instanceId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
            rows.Add((reader.GetInt64(0), wamid, instanceId));
        }
        return rows;
    }

    /// <summary>
    /// Re-queue ONE undelivered recipient (by message id) of a project for a real re-send: flip the
    /// message 'failed'/'ambiguous' -> 'queued' (reset attempt + provider fields + captured wamid +
    /// delivery timestamps) so the EXISTING worker re-sends it via its PRESERVED route/template, and fix
    /// the broadcast counters in the SAME tx — old bucket --, queued ++ (the exact inverse of the
    /// terminal transition in IncrementBroadcastCounterAsync/MarkCxapiOutcomeAsync) — and reopen a
    /// 'completed' broadcast to 'sending'. Single atomic statement under a FOR UPDATE row lock. Tenant +
    /// project scoped via the message's broadcast. Returns the broadcast id on success, null if the
    /// message is missing / not eligible (not failed|ambiguous) / not one of THIS project's recipients.
    /// </summary>
    public virtual async Task<Guid?> RequeueForResendAsync(
        int tenantId, long projectId, long messageId, CancellationToken ct = default)
    {
        const string sql = @"
            WITH tgt AS (
                SELECT m.id, m.broadcast_id, m.status AS old_status
                FROM outbound_messages m
                WHERE m.id = @mid AND m.tenant_id = @tid
                  AND m.status IN ('failed','ambiguous')
                  AND m.broadcast_id IN (
                      SELECT unnest(broadcast_ids) FROM bulk_send_jobs
                      WHERE tenant_id = @tid AND project_id = @pid)
                FOR UPDATE
            ), upd AS (
                UPDATE outbound_messages m
                SET status = 'queued', attempt_count = 0,
                    provider_status_code = NULL, provider_status = NULL, provider_request_id = NULL,
                    provider_error_message = NULL, failed_reason = NULL, external_message_id = NULL,
                    delivered_at = NULL, read_at = NULL, last_attempt_at = NULL
                FROM tgt WHERE m.id = tgt.id
                RETURNING tgt.broadcast_id AS broadcast_id, tgt.old_status AS old_status
            ), bc AS (
                UPDATE outbound_broadcasts b
                SET queued    = b.queued + 1,
                    failed    = GREATEST(b.failed    - (CASE WHEN upd.old_status = 'failed'    THEN 1 ELSE 0 END), 0),
                    ambiguous = GREATEST(b.ambiguous - (CASE WHEN upd.old_status = 'ambiguous' THEN 1 ELSE 0 END), 0),
                    status    = CASE WHEN b.status = 'completed' THEN 'sending' ELSE b.status END,
                    completed_at = CASE WHEN b.status = 'completed' THEN NULL ELSE b.completed_at END
                FROM upd WHERE b.id = upd.broadcast_id AND b.tenant_id = @tid
                RETURNING b.id
            )
            SELECT broadcast_id FROM upd";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("mid", messageId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", projectId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : (Guid?)null;
    }

    /// <summary>
    /// Bulk variant of <see cref="RequeueForResendAsync"/>: re-queue EVERY 'failed'/'ambiguous' recipient that
    /// belongs to ANY of this project's runs, in one transaction via each row's PRESERVED route (the worker
    /// re-sends — no new send-orchestration path). The broadcast counters are decremented in aggregate per
    /// broadcast (failed/ambiguous → queued) and any 'completed' broadcast is re-opened to 'sending'. Returns
    /// the number of rows actually re-queued (0 when nothing was eligible — caller treats that as a no-op).
    /// </summary>
    public virtual async Task<int> RequeueAllForResendAsync(
        int tenantId, long projectId, CancellationToken ct = default)
    {
        const string sql = @"
            WITH tgt AS (
                SELECT m.id, m.broadcast_id, m.status AS old_status
                FROM outbound_messages m
                WHERE m.tenant_id = @tid
                  AND m.status IN ('failed','ambiguous')
                  AND m.broadcast_id IN (
                      SELECT unnest(broadcast_ids) FROM bulk_send_jobs
                      WHERE tenant_id = @tid AND project_id = @pid)
                FOR UPDATE
            ), upd AS (
                UPDATE outbound_messages m
                SET status = 'queued', attempt_count = 0,
                    provider_status_code = NULL, provider_status = NULL, provider_request_id = NULL,
                    provider_error_message = NULL, failed_reason = NULL, external_message_id = NULL,
                    delivered_at = NULL, read_at = NULL, last_attempt_at = NULL
                FROM tgt WHERE m.id = tgt.id
                RETURNING tgt.broadcast_id AS broadcast_id, tgt.old_status AS old_status
            ), agg AS (
                SELECT broadcast_id,
                       COUNT(*)::int                                              AS requeued,
                       COUNT(*) FILTER (WHERE old_status = 'failed')::int         AS failed_dec,
                       COUNT(*) FILTER (WHERE old_status = 'ambiguous')::int      AS ambiguous_dec
                FROM upd
                GROUP BY broadcast_id
            ), bc AS (
                UPDATE outbound_broadcasts b
                SET queued    = b.queued + agg.requeued,
                    failed    = GREATEST(b.failed    - agg.failed_dec,    0),
                    ambiguous = GREATEST(b.ambiguous - agg.ambiguous_dec, 0),
                    status    = CASE WHEN b.status = 'completed' THEN 'sending' ELSE b.status END,
                    completed_at = CASE WHEN b.status = 'completed' THEN NULL ELSE b.completed_at END
                FROM agg WHERE b.id = agg.broadcast_id AND b.tenant_id = @tid
                RETURNING b.id
            )
            SELECT COUNT(*)::int FROM upd";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", projectId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int n ? n : 0;
    }
}
