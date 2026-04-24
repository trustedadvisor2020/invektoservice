using System.Text.Json;
using Invekto.Shared.Contracts.MetaLeadgen;
using Invekto.Shared.Data;
using Npgsql;
using NpgsqlTypes;

namespace Invekto.Backend.Services.MetaLeadgen;

/// <summary>
/// Paket B-META: repository for <c>meta_leadgen_events</c> — the audit trail /
/// idempotency anchor / Dashboard [Test Events] source.
///
/// Contract discipline:
///   * <see cref="InsertEventAsync"/> is the single entry point; UNIQUE violation
///     on <c>(tenant_id, leadgen_id)</c> is the at-least-once dedup signal and
///     is returned explicitly via <see cref="InsertEventOutcome.AlreadyExists"/>
///     rather than thrown, so the caller branches cleanly without swallowing
///     unrelated Npgsql errors.
///   * <see cref="ListRecentAsync"/> NEVER surfaces <c>raw_payload</c> — only
///     the top-level key set (PII-safe). Operators with direct DB access see
///     raw payloads; the Dashboard surface does not.
///   * <see cref="UpdateLeadLinkAsync"/> writes back <c>lead_id</c> + optional
///     <c>error_code</c> after the async intake job completes. Idempotent via
///     composite <c>(id, tenant_id)</c> WHERE clause.
/// </summary>
public class MetaLeadgenEventRepository
{
    private readonly PostgresConnectionFactory _db;

    public MetaLeadgenEventRepository(PostgresConnectionFactory db)
    {
        _db = db;
    }

    /// <summary>
    /// Attempts to INSERT a new event row. On UNIQUE (tenant_id, leadgen_id)
    /// violation returns <see cref="InsertEventOutcome.AlreadyExists"/> with the
    /// existing row's id (looked up in a second SELECT so the caller can update
    /// the retry attempt's http_status / error_code without double-enqueueing
    /// the Hangfire job).
    /// </summary>
    public virtual async Task<InsertEventOutcome> InsertEventAsync(
        int tenantId,
        string leadgenId,
        string? formId,
        string? pageId,
        short httpStatus,
        string? errorCode,
        string? rawPayloadJson,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO meta_leadgen_events
                (tenant_id, leadgen_id, form_id, page_id,
                 received_at, http_status, error_code, raw_payload)
            VALUES
                (@tid, @lg, @fid, @pid, NOW(), @hs, @ec, @raw::jsonb)
            ON CONFLICT (tenant_id, leadgen_id) DO NOTHING
            RETURNING id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lg", leadgenId);
        cmd.Parameters.AddWithValue("fid", (object?)formId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pid", (object?)pageId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hs", httpStatus);
        cmd.Parameters.AddWithValue("ec", (object?)errorCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("raw", (object?)rawPayloadJson ?? DBNull.Value);

        var inserted = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (inserted is long newId)
            return InsertEventOutcome.Inserted(newId);

        // Conflict fired -> locate existing row id + error_code + lead_id so the
        // caller can choose one of three paths:
        //  (a) Normal dedup for a previously-processed event (lead_id IS NOT
        //      NULL, error_code IS NULL) — just return 200, skip enqueue.
        //  (b) Explicit enqueue-retry marker (error_code = INV-JOB-001 from a
        //      prior Hangfire outage) — re-enqueue and clear the marker.
        //  (c) Stuck row (lead_id IS NULL, error_code IS NULL) — first webhook
        //      INSERT succeeded but BOTH the Hangfire enqueue AND the follow-up
        //      UpdateStatusAsync (which would have stamped INV-JOB-001) failed
        //      at once, leaving the row looking "healthy" even though intake
        //      never completed. The caller re-enqueues here; LeadIntakeService
        //      phone-idempotency and Hangfire's own at-most-once queue dedup
        //      prevent double-processing if the original job is in flight.
        await using var lookupCmd = conn.CreateCommand();
        lookupCmd.CommandText =
            "SELECT id, error_code, lead_id FROM meta_leadgen_events WHERE tenant_id = @tid AND leadgen_id = @lg LIMIT 1";
        lookupCmd.Parameters.AddWithValue("tid", tenantId);
        lookupCmd.Parameters.AddWithValue("lg", leadgenId);
        await using var reader = await lookupCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var existingId = reader.GetInt64(0);
            var existingErrorCode = reader.IsDBNull(1) ? null : reader.GetString(1);
            var hasLinkedLead = !reader.IsDBNull(2);
            return InsertEventOutcome.AlreadyExists(existingId, existingErrorCode, hasLinkedLead);
        }

        // Should be unreachable: INSERT said conflict, SELECT said absent. Caller
        // will treat as INFRA failure (distinct from duplicate path) and audit.
        return InsertEventOutcome.Unknown();
    }

