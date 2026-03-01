using Invekto.Shared.Data;
using Invekto.Shared.DTOs.Leads;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Backend.Data;

/// <summary>
/// PostgreSQL repository for leads + lead_activities tables.
/// Thread-safe, register as singleton. PKT-6B1: GR-3.13 Lead Management v2.
/// </summary>
public class LeadRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public LeadRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ================================================================
    // Lead CRUD
    // ================================================================

    /// <summary>
    /// Upsert a lead. On conflict (tenant_id, phone), updates name/email/source/interest/notes.
    /// Returns lead id.
    /// </summary>
    public virtual async Task<int> UpsertLeadAsync(
        int tenantId, LeadRequest request, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO leads
                (tenant_id, phone, name, email, source, utm_source, utm_medium, utm_campaign,
                 interest, notes)
            VALUES
                (@tid, @phone, @name, @email, @source, @utmSource, @utmMedium, @utmCampaign,
                 @interest, @notes)
            ON CONFLICT (tenant_id, phone)
            DO UPDATE SET
                name = COALESCE(EXCLUDED.name, leads.name),
                email = COALESCE(EXCLUDED.email, leads.email),
                source = EXCLUDED.source,
                interest = COALESCE(EXCLUDED.interest, leads.interest),
                notes = COALESCE(EXCLUDED.notes, leads.notes),
                updated_at = NOW()
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", request.Phone);
        cmd.Parameters.AddWithValue("name", (object?)request.Name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("email", (object?)request.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("source", request.Source ?? "organic");
        cmd.Parameters.AddWithValue("utmSource", (object?)request.UtmSource ?? DBNull.Value);
        cmd.Parameters.AddWithValue("utmMedium", (object?)request.UtmMedium ?? DBNull.Value);
        cmd.Parameters.AddWithValue("utmCampaign", (object?)request.UtmCampaign ?? DBNull.Value);
        cmd.Parameters.AddWithValue("interest", (object?)request.Interest ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes", (object?)request.Notes ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public virtual async Task<LeadResponse?> GetLeadAsync(
        int tenantId, int leadId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, phone, name, email, source, utm_source, utm_medium, utm_campaign,
                   interest, score, pipeline_status, assigned_to, last_contact_at,
                   next_followup_at, followup_count, notes, is_hot, created_at, updated_at
            FROM leads
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", leadId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadLeadResponse(reader);
        return null;
    }

    public virtual async Task<List<LeadResponse>> ListLeadsAsync(
        int tenantId, string? pipelineStatus, bool? isHot,
        string? search, int limit, int offset, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, phone, name, email, source, utm_source, utm_medium, utm_campaign,
                   interest, score, pipeline_status, assigned_to, last_contact_at,
                   next_followup_at, followup_count, notes, is_hot, created_at, updated_at
            FROM leads
            WHERE tenant_id = @tid
              AND (@status IS NULL OR pipeline_status = @status)
              AND (@isHot IS NULL OR is_hot = @isHot)
              AND (@search IS NULL OR phone ILIKE @search OR name ILIKE @search)
            ORDER BY created_at DESC LIMIT @limit OFFSET @offset";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("status", (object?)pipelineStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("isHot", isHot.HasValue ? isHot.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("search", !string.IsNullOrEmpty(search) ? $"%{search}%" : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("limit", Math.Min(limit, 200));
        cmd.Parameters.AddWithValue("offset", Math.Max(offset, 0));

        var leads = new List<LeadResponse>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            leads.Add(ReadLeadResponse(reader));
        return leads;
    }

    // ================================================================
    // Pipeline & Score
    // ================================================================

    /// <summary>
    /// Update pipeline status. Inserts a status_change activity.
    /// Returns false if lead not found.
    /// </summary>
    public virtual async Task<bool> UpdatePipelineStatusAsync(
        int tenantId, int leadId, string newStatus, string? assignedTo,
        CancellationToken ct = default)
    {
        const string selectSql = @"
            SELECT pipeline_status FROM leads
            WHERE tenant_id = @tid AND id = @id";

        const string updateSql = @"
            UPDATE leads
            SET pipeline_status = @newStatus,
                assigned_to = COALESCE(@assigned, assigned_to),
                last_contact_at = NOW(),
                updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id";

        const string activitySql = @"
            INSERT INTO lead_activities (lead_id, tenant_id, activity_type, old_value, new_value)
            VALUES (@id, @tid, 'status_change', @oldStatus, @newStatus)";

        await using var conn = await _db.OpenConnectionAsync(ct);

        // Get current status
        await using var selectCmd = new NpgsqlCommand(selectSql, conn);
        selectCmd.Parameters.AddWithValue("tid", tenantId);
        selectCmd.Parameters.AddWithValue("id", leadId);
        var oldStatus = await selectCmd.ExecuteScalarAsync(ct) as string;
        if (oldStatus == null) return false;

        // Update status
        await using var updateCmd = new NpgsqlCommand(updateSql, conn);
        updateCmd.Parameters.AddWithValue("tid", tenantId);
        updateCmd.Parameters.AddWithValue("id", leadId);
        updateCmd.Parameters.AddWithValue("newStatus", newStatus);
        updateCmd.Parameters.AddWithValue("assigned", (object?)assignedTo ?? DBNull.Value);
        await updateCmd.ExecuteNonQueryAsync(ct);

        // Insert activity
        await using var activityCmd = new NpgsqlCommand(activitySql, conn);
        activityCmd.Parameters.AddWithValue("id", leadId);
        activityCmd.Parameters.AddWithValue("tid", tenantId);
        activityCmd.Parameters.AddWithValue("oldStatus", oldStatus);
        activityCmd.Parameters.AddWithValue("newStatus", newStatus);
        await activityCmd.ExecuteNonQueryAsync(ct);

        return true;
    }

    /// <summary>
    /// Update lead score (0-100). Sets is_hot if score >= 80.
    /// Returns false if lead not found.
    /// </summary>
    public virtual async Task<bool> UpdateScoreAsync(
        int tenantId, int leadId, int newScore, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE leads
            SET score = @score,
                is_hot = (@score >= 80),
                updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", leadId);
        cmd.Parameters.AddWithValue("score", Math.Clamp(newScore, 0, 100));

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    // ================================================================
    // Activities
    // ================================================================

    public virtual async Task<int> InsertActivityAsync(
        int tenantId, int leadId, string activityType,
        string? oldValue, string? newValue, string? note,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO lead_activities (lead_id, tenant_id, activity_type, old_value, new_value, note)
            VALUES (@leadId, @tid, @type, @old, @new, @note)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("leadId", leadId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("type", activityType);
        cmd.Parameters.AddWithValue("old", (object?)oldValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("new", (object?)newValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public virtual async Task<List<LeadActivityResponse>> GetActivitiesAsync(
        int tenantId, int leadId, int limit, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, activity_type, old_value, new_value, note, created_at
            FROM lead_activities
            WHERE tenant_id = @tid AND lead_id = @leadId
            ORDER BY created_at DESC LIMIT @limit";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("leadId", leadId);
        cmd.Parameters.AddWithValue("limit", Math.Min(limit, 100));

        var activities = new List<LeadActivityResponse>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            activities.Add(new LeadActivityResponse
            {
                Id = reader.GetInt32(0),
                ActivityType = reader.GetString(1),
                OldValue = reader.IsDBNull(2) ? null : reader.GetString(2),
                NewValue = reader.IsDBNull(3) ? null : reader.GetString(3),
                Note = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5)
            });
        }
        return activities;
    }

    // ================================================================
    // Funnel & Hot leads
    // ================================================================

    public virtual async Task<LeadFunnelStatsResponse> GetFunnelStatsAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT pipeline_status, COUNT(*) as cnt
            FROM leads
            WHERE tenant_id = @tid
            GROUP BY pipeline_status";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var byStatus = new Dictionary<string, int>();
        var total = 0;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var status = reader.GetString(0);
            var count = reader.GetInt32(1);
            byStatus[status] = count;
            total += count;
        }

        return new LeadFunnelStatsResponse
        {
            TotalLeads = total,
            ByStatus = byStatus
        };
    }

    public virtual async Task<List<LeadResponse>> GetHotLeadsAsync(
        int tenantId, int limit, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, phone, name, email, source, utm_source, utm_medium, utm_campaign,
                   interest, score, pipeline_status, assigned_to, last_contact_at,
                   next_followup_at, followup_count, notes, is_hot, created_at, updated_at
            FROM leads
            WHERE tenant_id = @tid AND is_hot = TRUE
                AND pipeline_status NOT IN ('patient', 'lost')
            ORDER BY score DESC, created_at DESC
            LIMIT @limit";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("limit", Math.Min(limit, 50));

        var leads = new List<LeadResponse>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            leads.Add(ReadLeadResponse(reader));
        return leads;
    }

    /// <summary>
    /// Schedule next follow-up. Increments followup_count.
    /// </summary>
    public virtual async Task<bool> ScheduleFollowUpAsync(
        int tenantId, int leadId, DateTime followUpAt, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE leads
            SET next_followup_at = @followUp,
                followup_count = followup_count + 1,
                updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id
                AND pipeline_status NOT IN ('patient', 'lost')";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", leadId);
        cmd.Parameters.AddWithValue("followUp", followUpAt);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static LeadResponse ReadLeadResponse(NpgsqlDataReader reader)
    {
        return new LeadResponse
        {
            Id = reader.GetInt32(0),
            Phone = reader.GetString(1),
            Name = reader.IsDBNull(2) ? null : reader.GetString(2),
            Email = reader.IsDBNull(3) ? null : reader.GetString(3),
            Source = reader.GetString(4),
            UtmSource = reader.IsDBNull(5) ? null : reader.GetString(5),
            UtmMedium = reader.IsDBNull(6) ? null : reader.GetString(6),
            UtmCampaign = reader.IsDBNull(7) ? null : reader.GetString(7),
            Interest = reader.IsDBNull(8) ? null : reader.GetString(8),
            Score = reader.GetInt32(9),
            PipelineStatus = reader.GetString(10),
            AssignedTo = reader.IsDBNull(11) ? null : reader.GetString(11),
            LastContactAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            NextFollowupAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
            FollowupCount = reader.GetInt32(14),
            Notes = reader.IsDBNull(15) ? null : reader.GetString(15),
            IsHot = reader.GetBoolean(16),
            CreatedAt = reader.GetDateTime(17),
            UpdatedAt = reader.GetDateTime(18)
        };
    }
}
