using System.Text.Json;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs.Attribution;
using Invekto.Shared.Logging;

namespace Invekto.Backend.Services;

/// <summary>
/// GR-3.14: Attribution repository for lead_attributions + ad_costs tables.
/// Thread-safe singleton, uses PostgresConnectionFactory for connection pooling.
/// Pattern: same as AnalyticsRepository (PKT-3).
/// </summary>
public class AttributionRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public AttributionRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // LEAD ATTRIBUTION - INSERT (webhook inline, < 5ms target)
    // ============================================================

    /// <summary>
    /// Insert lead attribution from webhook conversation_started event.
    /// Called inline during webhook processing - must be fast.
    /// </summary>
    public virtual async Task<int> InsertLeadAttributionAsync(
        int tenantId, AttributionTrackRequest req, string leadSource, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO lead_attributions (
                tenant_id, customer_phone, chat_id,
                utm_source, utm_medium, utm_campaign, utm_content, utm_term,
                meta_click_id, lead_source
            ) VALUES (
                @tid, @phone, @chat_id,
                @utm_source, @utm_medium, @utm_campaign, @utm_content, @utm_term,
                @meta_click_id, @lead_source
            )
            ON CONFLICT DO NOTHING
            RETURNING id";

        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", req.CustomerPhone);
        cmd.Parameters.AddWithValue("chat_id", (object?)req.ChatId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("utm_source", (object?)req.UtmSource ?? DBNull.Value);
        cmd.Parameters.AddWithValue("utm_medium", (object?)req.UtmMedium ?? DBNull.Value);
        cmd.Parameters.AddWithValue("utm_campaign", (object?)req.UtmCampaign ?? DBNull.Value);
        cmd.Parameters.AddWithValue("utm_content", (object?)req.UtmContent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("utm_term", (object?)req.UtmTerm ?? DBNull.Value);
        cmd.Parameters.AddWithValue("meta_click_id", (object?)req.MetaClickId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("lead_source", leadSource);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int id ? id : 0;
    }

    // ============================================================
    // LEAD ATTRIBUTION - QUERY
    // ============================================================

    /// <summary>
    /// List lead attributions for tenant with optional date range filter.
    /// </summary>
    public virtual async Task<List<LeadAttributionDto>> GetLeadAttributionsAsync(
        int tenantId, DateOnly? from, DateOnly? to, int limit = 200, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, tenant_id, customer_phone, chat_id,
                   utm_source, utm_medium, utm_campaign, utm_content, utm_term,
                   meta_click_id, lead_source, lead_labels,
                   conversion_status, conversion_value, converted_at, created_at
            FROM lead_attributions
            WHERE tenant_id = @tid
              AND (@from_date IS NULL OR created_at >= @from_date::timestamptz)
              AND (@to_date IS NULL OR created_at < (@to_date::date + INTERVAL '1 day')::timestamptz)
            ORDER BY created_at DESC
            LIMIT @lim";

        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("from_date", from.HasValue ? (object)from.Value.ToString("yyyy-MM-dd") : DBNull.Value);
        cmd.Parameters.AddWithValue("to_date", to.HasValue ? (object)to.Value.ToString("yyyy-MM-dd") : DBNull.Value);
        cmd.Parameters.AddWithValue("lim", limit);

        var result = new List<LeadAttributionDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(MapLeadAttribution(reader));
        }
        return result;
    }

    /// <summary>
    /// Get a single lead attribution by ID.
    /// </summary>
    public virtual async Task<LeadAttributionDto?> GetLeadAttributionByIdAsync(
        int tenantId, int id, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, tenant_id, customer_phone, chat_id,
                   utm_source, utm_medium, utm_campaign, utm_content, utm_term,
                   meta_click_id, lead_source, lead_labels,
                   conversion_status, conversion_value, converted_at, created_at
            FROM lead_attributions
            WHERE tenant_id = @tid AND id = @id";

        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapLeadAttribution(reader) : null;
    }

    // ============================================================
    // LEAD STATUS UPDATE
    // ============================================================

    /// <summary>
    /// Update lead conversion status and optional value.
    /// </summary>
    public virtual async Task<bool> UpdateLeadStatusAsync(
        int tenantId, int id, LeadStatusUpdateRequest req, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE lead_attributions
            SET conversion_status = @status,
                conversion_value = COALESCE(@value, conversion_value),
                converted_at = CASE WHEN @status = 'converted' THEN NOW() ELSE converted_at END
            WHERE tenant_id = @tid AND id = @id";

        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("status", req.ConversionStatus);
        cmd.Parameters.AddWithValue("value", (object?)req.ConversionValue ?? DBNull.Value);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ============================================================
    // ATTRIBUTION SUMMARY
    // ============================================================

    /// <summary>
    /// Get attribution summary with source and campaign breakdowns.
    /// </summary>
    public virtual async Task<AttributionSummaryDto> GetAttributionSummaryAsync(
        int tenantId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var summary = new AttributionSummaryDto
        {
            TenantId = tenantId,
            From = from.ToString("yyyy-MM-dd"),
            To = to.ToString("yyyy-MM-dd")
        };

        await using var conn = await _db.OpenConnectionAsync(ct);

        // Totals
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT
                    COUNT(*),
                    COUNT(*) FILTER (WHERE conversion_status = 'converted'),
                    COALESCE(SUM(conversion_value) FILTER (WHERE conversion_status = 'converted'), 0)
                FROM lead_attributions
                WHERE tenant_id = @tid
                  AND created_at >= @from_date::date::timestamptz
                  AND created_at < (@to_date::date + INTERVAL '1 day')::timestamptz";

            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("from_date", from.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("to_date", to.ToString("yyyy-MM-dd"));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                summary.TotalLeads = (int)reader.GetInt64(0);
                summary.ConvertedLeads = (int)reader.GetInt64(1);
                summary.TotalRevenue = reader.GetDecimal(2);
                summary.ConversionRate = summary.TotalLeads > 0
                    ? Math.Round((double)summary.ConvertedLeads / summary.TotalLeads * 100, 1)
                    : 0;
            }
        }

        // By source
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT
                    lead_source,
                    COUNT(*),
                    COUNT(*) FILTER (WHERE conversion_status = 'converted'),
                    COALESCE(SUM(conversion_value) FILTER (WHERE conversion_status = 'converted'), 0)
                FROM lead_attributions
                WHERE tenant_id = @tid
                  AND created_at >= @from_date::date::timestamptz
                  AND created_at < (@to_date::date + INTERVAL '1 day')::timestamptz
                GROUP BY lead_source
                ORDER BY COUNT(*) DESC";

            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("from_date", from.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("to_date", to.ToString("yyyy-MM-dd"));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var leadCount = (int)reader.GetInt64(1);
                var converted = (int)reader.GetInt64(2);
                summary.BySource.Add(new SourceBreakdownItem
                {
                    LeadSource = reader.GetString(0),
                    LeadCount = leadCount,
                    ConvertedCount = converted,
                    ConversionRate = leadCount > 0 ? Math.Round((double)converted / leadCount * 100, 1) : 0,
                    TotalRevenue = reader.GetDecimal(3)
                });
            }
        }

        // By campaign
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT
                    COALESCE(utm_campaign, 'N/A'),
                    lead_source,
                    COUNT(*),
                    COUNT(*) FILTER (WHERE conversion_status = 'converted'),
                    COALESCE(SUM(conversion_value) FILTER (WHERE conversion_status = 'converted'), 0)
                FROM lead_attributions
                WHERE tenant_id = @tid
                  AND created_at >= @from_date::date::timestamptz
                  AND created_at < (@to_date::date + INTERVAL '1 day')::timestamptz
                  AND utm_campaign IS NOT NULL
                GROUP BY COALESCE(utm_campaign, 'N/A'), lead_source
                ORDER BY COUNT(*) DESC
                LIMIT 50";

            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("from_date", from.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("to_date", to.ToString("yyyy-MM-dd"));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var leadCount = (int)reader.GetInt64(2);
                var converted = (int)reader.GetInt64(3);
                summary.ByCampaign.Add(new CampaignBreakdownItem
                {
                    UtmCampaign = reader.GetString(0),
                    LeadSource = reader.GetString(1),
                    LeadCount = leadCount,
                    ConvertedCount = converted,
                    ConversionRate = leadCount > 0 ? Math.Round((double)converted / leadCount * 100, 1) : 0,
                    TotalRevenue = reader.GetDecimal(4)
                });
            }
        }

        return summary;
    }

    // ============================================================
    // COST-PER-LEAD
    // ============================================================

    /// <summary>
    /// Get cost-per-lead by platform, joining ad_costs with lead_attributions.
    /// </summary>
    public virtual async Task<List<CostPerLeadDto>> GetCostPerLeadAsync(
        int tenantId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            WITH costs AS (
                SELECT platform,
                       SUM(cost_amount) AS total_cost
                FROM ad_costs
                WHERE tenant_id = @tid
                  AND period_start >= @from_date
                  AND period_end <= @to_date
                GROUP BY platform
            ),
            leads AS (
                SELECT
                    CASE
                        WHEN lead_source = 'meta_ad' THEN 'meta'
                        WHEN lead_source = 'google_ad' THEN 'google'
                        WHEN lead_source LIKE '%_ad' THEN REPLACE(lead_source, '_ad', '')
                        ELSE lead_source
                    END AS platform,
                    COUNT(*) AS lead_count,
                    COUNT(*) FILTER (WHERE conversion_status = 'converted') AS converted_count
                FROM lead_attributions
                WHERE tenant_id = @tid
                  AND created_at >= @from_date::date::timestamptz
                  AND created_at < (@to_date::date + INTERVAL '1 day')::timestamptz
                GROUP BY 1
            )
            SELECT
                COALESCE(c.platform, l.platform) AS platform,
                COALESCE(c.total_cost, 0) AS total_cost,
                COALESCE(l.lead_count, 0) AS lead_count,
                CASE WHEN COALESCE(l.lead_count, 0) > 0
                    THEN ROUND(COALESCE(c.total_cost, 0) / l.lead_count, 2)
                    ELSE 0 END AS cost_per_lead,
                COALESCE(l.converted_count, 0) AS converted_count,
                CASE WHEN COALESCE(l.converted_count, 0) > 0
                    THEN ROUND(COALESCE(c.total_cost, 0) / l.converted_count, 2)
                    ELSE 0 END AS cost_per_conversion
            FROM costs c
            FULL OUTER JOIN leads l ON c.platform = l.platform
            ORDER BY COALESCE(c.total_cost, 0) DESC";

        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("from_date", from.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("to_date", to.ToDateTime(TimeOnly.MinValue));

        var result = new List<CostPerLeadDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new CostPerLeadDto
            {
                Platform = reader.GetString(0),
                TotalCost = reader.GetDecimal(1),
                LeadCount = (int)reader.GetInt64(2),
                CostPerLead = reader.GetDecimal(3),
                ConvertedCount = (int)reader.GetInt64(4),
                CostPerConversion = reader.GetDecimal(5)
            });
        }
        return result;
    }

    // ============================================================
    // AD COSTS CRUD
    // ============================================================

    public virtual async Task<int> InsertAdCostAsync(
        int tenantId, AdCostCreateRequest req, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ad_costs (tenant_id, platform, campaign_name, cost_amount, currency, period_start, period_end)
            VALUES (@tid, @platform, @campaign, @cost, @currency, @period_start::date, @period_end::date)
            RETURNING id";

        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("platform", req.Platform);
        cmd.Parameters.AddWithValue("campaign", (object?)req.CampaignName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cost", req.CostAmount);
        cmd.Parameters.AddWithValue("currency", req.Currency);
        cmd.Parameters.AddWithValue("period_start", req.PeriodStart);
        cmd.Parameters.AddWithValue("period_end", req.PeriodEnd);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int id ? id : 0;
    }

    public virtual async Task<List<AdCostDto>> GetAdCostsAsync(
        int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, tenant_id, platform, campaign_name, cost_amount, currency,
                   period_start::text, period_end::text, created_at
            FROM ad_costs
            WHERE tenant_id = @tid
            ORDER BY period_start DESC
            LIMIT 200";

        cmd.Parameters.AddWithValue("tid", tenantId);

        var result = new List<AdCostDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new AdCostDto
            {
                Id = reader.GetInt32(0),
                TenantId = reader.GetInt32(1),
                Platform = reader.GetString(2),
                CampaignName = reader.IsDBNull(3) ? null : reader.GetString(3),
                CostAmount = reader.GetDecimal(4),
                Currency = reader.GetString(5),
                PeriodStart = reader.GetString(6),
                PeriodEnd = reader.GetString(7),
                CreatedAt = reader.GetDateTime(8)
            });
        }
        return result;
    }

    public virtual async Task<bool> DeleteAdCostAsync(
        int tenantId, int id, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ad_costs WHERE tenant_id = @tid AND id = @id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ============================================================
    // PRIVATE HELPERS
    // ============================================================

    private static LeadAttributionDto MapLeadAttribution(System.Data.Common.DbDataReader reader)
    {
        var labelsJson = reader.IsDBNull(11) ? "[]" : reader.GetString(11);
        var labels = JsonSerializer.Deserialize<List<string>>(labelsJson) ?? [];

        return new LeadAttributionDto
        {
            Id = reader.GetInt32(0),
            TenantId = reader.GetInt32(1),
            CustomerPhone = reader.GetString(2),
            ChatId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            UtmSource = reader.IsDBNull(4) ? null : reader.GetString(4),
            UtmMedium = reader.IsDBNull(5) ? null : reader.GetString(5),
            UtmCampaign = reader.IsDBNull(6) ? null : reader.GetString(6),
            UtmContent = reader.IsDBNull(7) ? null : reader.GetString(7),
            UtmTerm = reader.IsDBNull(8) ? null : reader.GetString(8),
            MetaClickId = reader.IsDBNull(9) ? null : reader.GetString(9),
            LeadSource = reader.GetString(10),
            LeadLabels = labels,
            ConversionStatus = reader.GetString(12),
            ConversionValue = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
            ConvertedAt = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
            CreatedAt = reader.GetDateTime(15)
        };
    }
}