    /// <summary>
    /// Explicit status update — overwrites both <c>http_status</c> and
    /// <c>error_code</c> (no COALESCE), so callers can CLEAR the error_code by
    /// passing null. Used by the webhook service to mark an audit row as
    /// "enqueue failed, Meta retry can recover" (INV-JOB-001) and to clear
    /// the marker once the retry enqueue succeeds.
    ///
    /// Separate from <see cref="UpdateLeadLinkAsync"/> which COALESCEs for the
    /// Hangfire post-intake back-fill path (keeps existing error_code on the
    /// audit row if the job didn't hit a terminal failure mode).
    /// </summary>
    public virtual async Task<bool> UpdateStatusAsync(
        long eventId,
        int tenantId,
        short httpStatus,
        string? errorCode,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE meta_leadgen_events
            SET http_status = @hs,
                error_code  = @ec
            WHERE id = @eid AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("eid", eventId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("hs", httpStatus);
        cmd.Parameters.AddWithValue("ec", (object?)errorCode ?? DBNull.Value);
        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return rows > 0;
    }

    /// <summary>
    /// Back-fills <c>lead_id</c> + optional terminal <c>error_code</c> after the
    /// async Hangfire intake job resolves. Returns whether the UPDATE touched
    /// a row (belt-and-braces against stale eventId from job payload).
    /// </summary>
    public virtual async Task<bool> UpdateLeadLinkAsync(
        long eventId,
        int tenantId,
        int? leadId,
        string? errorCode,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE meta_leadgen_events
            SET lead_id    = COALESCE(@lid, lead_id),
                error_code = COALESCE(@ec, error_code)
            WHERE id = @eid AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("eid", eventId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lid", (object?)leadId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ec", (object?)errorCode ?? DBNull.Value);
        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return rows > 0;
    }

    /// <summary>
    /// Returns up to <paramref name="limit"/> most recent events for the tenant
    /// from the last 7 days (partial-index hot path). PayloadKeys derived from
    /// the stored JSON via <c>jsonb_object_keys</c> — no raw values leak to the
    /// Dashboard surface.
    /// </summary>
    public virtual async Task<List<MetaLeadgenEventDto>> ListRecentAsync(
        int tenantId, int limit, CancellationToken ct = default)
    {
        if (limit <= 0) limit = 5;
        if (limit > 50) limit = 50;

        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, leadgen_id, form_id, page_id, received_at,
                   http_status, error_code, lead_id,
                   CASE
                     WHEN raw_payload IS NULL THEN ARRAY[]::text[]
                     ELSE (SELECT COALESCE(array_agg(k), ARRAY[]::text[])
                           FROM jsonb_object_keys(raw_payload) AS k)
                   END AS payload_keys
            FROM meta_leadgen_events
            WHERE tenant_id = @tid
              AND received_at > NOW() - INTERVAL '7 days'
            ORDER BY received_at DESC
            LIMIT @lim";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.Add("lim", NpgsqlDbType.Integer).Value = limit;

        var result = new List<MetaLeadgenEventDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            result.Add(new MetaLeadgenEventDto
            {
                Id          = reader.GetInt64(0),
                LeadgenId   = reader.GetString(1),
                FormId      = reader.IsDBNull(2) ? null : reader.GetString(2),
                PageId      = reader.IsDBNull(3) ? null : reader.GetString(3),
                ReceivedAt  = reader.GetDateTime(4),
                HttpStatus  = reader.IsDBNull(5) ? (short?)null : reader.GetInt16(5),
                ErrorCode   = reader.IsDBNull(6) ? null : reader.GetString(6),
                LeadId      = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                PayloadKeys = reader.IsDBNull(8)
                    ? new List<string>()
                    : new List<string>(reader.GetFieldValue<string[]>(8))
            });
        }
        return result;
    }
}

/// <summary>
/// InsertEvent outcome carrier — discriminates "new row inserted" from
/// "duplicate detected" without throwing (at-least-once dedup is a normal
/// control flow, not an exceptional case).
/// </summary>
public sealed class InsertEventOutcome
{
    public long EventId { get; init; }
    public bool IsDuplicate { get; init; }
    public bool IsUnknownFailure { get; init; }

    /// <summary>
    /// Only populated on <see cref="IsDuplicate"/>. NULL = healthy prior event
    /// (normal Meta retry — just return 200). Non-null = the prior webhook hit
    /// the Hangfire enqueue-failure recovery path (typically INV-JOB-001); the
    /// caller should retry enqueue and CLEAR the marker on success so
    /// subsequent retries don't loop.
    /// </summary>
    public string? ExistingErrorCode { get; init; }

    /// <summary>
    /// True iff the prior event row has a non-null <c>lead_id</c>, i.e. the
    /// Hangfire intake job completed and back-filled the link. Used together
    /// with <see cref="ExistingErrorCode"/> to detect the "stuck row" edge case
    /// where BOTH enqueue AND the status-marker UPDATE failed — the row looks
    /// healthy (http_status=200, error_code=null) but intake never ran. A
    /// subsequent Meta retry arrives with <c>HasLinkedLead=false,
    /// ExistingErrorCode=null</c> and the caller re-enqueues from the duplicate
    /// path to recover. False for all freshly-inserted rows (the
    /// <see cref="IsDuplicate"/>=false case) since lead_id lands later via
    /// <see cref="UpdateLeadLinkAsync"/>.
    /// </summary>
    public bool HasLinkedLead { get; init; }

    public static InsertEventOutcome Inserted(long id) =>
        new() { EventId = id, IsDuplicate = false, HasLinkedLead = false };

    public static InsertEventOutcome AlreadyExists(long id, string? existingErrorCode, bool hasLinkedLead) =>
        new() { EventId = id, IsDuplicate = true, ExistingErrorCode = existingErrorCode, HasLinkedLead = hasLinkedLead };

    public static InsertEventOutcome Unknown() =>
        new() { EventId = 0, IsUnknownFailure = true };
}
