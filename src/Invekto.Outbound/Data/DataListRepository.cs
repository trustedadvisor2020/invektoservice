using Invekto.Shared.Data;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Outbound.Data;

/// <summary>
/// Persistence for FEAT-OBI Phase 1A contact lists (data_lists + list_records).
/// Every query is tenant_id scoped (project multi-tenant isolation rule); the
/// composite FK (tenant_id,list_id) makes a cross-tenant child row impossible.
/// Import is fully transactional (atomic) so a partial failure rolls back and
/// leaves NO half-built list. Register as singleton.
/// </summary>
public class DataListRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public DataListRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // Normalized row handed to ImportRecordsAsync (phone already E.164, deduped).
    // ------------------------------------------------------------------
    public sealed class NormalizedImportRow
    {
        public string Phone { get; init; } = "";
        public string? Name { get; init; }
        public string? Surname { get; init; }
        public string? Email { get; init; }
        public string? Tags { get; init; }
        public string? Note { get; init; }
        public string? Field1 { get; init; }
        public string? Field2 { get; init; }
        public string? Field3 { get; init; }
        public string? Field4 { get; init; }
        public string? Field5 { get; init; }
        public string? CustomFieldsJson { get; init; }
    }

    public sealed class ImportWriteResult
    {
        public bool Found { get; init; } = true;       // false => existing list vanished (TOCTOU)
        public bool NameConflict { get; init; }         // true => createName collided (new-list path)
        public bool CapExceeded { get; init; }
        public bool DbError { get; init; }               // true => mid-import DB failure (rolled back)
        public long ListId { get; init; }
        public int CurrentTotal { get; init; }
        public int AttemptedNew { get; init; }
        public int Inserted { get; init; }
        public int Updated { get; init; }
        public int Skipped { get; init; }
        public int TotalRecords { get; init; }
        public int SendableCount { get; init; }
    }

    // ------------------------------------------------------------------
    // List CRUD
    // ------------------------------------------------------------------
    public virtual async Task<List<DataListSummary>> ListAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, name, source, active, status, total_records, sendable_count, created_at, updated_at
            FROM data_lists
            WHERE tenant_id=@tid AND deleted_at IS NULL
            ORDER BY created_at DESC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var list = new List<DataListSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(MapSummary(reader));
        return list;
    }

    public virtual async Task<DataListSummary?> GetAsync(int tenantId, long listId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, name, source, active, status, total_records, sendable_count, created_at, updated_at
            FROM data_lists
            WHERE tenant_id=@tid AND id=@lid AND deleted_at IS NULL";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lid", listId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return MapSummary(reader);
    }

    /// <summary>Create an empty list. Returns (id, nameConflict). nameConflict=true when the
    /// normalized active name already exists for this tenant (partial-unique index).</summary>
    public virtual async Task<(long? id, bool nameConflict)> CreateAsync(
        int tenantId, string name, string source, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO data_lists (tenant_id, name, source, status)
            VALUES (@tid, @name, @src, 'ready')
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("src", source);
        try
        {
            var id = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
            return (id, false);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return (null, true);
        }
    }

    /// <summary>Returns: 0 = not found, 1 = updated, -1 = name conflict.</summary>
    public virtual async Task<int> UpdateAsync(
        int tenantId, long listId, string? name, bool? active, CancellationToken ct = default)
    {
        // Build a COALESCE-based partial update so only supplied fields change.
        const string sql = @"
            UPDATE data_lists
            SET name = COALESCE(@name, name),
                active = COALESCE(@active, active),
                updated_at = NOW()
            WHERE tenant_id=@tid AND id=@lid AND deleted_at IS NULL";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lid", listId);
        cmd.Parameters.AddWithValue("name", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("active", (object?)active ?? DBNull.Value);
        try
        {
            return await cmd.ExecuteNonQueryAsync(ct) == 1 ? 1 : 0;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return -1;
        }
    }

    public virtual async Task<bool> SoftDeleteAsync(int tenantId, long listId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE data_lists SET deleted_at=NOW(), updated_at=NOW()
            WHERE tenant_id=@tid AND id=@lid AND deleted_at IS NULL";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lid", listId);
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    // ------------------------------------------------------------------
    // Existence probe (records/exists)
    // ------------------------------------------------------------------
    /// <summary>Normalized phones that already exist for this tenant (optionally scoped to one list).</summary>
    public virtual async Task<HashSet<string>> ExistingPhonesAsync(
        int tenantId, long? listId, IReadOnlyCollection<string> normalizedPhones, CancellationToken ct = default)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (normalizedPhones.Count == 0) return result;

        var sql = listId.HasValue
            ? "SELECT DISTINCT normalized_phone FROM list_records WHERE tenant_id=@tid AND list_id=@lid AND normalized_phone = ANY(@phones)"
            : "SELECT DISTINCT normalized_phone FROM list_records WHERE tenant_id=@tid AND normalized_phone = ANY(@phones)";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        if (listId.HasValue) cmd.Parameters.AddWithValue("lid", listId.Value);
        cmd.Parameters.AddWithValue("phones", normalizedPhones.ToArray());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            if (!reader.IsDBNull(0)) result.Add(reader.GetString(0));
        return result;
    }

    // ------------------------------------------------------------------
    // Transactional import (atomic — no half-built list on failure)
    // ------------------------------------------------------------------
    /// <summary>
    /// Atomic import. Target is EITHER an existing <paramref name="existingListId"/> OR a new list
    /// created in-transaction from <paramref name="createName"/> (exactly one non-null). Creating the
    /// list inside the same transaction means a failed import leaves NO orphan list (full rollback).
    /// </summary>
    public virtual async Task<ImportWriteResult> ImportRecordsAsync(
        int tenantId, long? existingListId, string? createName, string scenario, ImportCustomFlags? flags,
        IReadOnlyList<NormalizedImportRow> rows, int maxRecordsPerList, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        long listId;
        int currentTotal;

        if (createName != null)
        {
            // New-list path: create inside the tx so a later failure rolls it back too.
            // Starts 'importing'; the success-path recompute flips it to 'ready'. A failure rolls
            // the whole insert back (no orphan row), so 'importing' is never observable on failure.
            await using var ins = new NpgsqlCommand(
                "INSERT INTO data_lists (tenant_id, name, source, status) VALUES (@tid, @name, 'upload', 'importing') RETURNING id",
                conn, tx);
            ins.Parameters.AddWithValue("tid", tenantId);
            ins.Parameters.AddWithValue("name", createName);
            try
            {
                listId = Convert.ToInt64(await ins.ExecuteScalarAsync(ct));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await tx.RollbackAsync(ct);
                return new ImportWriteResult { NameConflict = true };
            }
            currentTotal = 0;
        }
        else
        {
            // Existing-list path: lock the row, confirm it still exists for this tenant.
            if (!existingListId.HasValue)
                throw new ArgumentException("ImportRecordsAsync requires exactly one of existingListId/createName");
            listId = existingListId.Value;
            await using var lk = new NpgsqlCommand(
                "SELECT total_records FROM data_lists WHERE tenant_id=@tid AND id=@lid AND deleted_at IS NULL FOR UPDATE", conn, tx);
            lk.Parameters.AddWithValue("tid", tenantId);
            lk.Parameters.AddWithValue("lid", listId);
            var scalar = await lk.ExecuteScalarAsync(ct);
            if (scalar == null) // list vanished between resolve and lock (TOCTOU)
            {
                await tx.RollbackAsync(ct);
                return new ImportWriteResult { Found = false };
            }
            currentTotal = Convert.ToInt32(scalar);
        }

        var phones = rows.Select(r => r.Phone).ToArray();
        var existing = await ExistingPhonesInTxAsync(conn, tx, tenantId, listId, phones, ct);
        var canInsert = scenario is ImportScenarios.NewOnly or ImportScenarios.RecallAll or ImportScenarios.Custom;
        var attemptedNew = canInsert ? phones.Count(p => !existing.Contains(p)) : 0;

        // Cap enforcement — reject BEFORE any write (Codex: no auto-partition).
        if (currentTotal + attemptedNew > maxRecordsPerList)
        {
            await tx.RollbackAsync(ct);
            return new ImportWriteResult
            {
                CapExceeded = true,
                CurrentTotal = currentTotal,
                AttemptedNew = attemptedNew
            };
        }

        var inserted = 0;
        var updated = 0;

        try
        {
        switch (scenario)
        {
            case ImportScenarios.NewOnly:
                inserted = await InsertRowsAsync(conn, tx, tenantId, listId, rows, onConflictUpdate: false, ct);
                break;

            case ImportScenarios.RecallAll:
                // Upsert: insert new + reactivate/merge existing.
                await InsertRowsAsync(conn, tx, tenantId, listId, rows, onConflictUpdate: true, ct);
                inserted = attemptedNew;
                updated = existing.Count;
                break;

            case ImportScenarios.UpdateInfo:
                // Update fields of existing records only; never insert, never touch sendable.
                updated = await UpdateExistingFieldsAsync(conn, tx, tenantId, listId, rows, touchSendable: false, ct);
                break;

            case ImportScenarios.Custom:
                inserted = await InsertRowsAsync(conn, tx, tenantId, listId, rows, onConflictUpdate: false, ct);
                if (flags is { } customFlags && (customFlags.UpdateDuplicateFields || customFlags.SetDuplicatesSendable))
                    updated = await UpdateExistingCustomAsync(conn, tx, tenantId, listId, rows, customFlags, ct);
                break;
        }

        // Authoritative counter recompute (single statement; never incremental).
        int totalRecords, sendableCount;
        await using (var rc = new NpgsqlCommand(@"
            UPDATE data_lists SET
                total_records = (SELECT COUNT(*) FROM list_records WHERE tenant_id=@tid AND list_id=@lid),
                sendable_count = (SELECT COUNT(*) FROM list_records WHERE tenant_id=@tid AND list_id=@lid AND sendable=TRUE),
                status = 'ready',
                updated_at = NOW()
            WHERE tenant_id=@tid AND id=@lid
            RETURNING total_records, sendable_count", conn, tx))
        {
            rc.Parameters.AddWithValue("tid", tenantId);
            rc.Parameters.AddWithValue("lid", listId);
            await using var reader = await rc.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            totalRecords = reader.GetInt32(0);
            sendableCount = reader.GetInt32(1);
        }

        await tx.CommitAsync(ct);

        var skipped = phones.Length - inserted - updated;
        if (skipped < 0) skipped = 0;
        return new ImportWriteResult
        {
            ListId = listId,
            CapExceeded = false,
            CurrentTotal = currentTotal,
            AttemptedNew = attemptedNew,
            Inserted = inserted,
            Updated = updated,
            Skipped = skipped,
            TotalRecords = totalRecords,
            SendableCount = sendableCount
        };
        }
        catch (NpgsqlException ex)
        {
            // Mid-import DB failure (server error OR connection/transient — NpgsqlException is the
            // common base): roll the whole import back (no half-built list), surface a typed result
            // the service maps to an actionable INV-OB code. For an EXISTING list, flip status='failed'
            // in a fresh connection (the import tx rolled back, so this write must live outside it) so
            // the operator sees a recoverable state (re-import allowed) instead of a stale 'ready'.
            // A NEW list rolled back entirely — there is no orphan row to mark.
            // Rollback is best-effort: if the connection is already broken the `await using` disposal
            // still rolls back, and we must NOT let a rollback failure skip the 'failed' marker (which
            // uses a fresh connection) — otherwise an existing list would be stuck looking 'ready'.
            try { await tx.RollbackAsync(ct); }
            catch (NpgsqlException rbEx) { _logger.SystemError($"Contact import rollback failed (tenant={tenantId}, list={listId}); `await using` disposal will roll back: {rbEx.Message}"); }
            _logger.SystemError($"Contact import DB failure: tenant={tenantId}, list={listId}, msg={ex.Message}");
            if (existingListId.HasValue)
                await MarkListFailedAsync(tenantId, existingListId.Value, ct);
            return new ImportWriteResult { DbError = true, ListId = listId };
        }
    }

    /// <summary>Flag an existing list as failed after a rolled-back import (separate connection).</summary>
    private async Task MarkListFailedAsync(int tenantId, long listId, CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(
                "UPDATE data_lists SET status='failed', updated_at=NOW() WHERE tenant_id=@tid AND id=@lid AND deleted_at IS NULL",
                conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("lid", listId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (NpgsqlException ex)
        {
            // Best-effort: the import already failed; never let the failure-marker write (server OR
            // connection error) escape and mask the original DB error the caller is about to report.
            _logger.SystemError($"MarkListFailed failed: tenant={tenantId}, list={listId}, msg={ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // Import helpers (set-based via unnest)
    // ------------------------------------------------------------------
    private static async Task<HashSet<string>> ExistingPhonesInTxAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long listId,
        string[] phones, CancellationToken ct)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (phones.Length == 0) return set;
        await using var cmd = new NpgsqlCommand(
            "SELECT normalized_phone FROM list_records WHERE tenant_id=@tid AND list_id=@lid AND normalized_phone = ANY(@phones)",
            conn, tx);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lid", listId);
        cmd.Parameters.AddWithValue("phones", phones);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            if (!reader.IsDBNull(0)) set.Add(reader.GetString(0));
        return set;
    }

    private static (string[] phones, string[] names, string[] surnames, string[] emails,
        string[] tags, string[] notes, string[] f1, string[] f2, string[] f3, string[] f4,
        string[] f5, string[] cf) ToArrays(IReadOnlyList<DataListRepository.NormalizedImportRow> rows)
    {
        var n = rows.Count;
        var phones = new string[n]; var names = new string[n]; var surnames = new string[n];
        var emails = new string[n]; var tags = new string[n]; var notes = new string[n];
        var f1 = new string[n]; var f2 = new string[n]; var f3 = new string[n];
        var f4 = new string[n]; var f5 = new string[n]; var cf = new string[n];
        for (var i = 0; i < n; i++)
        {
            var r = rows[i];
            phones[i] = r.Phone;
            names[i] = r.Name ?? ""; surnames[i] = r.Surname ?? ""; emails[i] = r.Email ?? "";
            tags[i] = r.Tags ?? ""; notes[i] = r.Note ?? "";
            f1[i] = r.Field1 ?? ""; f2[i] = r.Field2 ?? ""; f3[i] = r.Field3 ?? "";
            f4[i] = r.Field4 ?? ""; f5[i] = r.Field5 ?? ""; cf[i] = r.CustomFieldsJson ?? "";
        }
        return (phones, names, surnames, emails, tags, notes, f1, f2, f3, f4, f5, cf);
    }

    private static void BindImportArrays(NpgsqlCommand cmd,
        (string[] phones, string[] names, string[] surnames, string[] emails, string[] tags,
         string[] notes, string[] f1, string[] f2, string[] f3, string[] f4, string[] f5, string[] cf) a)
    {
        cmd.Parameters.AddWithValue("phones", a.phones);
        cmd.Parameters.AddWithValue("names", a.names);
        cmd.Parameters.AddWithValue("surnames", a.surnames);
        cmd.Parameters.AddWithValue("emails", a.emails);
        cmd.Parameters.AddWithValue("tags", a.tags);
        cmd.Parameters.AddWithValue("notes", a.notes);
        cmd.Parameters.AddWithValue("f1", a.f1);
        cmd.Parameters.AddWithValue("f2", a.f2);
        cmd.Parameters.AddWithValue("f3", a.f3);
        cmd.Parameters.AddWithValue("f4", a.f4);
        cmd.Parameters.AddWithValue("f5", a.f5);
        cmd.Parameters.AddWithValue("cf", a.cf);
    }

    private const string UnnestFrom = @"
        FROM unnest(@phones::text[], @names::text[], @surnames::text[], @emails::text[],
                    @tags::text[], @notes::text[], @f1::text[], @f2::text[], @f3::text[],
                    @f4::text[], @f5::text[], @cf::text[])
             AS t(phone, name, surname, email, tags, note, f1, f2, f3, f4, f5, cf)";

    /// <summary>Insert valid rows (sendable=TRUE). onConflictUpdate=true => reactivate + merge non-empty fields.</summary>
    private async Task<int> InsertRowsAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long listId,
        IReadOnlyList<NormalizedImportRow> rows, bool onConflictUpdate, CancellationToken ct)
    {
        if (rows.Count == 0) return 0;

        var conflict = onConflictUpdate
            ? @"ON CONFLICT (list_id, normalized_phone) DO UPDATE SET
                    sendable = TRUE,
                    name = COALESCE(NULLIF(EXCLUDED.name,''), list_records.name),
                    surname = COALESCE(NULLIF(EXCLUDED.surname,''), list_records.surname),
                    email = COALESCE(NULLIF(EXCLUDED.email,''), list_records.email),
                    tags = COALESCE(NULLIF(EXCLUDED.tags,''), list_records.tags),
                    note = COALESCE(NULLIF(EXCLUDED.note,''), list_records.note),
                    field1 = COALESCE(NULLIF(EXCLUDED.field1,''), list_records.field1),
                    field2 = COALESCE(NULLIF(EXCLUDED.field2,''), list_records.field2),
                    field3 = COALESCE(NULLIF(EXCLUDED.field3,''), list_records.field3),
                    field4 = COALESCE(NULLIF(EXCLUDED.field4,''), list_records.field4),
                    field5 = COALESCE(NULLIF(EXCLUDED.field5,''), list_records.field5),
                    custom_fields = COALESCE(EXCLUDED.custom_fields, list_records.custom_fields),
                    updated_at = NOW()"
            : "ON CONFLICT (list_id, normalized_phone) DO NOTHING";

        var sql = $@"
            INSERT INTO list_records
                (list_id, tenant_id, normalized_phone, name, surname, email, tags, note,
                 field1, field2, field3, field4, field5, custom_fields, sendable)
            SELECT @lid, @tid, t.phone,
                 NULLIF(t.name,''), NULLIF(t.surname,''), NULLIF(t.email,''),
                 NULLIF(t.tags,''), NULLIF(t.note,''),
                 NULLIF(t.f1,''), NULLIF(t.f2,''), NULLIF(t.f3,''), NULLIF(t.f4,''), NULLIF(t.f5,''),
                 NULLIF(t.cf,'')::jsonb, TRUE
            {UnnestFrom}
            {conflict}";

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("lid", listId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        BindImportArrays(cmd, ToArrays(rows));
        // For DO NOTHING this returns only the rows actually inserted (new phones).
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Update fields of EXISTING records only (merge non-empty). touchSendable left as-is.</summary>
    private async Task<int> UpdateExistingFieldsAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long listId,
        IReadOnlyList<NormalizedImportRow> rows, bool touchSendable, CancellationToken ct)
    {
        if (rows.Count == 0) return 0;
        var sql = $@"
            UPDATE list_records lr SET
                name = COALESCE(NULLIF(t.name,''), lr.name),
                surname = COALESCE(NULLIF(t.surname,''), lr.surname),
                email = COALESCE(NULLIF(t.email,''), lr.email),
                tags = COALESCE(NULLIF(t.tags,''), lr.tags),
                note = COALESCE(NULLIF(t.note,''), lr.note),
                field1 = COALESCE(NULLIF(t.f1,''), lr.field1),
                field2 = COALESCE(NULLIF(t.f2,''), lr.field2),
                field3 = COALESCE(NULLIF(t.f3,''), lr.field3),
                field4 = COALESCE(NULLIF(t.f4,''), lr.field4),
                field5 = COALESCE(NULLIF(t.f5,''), lr.field5),
                custom_fields = COALESCE(NULLIF(t.cf,'')::jsonb, lr.custom_fields),
                updated_at = NOW()
            {UnnestFrom}
            WHERE lr.tenant_id=@tid AND lr.list_id=@lid AND lr.normalized_phone = t.phone";

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("lid", listId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        BindImportArrays(cmd, ToArrays(rows));
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    // ------------------------------------------------------------------
    // FEAT-PROJELER PKT-14 — read-only list insights (aha pack + viewer).
    // ------------------------------------------------------------------

    /// <summary>
    /// One round-trip insight over the selected lists for the Projeler modal:
    /// precise deduplicated reach + per-column fill stats (SENDABLE rows only — matches
    /// who a send would target) + a real sample recipient (first sendable record of the
    /// FIRST id, the operator's primary list). Foreign/other-tenant ids simply contribute
    /// nothing (every query carries tenant_id) — scoping IS the ownership check.
    /// </summary>
    public virtual async Task<DataListPreviewSampleResponse> GetPreviewSampleAsync(
        int tenantId, long[] listIds, CancellationToken ct = default)
    {
        const string statsSql = @"
            SELECT
                COUNT(DISTINCT normalized_phone)::int                   AS reach,
                COUNT(*)::int                                           AS total,
                COUNT(*) FILTER (WHERE COALESCE(name,'')    <> '')::int AS f_name,
                COUNT(*) FILTER (WHERE COALESCE(surname,'') <> '')::int AS f_surname,
                COUNT(*) FILTER (WHERE COALESCE(email,'')   <> '')::int AS f_email,
                COUNT(*) FILTER (WHERE COALESCE(field1,'')  <> '')::int AS f_field1,
                COUNT(*) FILTER (WHERE COALESCE(field2,'')  <> '')::int AS f_field2,
                COUNT(*) FILTER (WHERE COALESCE(field3,'')  <> '')::int AS f_field3,
                COUNT(*) FILTER (WHERE COALESCE(field4,'')  <> '')::int AS f_field4,
                COUNT(*) FILTER (WHERE COALESCE(field5,'')  <> '')::int AS f_field5,
                COUNT(*) FILTER (WHERE COALESCE(tags,'')    <> '')::int AS f_tags,
                COUNT(*) FILTER (WHERE COALESCE(note,'')    <> '')::int AS f_note
            FROM list_records
            WHERE tenant_id=@tid AND list_id = ANY(@ids) AND sendable = TRUE";

        const string sampleSql = @"
            SELECT id, normalized_phone, name, surname, email, field1, field2, field3, field4, field5, tags, note, sendable
            FROM list_records
            WHERE tenant_id=@tid AND list_id=@first AND sendable = TRUE
            ORDER BY id
            LIMIT 1";

        var response = new DataListPreviewSampleResponse();

        await using var conn = await _db.OpenConnectionAsync(ct);

        await using (var cmd = new NpgsqlCommand(statsSql, conn))
        {
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("ids", listIds);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                response.Reach = reader.GetInt32(0);
                var total = reader.GetInt32(1);
                string[] cols = { "name", "surname", "email", "field1", "field2", "field3", "field4", "field5", "tags", "note" };
                for (var i = 0; i < cols.Length; i++)
                    response.ColumnStats[cols[i]] = new ListColumnStatDto { Filled = reader.GetInt32(2 + i), Total = total };
            }
        }

        await using (var cmd = new NpgsqlCommand(sampleSql, conn))
        {
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("first", listIds[0]);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
                response.Sample = MapRecord(reader);
        }

        return response;
    }

    /// <summary>
    /// Server-paged, searchable records of ONE list (viewer popup). Static SQL: the search is a
    /// single parameterized ILIKE pattern over every text column (the SERVICE escapes LIKE
    /// wildcards and builds the %...% pattern); an empty @q short-circuits the filter. Caller
    /// pre-validates list ownership (GetAsync) and clamps page/pageSize.
    /// </summary>
    public virtual async Task<ListRecordsPageResponse> GetRecordsPageAsync(
        int tenantId, long listId, string likePattern, int page, int pageSize, CancellationToken ct = default)
    {
        const string where = @"
            WHERE tenant_id=@tid AND list_id=@lid
              AND (@q = '' OR normalized_phone ILIKE @q OR name ILIKE @q OR surname ILIKE @q
                   OR email ILIKE @q OR field1 ILIKE @q OR field2 ILIKE @q OR field3 ILIKE @q
                   OR field4 ILIKE @q OR field5 ILIKE @q OR tags ILIKE @q OR note ILIKE @q)";

        const string countSql = $"SELECT COUNT(*)::int FROM list_records {where}";
        const string pageSql = $@"
            SELECT id, normalized_phone, name, surname, email, field1, field2, field3, field4, field5, tags, note, sendable
            FROM list_records
            {where}
            ORDER BY id
            LIMIT @limit OFFSET @offset";

        var response = new ListRecordsPageResponse { Page = page, PageSize = pageSize };

        await using var conn = await _db.OpenConnectionAsync(ct);

        await using (var cmd = new NpgsqlCommand(countSql, conn))
        {
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("lid", listId);
            cmd.Parameters.AddWithValue("q", likePattern);
            response.Total = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        }

        await using (var cmd = new NpgsqlCommand(pageSql, conn))
        {
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("lid", listId);
            cmd.Parameters.AddWithValue("q", likePattern);
            cmd.Parameters.AddWithValue("limit", pageSize);
            cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                response.Records.Add(MapRecord(reader));
        }

        return response;
    }

    /// <summary>Maps the shared 13-column record projection (id + phone..note + sendable).</summary>
    private static ListRecordDto MapRecord(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Phone = reader.IsDBNull(1) ? null : reader.GetString(1),
        Name = reader.IsDBNull(2) ? null : reader.GetString(2),
        Surname = reader.IsDBNull(3) ? null : reader.GetString(3),
        Email = reader.IsDBNull(4) ? null : reader.GetString(4),
        Field1 = reader.IsDBNull(5) ? null : reader.GetString(5),
        Field2 = reader.IsDBNull(6) ? null : reader.GetString(6),
        Field3 = reader.IsDBNull(7) ? null : reader.GetString(7),
        Field4 = reader.IsDBNull(8) ? null : reader.GetString(8),
        Field5 = reader.IsDBNull(9) ? null : reader.GetString(9),
        Tags = reader.IsDBNull(10) ? null : reader.GetString(10),
        Note = reader.IsDBNull(11) ? null : reader.GetString(11),
        Sendable = reader.GetBoolean(12)
    };

    // ------------------------------------------------------------------
    // Single-record mutations (list-viewer popup) — permanent delete + manual add.
    //
    // LOCK-FIRST transaction shape (plan-consult PR-001/PR-002): the data_lists
    // parent row is locked (FOR UPDATE) BEFORE any decision, so status/cap checks,
    // the write, and the COUNT(*)-based counter recompute are all serialized
    // against concurrent imports and other single-record mutations. Under READ
    // COMMITTED a blocked counter UPDATE would NOT re-evaluate its subqueries,
    // so taking the parent lock first is what makes the counters exact.
    // Early returns dispose the transaction => rollback, lock released.
    // ------------------------------------------------------------------

    public enum RecordMutationOutcome { Ok, ListNotFound, ListNotReady, CapExceeded, Duplicate, RecordNotFound }

    public sealed class RecordMutationResult
    {
        public RecordMutationOutcome Outcome { get; init; }
        public long RecordId { get; init; }
        public int TotalRecords { get; init; }
        public int SendableCount { get; init; }
    }

    /// <summary>
    /// Insert ONE manually-entered record (phone already E.164-normalized by the service;
    /// invalid phones are rejected upstream — unlike bulk import, no sendable=false rows here).
    /// Duplicate = uq_list_record_list_phone unique violation, constraint-name filtered, so
    /// detection is race-safe without a SELECT pre-check.
    /// </summary>
    public virtual async Task<RecordMutationResult> InsertRecordAsync(
        int tenantId, long listId, string normalizedPhone, string? name, string? surname, string? email,
        int maxRecordsPerList, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var (found, status, total) = await LockListRowAsync(conn, tx, tenantId, listId, ct);
        if (!found) return new RecordMutationResult { Outcome = RecordMutationOutcome.ListNotFound };
        if (status != "ready") return new RecordMutationResult { Outcome = RecordMutationOutcome.ListNotReady };
        if (total + 1 > maxRecordsPerList) return new RecordMutationResult { Outcome = RecordMutationOutcome.CapExceeded };

        long recordId;
        try
        {
            await using var ins = new NpgsqlCommand(@"
                INSERT INTO list_records (list_id, tenant_id, normalized_phone, name, surname, email, sendable)
                VALUES (@lid, @tid, @phone, NULLIF(@name,''), NULLIF(@surname,''), NULLIF(@email,''), TRUE)
                RETURNING id", conn, tx);
            ins.Parameters.AddWithValue("lid", listId);
            ins.Parameters.AddWithValue("tid", tenantId);
            ins.Parameters.AddWithValue("phone", normalizedPhone);
            ins.Parameters.AddWithValue("name", name ?? "");
            ins.Parameters.AddWithValue("surname", surname ?? "");
            ins.Parameters.AddWithValue("email", email ?? "");
            recordId = Convert.ToInt64(await ins.ExecuteScalarAsync(ct));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation
                                           && ex.ConstraintName == "uq_list_record_list_phone")
        {
            return new RecordMutationResult { Outcome = RecordMutationOutcome.Duplicate };
        }

        var (totalRecords, sendableCount) = await RecomputeCountersAsync(conn, tx, tenantId, listId, ct);
        await tx.CommitAsync(ct);
        return new RecordMutationResult
        {
            Outcome = RecordMutationOutcome.Ok,
            RecordId = recordId,
            TotalRecords = totalRecords,
            SendableCount = sendableCount
        };
    }

    /// <summary>Permanently delete ONE record of the tenant's list (viewer popup trash action).</summary>
    public virtual async Task<RecordMutationResult> DeleteRecordAsync(
        int tenantId, long listId, long recordId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var (found, status, _) = await LockListRowAsync(conn, tx, tenantId, listId, ct);
        if (!found) return new RecordMutationResult { Outcome = RecordMutationOutcome.ListNotFound };
        if (status != "ready") return new RecordMutationResult { Outcome = RecordMutationOutcome.ListNotReady };

        await using (var del = new NpgsqlCommand(
            "DELETE FROM list_records WHERE tenant_id=@tid AND list_id=@lid AND id=@rid", conn, tx))
        {
            del.Parameters.AddWithValue("tid", tenantId);
            del.Parameters.AddWithValue("lid", listId);
            del.Parameters.AddWithValue("rid", recordId);
            if (await del.ExecuteNonQueryAsync(ct) == 0)
                return new RecordMutationResult { Outcome = RecordMutationOutcome.RecordNotFound };
        }

        var (totalRecords, sendableCount) = await RecomputeCountersAsync(conn, tx, tenantId, listId, ct);
        await tx.CommitAsync(ct);
        return new RecordMutationResult
        {
            Outcome = RecordMutationOutcome.Ok,
            RecordId = recordId,
            TotalRecords = totalRecords,
            SendableCount = sendableCount
        };
    }

    /// <summary>Locks the parent data_lists row (FOR UPDATE) and returns its status + total.</summary>
    private static async Task<(bool found, string status, int totalRecords)> LockListRowAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long listId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(@"
            SELECT status, total_records FROM data_lists
            WHERE tenant_id=@tid AND id=@lid AND deleted_at IS NULL
            FOR UPDATE", conn, tx);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lid", listId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (false, "", 0);
        return (true, reader.GetString(0), reader.GetInt32(1));
    }

    /// <summary>
    /// COUNT(*)-based counter recompute inside the caller's transaction. Deliberately does NOT
    /// touch status (import's recompute sets status='ready'; a single-record mutation must never
    /// resurrect a non-ready list — non-ready is already rejected under the same lock).
    /// </summary>
    private static async Task<(int totalRecords, int sendableCount)> RecomputeCountersAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long listId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(@"
            UPDATE data_lists SET
                total_records = (SELECT COUNT(*)::int FROM list_records WHERE tenant_id=@tid AND list_id=@lid),
                sendable_count = (SELECT COUNT(*)::int FROM list_records WHERE tenant_id=@tid AND list_id=@lid AND sendable=TRUE),
                updated_at = NOW()
            WHERE tenant_id=@tid AND id=@lid
            RETURNING total_records, sendable_count", conn, tx);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lid", listId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    /// <summary>Custom-scenario update of existing records, gated by flags.</summary>
    private async Task<int> UpdateExistingCustomAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int tenantId, long listId,
        IReadOnlyList<NormalizedImportRow> rows, ImportCustomFlags flags, CancellationToken ct)
    {
        if (rows.Count == 0) return 0;

        var sets = new List<string> { "updated_at = NOW()" };
        if (flags.SetDuplicatesSendable) sets.Add("sendable = TRUE");
        if (flags.UpdateDuplicateFields)
            sets.AddRange(new[]
            {
                "name = COALESCE(NULLIF(t.name,''), lr.name)",
                "surname = COALESCE(NULLIF(t.surname,''), lr.surname)",
                "email = COALESCE(NULLIF(t.email,''), lr.email)",
                "tags = COALESCE(NULLIF(t.tags,''), lr.tags)",
                "note = COALESCE(NULLIF(t.note,''), lr.note)",
                "field1 = COALESCE(NULLIF(t.f1,''), lr.field1)",
                "field2 = COALESCE(NULLIF(t.f2,''), lr.field2)",
                "field3 = COALESCE(NULLIF(t.f3,''), lr.field3)",
                "field4 = COALESCE(NULLIF(t.f4,''), lr.field4)",
                "field5 = COALESCE(NULLIF(t.f5,''), lr.field5)",
                "custom_fields = COALESCE(NULLIF(t.cf,'')::jsonb, lr.custom_fields)"
            });

        var sql = $@"
            UPDATE list_records lr SET {string.Join(", ", sets)}
            {UnnestFrom}
            WHERE lr.tenant_id=@tid AND lr.list_id=@lid AND lr.normalized_phone = t.phone";

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("lid", listId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        BindImportArrays(cmd, ToArrays(rows));
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static DataListSummary MapSummary(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Name = reader.GetString(1),
        Source = reader.GetString(2),
        Active = reader.GetBoolean(3),
        Status = reader.GetString(4),
        TotalRecords = reader.GetInt32(5),
        SendableCount = reader.GetInt32(6),
        CreatedAt = reader.GetDateTime(7),
        UpdatedAt = reader.GetDateTime(8)
    };
}
