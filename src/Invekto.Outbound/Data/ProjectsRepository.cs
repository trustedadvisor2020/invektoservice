using Invekto.Shared.Data;
using Invekto.Shared.DTOs.Outbound;
using Npgsql;

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
    public virtual async Task<List<ProjectSummary>> ListAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT p.id, p.name, p.description, p.status,
                   (SELECT COUNT(*) FROM project_targets pt
                      WHERE pt.tenant_id = p.tenant_id AND pt.project_id = p.id)::int AS target_count,
                   p.run_count, p.total_targets, p.sent_count, p.delivered_count,
                   p.read_count, p.failed_count, p.ambiguous_count,
                   p.created_at, p.updated_at, p.started_at, p.completed_at
            FROM projects p
            WHERE p.tenant_id = @tid AND p.archived_at IS NULL
            ORDER BY p.created_at DESC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

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
                   p.created_at, p.updated_at, p.started_at, p.completed_at
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
        CancellationToken ct = default)
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
            INSERT INTO projects (tenant_id, name, description, status, created_by)
            VALUES (@tid, @name, @desc, 'draft', @by)
            RETURNING id", conn, tx))
        {
            ins.Parameters.AddWithValue("tid", tenantId);
            ins.Parameters.AddWithValue("name", name);
            ins.Parameters.AddWithValue("desc", (object?)description ?? DBNull.Value);
            ins.Parameters.AddWithValue("by", createdBy);
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
        CancellationToken ct = default)
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
                updated_at = NOW()
            WHERE tenant_id = @tid AND id = @pid AND archived_at IS NULL", conn, tx))
        {
            upd.Parameters.AddWithValue("tid", tenantId);
            upd.Parameters.AddWithValue("pid", projectId);
            upd.Parameters.AddWithValue("name", (object?)name ?? DBNull.Value);
            upd.Parameters.AddWithValue("desc", (object?)description ?? DBNull.Value);
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
        CompletedAt = reader.IsDBNull(15) ? null : reader.GetDateTime(15)
    };
}
