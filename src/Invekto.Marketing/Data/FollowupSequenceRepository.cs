using System.Text.Json;
using Invekto.Shared.Contracts.Followup;
using Invekto.Shared.Data;
using Npgsql;

namespace Invekto.Marketing.Data;

/// <summary>
/// FEAT-EFS data access for <c>event_followup_sequences</c>, <c>event_followup_runs</c>,
/// <c>tenant_settings.efs_*</c>, and <c>leads.followup_*</c>. All methods are tenant-scoped
/// — callers MUST pass tenant_id from the verified JWT claim, never from request body.
///
/// Snake_case JSON serialization is used so persisted JSONB shape matches the wire DTO
/// contract emitted by GET /api/v1/followup/sequences.
/// </summary>
public sealed class FollowupSequenceRepository
{
    /// <summary>
    /// Snake-cased serializer; mirrors the contract used by Backend's TFM endpoints so
    /// GET / PUT round-trips preserve field names.
    /// </summary>
    public static readonly JsonSerializerOptions SnakeCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private readonly PostgresConnectionFactory _db;

    public FollowupSequenceRepository(PostgresConnectionFactory db)
    {
        _db = db;
    }

    /// <summary>
    /// Pass-through helper for callers that need a raw connection (e.g.
    /// <c>FollowupStageJob.ResolveSequenceByIdAsync</c> which inlines a focused SELECT).
    /// Centralizes connection lifetime so the repo remains the single owner of the
    /// <see cref="PostgresConnectionFactory"/>.
    /// </summary>
    public Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct) => _db.OpenConnectionAsync(ct);

    // ============================================================
    // Sequence CRUD
    // ============================================================

    /// <summary>
    /// List all sequences (enabled or not) for the tenant. Stages JSONB is deserialized
    /// using the snake-cased contract.
    /// </summary>
    public async Task<List<FollowupSequenceConfig>> ListSequencesAsync(int tenantId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT id, slug, stages::text, ab_split_percent, enabled, created_at, updated_at " +
            "FROM event_followup_sequences WHERE tenant_id = @t ORDER BY slug", conn);
        cmd.Parameters.AddWithValue("t", tenantId);

        var result = new List<FollowupSequenceConfig>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            result.Add(new FollowupSequenceConfig
            {
                Id = reader.GetInt64(0),
                Slug = reader.GetString(1),
                Stages = JsonSerializer.Deserialize<List<FollowupStageConfig>>(
                    reader.GetString(2), SnakeCaseJson) ?? new List<FollowupStageConfig>(),
                AbSplitPercent = reader.GetInt16(3),
                Enabled = reader.GetBoolean(4),
                CreatedAt = reader.GetFieldValue<DateTime>(5),
                UpdatedAt = reader.GetFieldValue<DateTime>(6)
            });
        }
        return result;
    }

    /// <summary>Find one sequence by tenant + slug. Returns NULL if not found.</summary>
    public async Task<FollowupSequenceConfig?> FindBySlugAsync(int tenantId, string slug, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT id, slug, stages::text, ab_split_percent, enabled, created_at, updated_at " +
            "FROM event_followup_sequences WHERE tenant_id = @t AND slug = @s LIMIT 1", conn);
        cmd.Parameters.AddWithValue("t", tenantId);
        cmd.Parameters.AddWithValue("s", slug);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

        return new FollowupSequenceConfig
        {
            Id = reader.GetInt64(0),
            Slug = reader.GetString(1),
            Stages = JsonSerializer.Deserialize<List<FollowupStageConfig>>(
                reader.GetString(2), SnakeCaseJson) ?? new List<FollowupStageConfig>(),
            AbSplitPercent = reader.GetInt16(3),
            Enabled = reader.GetBoolean(4),
            CreatedAt = reader.GetFieldValue<DateTime>(5),
            UpdatedAt = reader.GetFieldValue<DateTime>(6)
        };
    }

    /// <summary>
    /// UPSERT by (tenant_id, slug) — INSERT ON CONFLICT. Returns the canonical post-write row
    /// so callers see server-assigned id/timestamps.
    /// </summary>
    public async Task<FollowupSequenceConfig> UpsertSequenceAsync(int tenantId, FollowupSequenceConfig config, CancellationToken ct)
    {
        var stagesJson = JsonSerializer.Serialize(config.Stages, SnakeCaseJson);

        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO event_followup_sequences (tenant_id, slug, stages, ab_split_percent, enabled, created_at, updated_at) " +
            "VALUES (@t, @s, @stages::jsonb, @ab, @en, now(), now()) " +
            "ON CONFLICT (tenant_id, slug) DO UPDATE SET " +
            "    stages = EXCLUDED.stages, " +
            "    ab_split_percent = EXCLUDED.ab_split_percent, " +
            "    enabled = EXCLUDED.enabled, " +
            "    updated_at = now() " +
            "RETURNING id, created_at, updated_at", conn);
        cmd.Parameters.AddWithValue("t", tenantId);
        cmd.Parameters.AddWithValue("s", config.Slug);
        cmd.Parameters.AddWithValue("stages", stagesJson);
        cmd.Parameters.AddWithValue("ab", (short)config.AbSplitPercent);
        cmd.Parameters.AddWithValue("en", config.Enabled);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("UPSERT did not RETURN a row — check Postgres trigger interference.");

        return new FollowupSequenceConfig
        {
            Id = reader.GetInt64(0),
            Slug = config.Slug,
            Stages = config.Stages,
            AbSplitPercent = config.AbSplitPercent,
            Enabled = config.Enabled,
            CreatedAt = reader.GetFieldValue<DateTime>(1),
            UpdatedAt = reader.GetFieldValue<DateTime>(2)
        };
    }

    // ============================================================
    // Tenant settings (test mode + no-reply threshold)
    // ============================================================

    /// <summary>
    /// Read EFS-relevant tenant settings in a single round-trip. Returns sane defaults
    /// (efs_test_mode=false, threshold=3) when the row is missing.
    /// </summary>
    public async Task<(bool TestMode, int NoReplyThresholdDays)> GetTenantEfsSettingsAsync(int tenantId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT efs_test_mode, efs_no_reply_threshold_days FROM tenant_settings " +
            "WHERE tenant_id = @t LIMIT 1", conn);
        cmd.Parameters.AddWithValue("t", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return (TestMode: false, NoReplyThresholdDays: 3);

        var testMode = !reader.IsDBNull(0) && reader.GetBoolean(0);
        var threshold = reader.IsDBNull(1) ? 3 : reader.GetInt32(1);
        return (testMode, threshold);
    }

    // ============================================================
    // Lead followup state (opt-out + ab group)
    // ============================================================

    /// <summary>
    /// Read the lead's followup-relevant fields used by Orchestrator collision detection
    /// and StageJob opt-out check. Returns NULL if the lead does not exist (caller maps to
    /// 404 / INV-MK-051).
    ///
    /// Opt-out: canonical source is <c>inma_optout_outbox</c> latest event_type for
    /// (tenant, phone). Additionally, per defense-in-depth (lessons 2026-04-21 Codex
    /// iter 2 Q5), the stored <c>followup_state.opted_out_at</c> JSONB key is consulted
    /// — if EITHER signal says opt-out, we skip. This protects against the unlikely
    /// case where inma_optout_outbox pruning has occurred but the lead's local mirror
    /// still carries the flag, and symmetrically catches the case where an orchestrator
    /// upstream wrote opted_out_at before the outbox row was seen.
    /// </summary>
    public async Task<FollowupLeadState?> GetLeadFollowupStateAsync(int tenantId, long leadId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        // Both signals are read in the same query:
        //   latest_opt.event_type — FEAT-J2 canonical outbox.
        //   followup_state ->> 'opted_out_at' — defense-in-depth mirror from lead JSONB.
        await using var cmd = new NpgsqlCommand(
            "SELECT " +
            "    l.followup_state::text, " +
            "    l.followup_ab_group, " +
            "    latest_opt.event_type, " +
            "    (l.followup_state ->> 'opted_out_at') AS state_opted_out_at " +
            "FROM leads l " +
            "LEFT JOIN LATERAL ( " +
            "    SELECT event_type FROM inma_optout_outbox o " +
            "    WHERE o.tenant_id = l.tenant_id AND o.phone = l.phone " +
            "    ORDER BY o.created_at DESC LIMIT 1 " +
            ") latest_opt ON TRUE " +
            "WHERE l.id = @lid AND l.tenant_id = @tid LIMIT 1", conn);
        cmd.Parameters.AddWithValue("lid", leadId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

        var stateJson = reader.IsDBNull(0) ? "{}" : reader.GetString(0);
        var abGroup = reader.IsDBNull(1) ? null : reader.GetString(1);
        var optOutFromOutbox = !reader.IsDBNull(2) && string.Equals(reader.GetString(2), "opt_out", StringComparison.OrdinalIgnoreCase);
        var optOutFromState = !reader.IsDBNull(3) && !string.IsNullOrWhiteSpace(reader.GetString(3));
        var optOut = optOutFromOutbox || optOutFromState;
        return new FollowupLeadState(stateJson, abGroup, optOut);
    }

    /// <summary>
    /// Record A/B group decision on the lead at first EnqueueAsync. Idempotent — caller
    /// may safely re-invoke with the same group; row UPDATE does not change history.
    ///
    /// JSONB merge (lessons 2026-04-21 Codex iter 0 CQ9 / Q1 / Q3 FAIL): followup_state
    /// MUST be MERGED via the <c>||</c> operator so previously-recorded audit keys (e.g.
    /// <c>opted_out_at</c>, historical <c>last_sequence_id</c>) are preserved. Overwriting
    /// with <c>jsonb_build_object(...)</c> destroys any pre-existing audit entries and
    /// breaks the execution-time opt-out contract read by
    /// <see cref="GetLeadFollowupStateAsync"/>. The <c>COALESCE(..., '{}'::jsonb)</c>
    /// guard handles the NULL-safe case even though the column is NOT NULL DEFAULT '{}' —
    /// defensive against a future ALTER that could relax the default.
    /// </summary>
    public async Task SetLeadAbGroupAsync(int tenantId, long leadId, string abGroup, long sequenceId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "UPDATE leads SET " +
            "    followup_ab_group = @ab, " +
            "    followup_state = COALESCE(followup_state, '{}'::jsonb) || jsonb_build_object( " +
            "        'current_sequence_id', @sid::bigint, " +
            "        'current_ab_group', @ab::text, " +
            "        'ab_assigned_at', now() " +
            "    ) " +
            "WHERE id = @lid AND tenant_id = @tid", conn);
        cmd.Parameters.AddWithValue("ab", abGroup);
        cmd.Parameters.AddWithValue("sid", sequenceId);
        cmd.Parameters.AddWithValue("lid", leadId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    // ============================================================
    // Run history (event_followup_runs)
    // ============================================================

    /// <summary>
    /// Detect existing scheduled runs for (tenant, lead) — the orchestrator's idempotency
    /// guard. Returns count of rows with status='scheduled'.
    /// </summary>
    public async Task<int> CountScheduledRunsForLeadAsync(int tenantId, long leadId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM event_followup_runs " +
            "WHERE tenant_id = @tid AND lead_id = @lid AND status = 'scheduled'", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lid", leadId);

        var raw = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return raw is long l ? (int)l : 0;
    }

    /// <summary>
    /// Insert a new scheduled run row. Returns the assigned id so the orchestrator can
    /// pass it as the Hangfire job argument.
    /// </summary>
    public async Task<long> InsertScheduledRunAsync(
        int tenantId,
        long sequenceId,
        long leadId,
        int stageIndex,
        string abGroup,
        DateTime scheduledAt,
        CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO event_followup_runs " +
            "    (tenant_id, sequence_id, lead_id, stage_index, ab_group, scheduled_at, status) " +
            "VALUES (@tid, @sid, @lid, @idx, @ab, @sched, 'scheduled') " +
            "RETURNING id", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("sid", sequenceId);
        cmd.Parameters.AddWithValue("lid", leadId);
        cmd.Parameters.AddWithValue("idx", (short)stageIndex);
        cmd.Parameters.AddWithValue("ab", abGroup);
        cmd.Parameters.AddWithValue("sched", scheduledAt);

        var raw = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return raw is long l ? l : throw new InvalidOperationException("INSERT ... RETURNING did not produce id");
    }

    /// <summary>Stamp the Hangfire job id onto a previously inserted run row.</summary>
    public async Task UpdateRunHangfireJobIdAsync(long runId, string hangfireJobId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "UPDATE event_followup_runs SET hangfire_job_id = @hid WHERE id = @rid", conn);
        cmd.Parameters.AddWithValue("hid", hangfireJobId);
        cmd.Parameters.AddWithValue("rid", runId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Read a scheduled run row before execution (job handler entry). Returns NULL if the
    /// run was deleted (e.g. sequence cascade) — handler maps to no-op + INV-MK-051 log.
    /// </summary>
    public async Task<FollowupRunRow?> GetRunAsync(long runId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT id, tenant_id, sequence_id, lead_id, stage_index, ab_group, scheduled_at, status " +
            "FROM event_followup_runs WHERE id = @rid LIMIT 1", conn);
        cmd.Parameters.AddWithValue("rid", runId);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

        return new FollowupRunRow(
            Id: reader.GetInt64(0),
            TenantId: reader.GetInt32(1),
            SequenceId: reader.GetInt64(2),
            LeadId: reader.GetInt64(3),
            StageIndex: reader.GetInt16(4),
            AbGroup: reader.GetString(5),
            ScheduledAt: reader.GetFieldValue<DateTime>(6),
            Status: reader.GetString(7));
    }

    /// <summary>
    /// Mark a run as terminal (sent / skipped_optout / skipped_disabled / failed) with
    /// optional error code. ExecutedAt is stamped to now() server-side.
    /// </summary>
    public async Task MarkRunTerminalAsync(long runId, string status, string? errorCode, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "UPDATE event_followup_runs SET " +
            "    status = @st, " +
            "    error_code = @ec, " +
            "    executed_at = now() " +
            "WHERE id = @rid", conn);
        cmd.Parameters.AddWithValue("st", status);
        cmd.Parameters.AddWithValue("ec", (object?)errorCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("rid", runId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Tenant-scoped run history view for the Dashboard Runs tab. Limited to the most
    /// recent 200 rows; pagination/filter is a future enhancement.
    /// </summary>
    public async Task<List<FollowupRunRow>> ListRecentRunsForTenantAsync(int tenantId, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT id, tenant_id, sequence_id, lead_id, stage_index, ab_group, scheduled_at, status " +
            "FROM event_followup_runs WHERE tenant_id = @tid " +
            "ORDER BY scheduled_at DESC LIMIT 200", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var rows = new List<FollowupRunRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new FollowupRunRow(
                Id: reader.GetInt64(0),
                TenantId: reader.GetInt32(1),
                SequenceId: reader.GetInt64(2),
                LeadId: reader.GetInt64(3),
                StageIndex: reader.GetInt16(4),
                AbGroup: reader.GetString(5),
                ScheduledAt: reader.GetFieldValue<DateTime>(6),
                Status: reader.GetString(7)));
        }
        return rows;
    }
}

/// <summary>Snapshot of a lead row used by the orchestrator + stage job.</summary>
public sealed record FollowupLeadState(string FollowupStateJson, string? AbGroup, bool OptOut);

/// <summary>Projection of <c>event_followup_runs</c> used in handler + ops surfaces.</summary>
public sealed record FollowupRunRow(
    long Id,
    int TenantId,
    long SequenceId,
    long LeadId,
    int StageIndex,
    string AbGroup,
    DateTime ScheduledAt,
    string Status);
