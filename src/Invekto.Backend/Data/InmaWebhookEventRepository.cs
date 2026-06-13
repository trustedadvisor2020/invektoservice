using Invekto.Shared.Data;
using Npgsql;

namespace Invekto.Backend.Data;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C2 — persists INMA <c>customer.selection_changed</c> events and applies
/// the derived opaque <c>leads.customer_status</c>. Idempotency + audit + status apply all happen
/// in ONE transaction so a crash before commit rolls everything back (the at-least-once redelivery
/// then re-runs cleanly); a crash after commit leaves the dedupe anchor so the redelivery is a no-op.
///
/// NOT tenant-scoped at the repository level (it is called from the webhook handler with an already
/// authenticated tenant_id); every statement is explicitly tenant_id-scoped.
/// </summary>
public sealed class InmaWebhookEventRepository
{
    private readonly PostgresConnectionFactory _db;

    public InmaWebhookEventRepository(PostgresConnectionFactory db) => _db = db;

    /// <summary>
    /// Insert the event (dedupe on tenant_id,event_id) and, if newly inserted, apply customer_status
    /// to matching leads under an occurredAt monotonic guard. Throws <see cref="NpgsqlException"/> on
    /// DB failure (caller maps to INV-INM-004 / HTTP 500 so INMA retries).
    /// </summary>
    public async Task<InmaWebhookPersistResult> PersistAndApplyAsync(
        InmaWebhookEventRow row,
        IReadOnlyList<string> normalizedPhones,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // (1) Dedupe + durable audit. RETURNING id yields a row ONLY on a fresh insert.
        const string insertSql = @"
            INSERT INTO inma_webhook_events
                (tenant_id, event_id, event_type, occurred_at, actor_type, origin_request_id,
                 body_company_id, customer_id, feature_group_id, selection_mode, derived_status,
                 raw_body, raw_body_sha256)
            VALUES
                (@tid, @eid, @etype, @occ, @actor, @origin,
                 @bcid, @cid, @fgid, @mode, @status,
                 @raw, @sha)
            ON CONFLICT (tenant_id, event_id) DO NOTHING
            RETURNING id";

        bool inserted;
        await using (var insertCmd = new NpgsqlCommand(insertSql, conn, (NpgsqlTransaction)tx))
        {
            insertCmd.Parameters.AddWithValue("tid", row.TenantId);
            insertCmd.Parameters.AddWithValue("eid", row.EventId);
            insertCmd.Parameters.AddWithValue("etype", row.EventType);
            insertCmd.Parameters.AddWithValue("occ", (object?)row.OccurredAt ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("actor", (object?)row.ActorType ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("origin", (object?)row.OriginRequestId ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("bcid", (object?)row.BodyCompanyId ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("cid", (object?)row.CustomerId ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("fgid", (object?)row.FeatureGroupId ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("mode", (object?)row.SelectionMode ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("status", (object?)row.DerivedStatus ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("raw", row.RawBody);
            insertCmd.Parameters.AddWithValue("sha", row.RawBodySha256);

            var insertedId = await insertCmd.ExecuteScalarAsync(ct);
            inserted = insertedId is int;
        }

        // (2) Duplicate path: compare stored body hash so a divergent re-send is not silently swallowed.
        if (!inserted)
        {
            const string shaSql = "SELECT raw_body_sha256 FROM inma_webhook_events WHERE tenant_id=@tid AND event_id=@eid";
            await using var shaCmd = new NpgsqlCommand(shaSql, conn, (NpgsqlTransaction)tx);
            shaCmd.Parameters.AddWithValue("tid", row.TenantId);
            shaCmd.Parameters.AddWithValue("eid", row.EventId);
            var storedSha = (await shaCmd.ExecuteScalarAsync(ct)) as string;

            await tx.CommitAsync(ct);
            return new InmaWebhookPersistResult(
                string.Equals(storedSha, row.RawBodySha256, StringComparison.Ordinal)
                    ? InmaWebhookPersistOutcome.DuplicateSameBody
                    : InmaWebhookPersistOutcome.DuplicateDivergentBody,
                0);
        }

        // (3) No usable phone -> store-only, no apply.
        if (normalizedPhones.Count == 0)
        {
            await tx.CommitAsync(ct);
            return new InmaWebhookPersistResult(InmaWebhookPersistOutcome.StoredPhoneUnmatched, 0);
        }

        // (4) Apply status to matching leads under the occurredAt monotonic guard.
        //  - A TIMESTAMPED event applies iff it is >= the last applied occurredAt (or none yet), and
        //    advances customer_status_occurred_at.
        //  - An UNORDERED event (occurredAt absent) is lowest-priority: it may only fill a lead that has
        //    NO prior timestamp, and it NEVER resets an existing one (COALESCE keeps the stored value).
        //    This prevents a late/untimed event from overwriting newer state or disabling future
        //    stale-protection by nulling the anchor.
        const string updateSql = @"
            UPDATE leads
               SET customer_status = @status,
                   customer_status_occurred_at = COALESCE(@occ, customer_status_occurred_at)
             WHERE tenant_id = @tid
               AND regexp_replace(phone, '\D', '', 'g') = ANY(@phones)
               AND (
                    (@occ IS NOT NULL AND (customer_status_occurred_at IS NULL OR customer_status_occurred_at <= @occ))
                 OR (@occ IS NULL AND customer_status_occurred_at IS NULL)
               )";

        int updated;
        await using (var updateCmd = new NpgsqlCommand(updateSql, conn, (NpgsqlTransaction)tx))
        {
            updateCmd.Parameters.AddWithValue("status", (object?)row.DerivedStatus ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("occ", (object?)row.OccurredAt ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("tid", row.TenantId);
            updateCmd.Parameters.AddWithValue("phones", normalizedPhones.ToArray());
            updated = await updateCmd.ExecuteNonQueryAsync(ct);
        }

        InmaWebhookPersistOutcome outcome;
        if (updated > 0)
        {
            outcome = InmaWebhookPersistOutcome.StoredApplied;
        }
        else
        {
            // 0 updated: distinguish "no such lead" (INV-INM-005) from "lead exists but our event is
            // older than the last applied one" (stale-skip — healthy, not a miss).
            const string existsSql = @"
                SELECT EXISTS(
                    SELECT 1 FROM leads
                     WHERE tenant_id = @tid
                       AND regexp_replace(phone, '\D', '', 'g') = ANY(@phones))";
            await using var existsCmd = new NpgsqlCommand(existsSql, conn, (NpgsqlTransaction)tx);
            existsCmd.Parameters.AddWithValue("tid", row.TenantId);
            existsCmd.Parameters.AddWithValue("phones", normalizedPhones.ToArray());
            var leadExists = (await existsCmd.ExecuteScalarAsync(ct)) is true;
            outcome = leadExists
                ? InmaWebhookPersistOutcome.StoredStaleSkipped
                : InmaWebhookPersistOutcome.StoredPhoneUnmatched;
        }

        // Record the apply count on the audit row (best-effort audit completeness, same tx).
        const string countSql = "UPDATE inma_webhook_events SET matched_lead_count=@c WHERE tenant_id=@tid AND event_id=@eid";
        await using (var countCmd = new NpgsqlCommand(countSql, conn, (NpgsqlTransaction)tx))
        {
            countCmd.Parameters.AddWithValue("c", updated);
            countCmd.Parameters.AddWithValue("tid", row.TenantId);
            countCmd.Parameters.AddWithValue("eid", row.EventId);
            await countCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return new InmaWebhookPersistResult(outcome, updated);
    }
}

/// <summary>Immutable input for one INMA system-event persist. All nullable fields map to DBNull.</summary>
public sealed record InmaWebhookEventRow(
    int TenantId,
    string EventId,
    string EventType,
    DateTimeOffset? OccurredAt,
    string? ActorType,
    string? OriginRequestId,
    int? BodyCompanyId,
    int? CustomerId,
    int? FeatureGroupId,
    string? SelectionMode,
    string? DerivedStatus,
    string RawBody,
    string RawBodySha256);

public enum InmaWebhookPersistOutcome
{
    /// <summary>Fresh event, customer_status applied to >=1 lead.</summary>
    StoredApplied,
    /// <summary>Fresh event, a matching lead exists but our occurredAt is older than the last applied — skipped (healthy).</summary>
    StoredStaleSkipped,
    /// <summary>Fresh event, no lead matched any normalized phone (or no usable phone) — raw retained, INV-INM-005.</summary>
    StoredPhoneUnmatched,
    /// <summary>Duplicate (tenant_id,event_id) with identical body — idempotent no-op.</summary>
    DuplicateSameBody,
    /// <summary>Duplicate id but DIFFERENT body hash — INMA anomaly/replay, INV-INM-006.</summary>
    DuplicateDivergentBody
}

public readonly record struct InmaWebhookPersistResult(InmaWebhookPersistOutcome Outcome, int MatchedLeadCount);
