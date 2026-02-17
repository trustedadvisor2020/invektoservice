using System.Security.Cryptography;
using Invekto.Shared.Data;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Marketing.Data;

/// <summary>
/// Repository for Marketing service tables.
/// GR-3.21: Google Yorum + Referans Motoru.
/// GR-3.22: Medikal Turizm Lead Capture.
/// GR-3.24: Proactive Review Rescue (review_risks, rescue_templates).
/// GR-3.25: Multilingual Medical Tourism (treatment_catalog, tourism_conversations).
/// All queries include tenant_id WHERE clause for multi-tenant isolation.
/// </summary>
public sealed class MarketingRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public MarketingRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ================================================================
    // REVIEW REQUESTS (GR-3.21)
    // ================================================================

    public async Task<int> CreateReviewRequestAsync(
        int tenantId, string patientPhone, string? patientName,
        string? treatmentType, short? satisfactionScore, string? reviewLinkUrl,
        string platform, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO review_requests
                (tenant_id, patient_phone, patient_name, treatment_type,
                 satisfaction_score, review_link_url, platform)
            VALUES (@tid, @phone, @name, @treatment, @score, @link, @platform)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", patientPhone);
        cmd.Parameters.AddWithValue("name", (object?)patientName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("treatment", (object?)treatmentType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("score", satisfactionScore.HasValue ? (object)satisfactionScore.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("link", (object?)reviewLinkUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("platform", platform);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int id ? id : Convert.ToInt32(result);
    }

    public async Task<List<ReviewRequestDto>> ListReviewRequestsAsync(
        int tenantId, string? status, string? platform, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, patient_phone, patient_name, treatment_type,
                   satisfaction_score, review_link_url, review_link_sent, review_posted,
                   review_rating, platform, status, created_at, updated_at
            FROM review_requests
            WHERE tenant_id = @tid
              AND (@status IS NULL OR status = @status)
              AND (@platform IS NULL OR platform = @platform)
            ORDER BY created_at DESC
            LIMIT 200";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("platform", (object?)platform ?? DBNull.Value);

        var results = new List<ReviewRequestDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(ReadReviewRequestDto(reader));
        return results;
    }

    public async Task<bool> MarkReviewSentAsync(
        int tenantId, int id, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE review_requests
            SET review_link_sent = TRUE, status = 'sent', updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id AND status = 'pending'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> MarkReviewPostedAsync(
        int tenantId, int id, short reviewRating, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE review_requests
            SET review_posted = TRUE, review_rating = @rating, status = 'posted', updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("rating", reviewRating);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<ReviewStatsDto> GetReviewStatsAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                COUNT(*) AS total,
                COUNT(*) FILTER (WHERE review_link_sent = TRUE) AS sent,
                COUNT(*) FILTER (WHERE review_posted = TRUE) AS posted,
                COALESCE(AVG(review_rating) FILTER (WHERE review_rating IS NOT NULL), 0) AS avg_rating
            FROM review_requests
            WHERE tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new ReviewStatsDto
            {
                Total = reader.GetInt64(reader.GetOrdinal("total")),
                Sent = reader.GetInt64(reader.GetOrdinal("sent")),
                Posted = reader.GetInt64(reader.GetOrdinal("posted")),
                AvgRating = Math.Round(reader.GetDouble(reader.GetOrdinal("avg_rating")), 1),
                PostRate = reader.GetInt64(reader.GetOrdinal("sent")) > 0
                    ? Math.Round((double)reader.GetInt64(reader.GetOrdinal("posted")) / reader.GetInt64(reader.GetOrdinal("sent")) * 100, 1)
                    : 0
            };
        }
        return new ReviewStatsDto();
    }

    // ================================================================
    // REFERRALS (GR-3.21)
    // ================================================================

    public async Task<(int Id, string Code)> CreateReferralAsync(
        int tenantId, string referrerPhone, string? referrerName,
        short discountPct, string? referrerReward, CancellationToken ct = default)
    {
        var code = GenerateReferralCode();

        const string sql = @"
            INSERT INTO referrals
                (tenant_id, referrer_phone, referrer_name, referral_code, discount_pct, referrer_reward)
            VALUES (@tid, @phone, @name, @code, @discount, @reward)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", referrerPhone);
        cmd.Parameters.AddWithValue("name", (object?)referrerName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("code", code);
        cmd.Parameters.AddWithValue("discount", discountPct);
        cmd.Parameters.AddWithValue("reward", (object?)referrerReward ?? DBNull.Value);

        var scalarResult = await cmd.ExecuteScalarAsync(ct);
        var id = scalarResult is int intId ? intId : Convert.ToInt32(scalarResult);
        return (id, code);
    }

    public async Task<List<ReferralDto>> ListReferralsAsync(
        int tenantId, string? status, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, referrer_phone, referrer_name, referee_phone, referee_name,
                   referral_code, discount_pct, referrer_reward, status, created_at, redeemed_at
            FROM referrals
            WHERE tenant_id = @tid
              AND (@status IS NULL OR status = @status)
            ORDER BY created_at DESC
            LIMIT 200";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);

        var results = new List<ReferralDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(ReadReferralDto(reader));
        return results;
    }

    public async Task<ReferralDto?> LookupReferralByCodeAsync(
        int tenantId, string code, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, referrer_phone, referrer_name, referee_phone, referee_name,
                   referral_code, discount_pct, referrer_reward, status, created_at, redeemed_at
            FROM referrals
            WHERE tenant_id = @tid AND referral_code = @code";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("code", code);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadReferralDto(reader);
        return null;
    }

    public async Task<bool> RedeemReferralAsync(
        int tenantId, int id, string refereePhone, string? refereeName,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE referrals
            SET referee_phone = @phone, referee_name = @name,
                status = 'redeemed', redeemed_at = NOW()
            WHERE tenant_id = @tid AND id = @id AND status = 'active'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("phone", refereePhone);
        cmd.Parameters.AddWithValue("name", (object?)refereeName ?? DBNull.Value);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ================================================================
    // MEDICAL TOURISM LEADS (GR-3.22)
    // ================================================================

    public async Task<int> CreateTourismLeadAsync(
        int tenantId, string patientPhone, string? patientName,
        string? patientCountry, string patientLang, string? treatmentInterest,
        bool accommodationNeeded, bool transferNeeded,
        string? budgetCurrency, decimal? budgetAmount, string? source,
        string? notes, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO medical_tourism_leads
                (tenant_id, patient_phone, patient_name, patient_country, patient_lang,
                 treatment_interest, accommodation_needed, transfer_needed,
                 budget_currency, budget_amount, source, notes)
            VALUES (@tid, @phone, @name, @country, @lang,
                    @treatment, @accommodation, @transfer,
                    @currency, @budget, @source, @notes)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", patientPhone);
        cmd.Parameters.AddWithValue("name", (object?)patientName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("country", (object?)patientCountry ?? DBNull.Value);
        cmd.Parameters.AddWithValue("lang", patientLang);
        cmd.Parameters.AddWithValue("treatment", (object?)treatmentInterest ?? DBNull.Value);
        cmd.Parameters.AddWithValue("accommodation", accommodationNeeded);
        cmd.Parameters.AddWithValue("transfer", transferNeeded);
        cmd.Parameters.AddWithValue("currency", (object?)budgetCurrency ?? DBNull.Value);
        cmd.Parameters.AddWithValue("budget", budgetAmount.HasValue ? (object)budgetAmount.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("source", (object?)source ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int id ? id : Convert.ToInt32(result);
    }

    public async Task<List<TourismLeadDto>> ListTourismLeadsAsync(
        int tenantId, string? status, string? country, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, patient_phone, patient_name, patient_country, patient_lang,
                   treatment_interest, accommodation_needed, transfer_needed,
                   budget_currency, budget_amount, source, notes, status, created_at, updated_at
            FROM medical_tourism_leads
            WHERE tenant_id = @tid
              AND (@status IS NULL OR status = @status)
              AND (@country IS NULL OR patient_country = @country)
            ORDER BY created_at DESC
            LIMIT 200";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("country", (object?)country ?? DBNull.Value);

        var results = new List<TourismLeadDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(ReadTourismLeadDto(reader));
        return results;
    }

    public async Task<TourismLeadDto?> GetTourismLeadAsync(
        int tenantId, int id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, patient_phone, patient_name, patient_country, patient_lang,
                   treatment_interest, accommodation_needed, transfer_needed,
                   budget_currency, budget_amount, source, notes, status, created_at, updated_at
            FROM medical_tourism_leads
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadTourismLeadDto(reader);
        return null;
    }

    public async Task<bool> UpdateTourismLeadAsync(
        int tenantId, int id, string? status, string? notes,
        string? patientName, string? budgetCurrency, decimal? budgetAmount,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE medical_tourism_leads
            SET status = COALESCE(@status, status),
                notes = COALESCE(@notes, notes),
                patient_name = COALESCE(@name, patient_name),
                budget_currency = COALESCE(@currency, budget_currency),
                budget_amount = COALESCE(@budget, budget_amount),
                updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("name", (object?)patientName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("currency", (object?)budgetCurrency ?? DBNull.Value);
        cmd.Parameters.AddWithValue("budget", budgetAmount.HasValue ? (object)budgetAmount.Value : DBNull.Value);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<TourismStatsDto> GetTourismStatsAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                COUNT(*) AS total,
                COUNT(*) FILTER (WHERE status = 'new') AS new_count,
                COUNT(*) FILTER (WHERE status = 'contacted') AS contacted,
                COUNT(*) FILTER (WHERE status = 'consultation') AS consultation,
                COUNT(*) FILTER (WHERE status = 'booked') AS booked,
                COUNT(*) FILTER (WHERE status = 'treated') AS treated,
                COUNT(*) FILTER (WHERE status = 'lost') AS lost
            FROM medical_tourism_leads
            WHERE tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var total = reader.GetInt64(reader.GetOrdinal("total"));
            var booked = reader.GetInt64(reader.GetOrdinal("booked"));
            var treated = reader.GetInt64(reader.GetOrdinal("treated"));
            return new TourismStatsDto
            {
                Total = total,
                NewCount = reader.GetInt64(reader.GetOrdinal("new_count")),
                Contacted = reader.GetInt64(reader.GetOrdinal("contacted")),
                Consultation = reader.GetInt64(reader.GetOrdinal("consultation")),
                Booked = booked,
                Treated = treated,
                Lost = reader.GetInt64(reader.GetOrdinal("lost")),
                ConversionRate = total > 0
                    ? Math.Round((double)(booked + treated) / total * 100, 1)
                    : 0
            };
        }
        return new TourismStatsDto();
    }

    // ================================================================
    // REVIEW RISKS (GR-3.24)
    // ================================================================

    public async Task<int> CreateReviewRiskAsync(
        int tenantId, string customerPhone, string? conversationId,
        short riskScore, string riskLevel, string? triggerReason,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO review_risks
                (tenant_id, customer_phone, conversation_id, risk_score, risk_level, trigger_reason)
            VALUES (@tid, @phone, @convId, @score, @level, @reason)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", customerPhone);
        cmd.Parameters.AddWithValue("convId", (object?)conversationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("score", riskScore);
        cmd.Parameters.AddWithValue("level", riskLevel);
        cmd.Parameters.AddWithValue("reason", (object?)triggerReason ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int id ? id : Convert.ToInt32(result);
    }

    public async Task<List<ReviewRiskDto>> ListReviewRisksAsync(
        int tenantId, string? riskLevel, string? rescueStatus, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, customer_phone, conversation_id, risk_score, risk_level,
                   trigger_reason, rescue_status, rescue_strategy, rescue_cost,
                   customer_response, review_posted, review_rating,
                   created_at, resolved_at, updated_at
            FROM review_risks
            WHERE tenant_id = @tid
              AND (@level IS NULL OR risk_level = @level)
              AND (@status IS NULL OR rescue_status = @status)
            ORDER BY created_at DESC
            LIMIT 200";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("level", (object?)riskLevel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", (object?)rescueStatus ?? DBNull.Value);

        var results = new List<ReviewRiskDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(ReadReviewRiskDto(reader));
        return results;
    }

    public async Task<bool> UpdateReviewRiskAsync(
        int tenantId, int id, string? rescueStatus, string? rescueStrategy,
        decimal? rescueCost, string? customerResponse, bool? reviewPosted,
        short? reviewRating, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE review_risks
            SET rescue_status = COALESCE(@rescueStatus, rescue_status),
                rescue_strategy = COALESCE(@strategy, rescue_strategy),
                rescue_cost = COALESCE(@cost, rescue_cost),
                customer_response = COALESCE(@response, customer_response),
                review_posted = COALESCE(@posted, review_posted),
                review_rating = COALESCE(@rating, review_rating),
                resolved_at = CASE WHEN @rescueStatus IN ('rescued', 'failed', 'expired') THEN NOW() ELSE resolved_at END,
                updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("rescueStatus", (object?)rescueStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("strategy", (object?)rescueStrategy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cost", rescueCost.HasValue ? (object)rescueCost.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("response", (object?)customerResponse ?? DBNull.Value);
        cmd.Parameters.AddWithValue("posted", reviewPosted.HasValue ? (object)reviewPosted.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("rating", reviewRating.HasValue ? (object)reviewRating.Value : DBNull.Value);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<RescueStatsDto> GetRescueStatsAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                COUNT(*) AS total,
                COUNT(*) FILTER (WHERE rescue_status = 'pending') AS pending,
                COUNT(*) FILTER (WHERE rescue_status = 'in_progress') AS in_progress,
                COUNT(*) FILTER (WHERE rescue_status = 'rescued') AS rescued,
                COUNT(*) FILTER (WHERE rescue_status = 'failed') AS failed,
                COUNT(*) FILTER (WHERE risk_level = 'critical') AS critical_count,
                COUNT(*) FILTER (WHERE risk_level = 'high') AS high_count,
                COUNT(*) FILTER (WHERE review_posted = TRUE) AS reviews_posted,
                COALESCE(AVG(review_rating) FILTER (WHERE review_rating IS NOT NULL), 0) AS avg_review_rating,
                COALESCE(SUM(rescue_cost) FILTER (WHERE rescue_cost IS NOT NULL), 0) AS total_rescue_cost
            FROM review_risks
            WHERE tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var total = reader.GetInt64(reader.GetOrdinal("total"));
            var rescued = reader.GetInt64(reader.GetOrdinal("rescued"));
            return new RescueStatsDto
            {
                Total = total,
                Pending = reader.GetInt64(reader.GetOrdinal("pending")),
                InProgress = reader.GetInt64(reader.GetOrdinal("in_progress")),
                Rescued = rescued,
                Failed = reader.GetInt64(reader.GetOrdinal("failed")),
                CriticalCount = reader.GetInt64(reader.GetOrdinal("critical_count")),
                HighCount = reader.GetInt64(reader.GetOrdinal("high_count")),
                ReviewsPosted = reader.GetInt64(reader.GetOrdinal("reviews_posted")),
                AvgReviewRating = Math.Round(reader.GetDouble(reader.GetOrdinal("avg_review_rating")), 1),
                TotalRescueCost = reader.GetDecimal(reader.GetOrdinal("total_rescue_cost")),
                RescueRate = total > 0 ? Math.Round((double)rescued / total * 100, 1) : 0
            };
        }
        return new RescueStatsDto();
    }

    // ================================================================
    // RESCUE TEMPLATES (GR-3.24)
    // ================================================================

    public async Task<int> CreateRescueTemplateAsync(
        int tenantId, string templateName, string riskLevel, string strategy,
        string messageTemplate, short? maxDiscountPct, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO rescue_templates
                (tenant_id, template_name, risk_level, strategy, message_template, max_discount_pct)
            VALUES (@tid, @name, @level, @strategy, @template, @discount)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("name", templateName);
        cmd.Parameters.AddWithValue("level", riskLevel);
        cmd.Parameters.AddWithValue("strategy", strategy);
        cmd.Parameters.AddWithValue("template", messageTemplate);
        cmd.Parameters.AddWithValue("discount", maxDiscountPct.HasValue ? (object)maxDiscountPct.Value : DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int id ? id : Convert.ToInt32(result);
    }

    public async Task<List<RescueTemplateDto>> ListRescueTemplatesAsync(
        int tenantId, string? riskLevel, bool activeOnly = true, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, template_name, risk_level, strategy,
                   message_template, max_discount_pct, is_active, created_at, updated_at
            FROM rescue_templates
            WHERE tenant_id = @tid
              AND (@level IS NULL OR risk_level = @level)
              AND (@activeOnly = FALSE OR is_active = TRUE)
            ORDER BY risk_level, strategy
            LIMIT 200";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("level", (object?)riskLevel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("activeOnly", activeOnly);

        var results = new List<RescueTemplateDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(ReadRescueTemplateDto(reader));
        return results;
    }

    public async Task<bool> UpdateRescueTemplateAsync(
        int tenantId, int id, string? templateName, string? messageTemplate,
        short? maxDiscountPct, bool? isActive, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE rescue_templates
            SET template_name = COALESCE(@name, template_name),
                message_template = COALESCE(@template, message_template),
                max_discount_pct = COALESCE(@discount, max_discount_pct),
                is_active = COALESCE(@active, is_active),
                updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", (object?)templateName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("template", (object?)messageTemplate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("discount", maxDiscountPct.HasValue ? (object)maxDiscountPct.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("active", isActive.HasValue ? (object)isActive.Value : DBNull.Value);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeactivateRescueTemplateAsync(
        int tenantId, int id, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE rescue_templates
            SET is_active = FALSE, updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id AND is_active = TRUE";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ================================================================
    // TREATMENT CATALOG (GR-3.25)
    // ================================================================

    public async Task<int> CreateTreatmentAsync(
        int tenantId, string treatmentName, string? treatmentNameEn, string? category,
        decimal? priceMin, decimal? priceMax, string priceCurrency,
        short? durationDays, short? recoveryDays,
        string? descriptionTr, string? descriptionEn, string? packageIncludes,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO treatment_catalog
                (tenant_id, treatment_name, treatment_name_en, category,
                 price_min, price_max, price_currency, duration_days, recovery_days,
                 description_tr, description_en, package_includes)
            VALUES (@tid, @name, @nameEn, @category,
                    @priceMin, @priceMax, @currency, @duration, @recovery,
                    @descTr, @descEn, @package)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("name", treatmentName);
        cmd.Parameters.AddWithValue("nameEn", (object?)treatmentNameEn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("category", (object?)category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("priceMin", priceMin.HasValue ? (object)priceMin.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("priceMax", priceMax.HasValue ? (object)priceMax.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("currency", priceCurrency);
        cmd.Parameters.AddWithValue("duration", durationDays.HasValue ? (object)durationDays.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("recovery", recoveryDays.HasValue ? (object)recoveryDays.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("descTr", (object?)descriptionTr ?? DBNull.Value);
        cmd.Parameters.AddWithValue("descEn", (object?)descriptionEn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("package", (object?)packageIncludes ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int id ? id : Convert.ToInt32(result);
    }

    public async Task<List<TreatmentDto>> ListTreatmentsAsync(
        int tenantId, string? category, bool activeOnly = true, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, treatment_name, treatment_name_en, category,
                   price_min, price_max, price_currency, duration_days, recovery_days,
                   description_tr, description_en, package_includes, is_active,
                   created_at, updated_at
            FROM treatment_catalog
            WHERE tenant_id = @tid
              AND (@category IS NULL OR category = @category)
              AND (@activeOnly = FALSE OR is_active = TRUE)
            ORDER BY category, treatment_name
            LIMIT 200";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("category", (object?)category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("activeOnly", activeOnly);

        var results = new List<TreatmentDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(ReadTreatmentDto(reader));
        return results;
    }

    public async Task<bool> UpdateTreatmentAsync(
        int tenantId, int id, string? treatmentName, string? treatmentNameEn,
        string? category, decimal? priceMin, decimal? priceMax, string? priceCurrency,
        short? durationDays, short? recoveryDays,
        string? descriptionTr, string? descriptionEn, string? packageIncludes,
        bool? isActive, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE treatment_catalog
            SET treatment_name = COALESCE(@name, treatment_name),
                treatment_name_en = COALESCE(@nameEn, treatment_name_en),
                category = COALESCE(@category, category),
                price_min = COALESCE(@priceMin, price_min),
                price_max = COALESCE(@priceMax, price_max),
                price_currency = COALESCE(@currency, price_currency),
                duration_days = COALESCE(@duration, duration_days),
                recovery_days = COALESCE(@recovery, recovery_days),
                description_tr = COALESCE(@descTr, description_tr),
                description_en = COALESCE(@descEn, description_en),
                package_includes = COALESCE(@package, package_includes),
                is_active = COALESCE(@active, is_active),
                updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", (object?)treatmentName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("nameEn", (object?)treatmentNameEn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("category", (object?)category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("priceMin", priceMin.HasValue ? (object)priceMin.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("priceMax", priceMax.HasValue ? (object)priceMax.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("currency", (object?)priceCurrency ?? DBNull.Value);
        cmd.Parameters.AddWithValue("duration", durationDays.HasValue ? (object)durationDays.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("recovery", recoveryDays.HasValue ? (object)recoveryDays.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("descTr", (object?)descriptionTr ?? DBNull.Value);
        cmd.Parameters.AddWithValue("descEn", (object?)descriptionEn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("package", (object?)packageIncludes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("active", isActive.HasValue ? (object)isActive.Value : DBNull.Value);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeactivateTreatmentAsync(
        int tenantId, int id, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE treatment_catalog
            SET is_active = FALSE, updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id AND is_active = TRUE";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<List<TreatmentDto>> GetActiveTreatmentsForResponseAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, treatment_name, treatment_name_en, category,
                   price_min, price_max, price_currency, duration_days, recovery_days,
                   description_tr, description_en, package_includes, is_active,
                   created_at, updated_at
            FROM treatment_catalog
            WHERE tenant_id = @tid AND is_active = TRUE
            ORDER BY category, treatment_name";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var results = new List<TreatmentDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(ReadTreatmentDto(reader));
        return results;
    }

    // ================================================================
    // TOURISM CONVERSATIONS (GR-3.25)
    // ================================================================

    public async Task<int> CreateTourismConversationAsync(
        int tenantId, int? leadId, string patientPhone, string patientLang,
        string? patientCountry, string patientMessage, string? detectedIntent,
        string? aiResponse, string? aiResponseLang, string? trTranslation,
        string? treatmentInterest, bool responseGenerated,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO tourism_conversations
                (tenant_id, lead_id, patient_phone, patient_lang, patient_country,
                 patient_message, detected_intent, ai_response, ai_response_lang,
                 tr_translation, treatment_interest, response_generated)
            VALUES (@tid, @leadId, @phone, @lang, @country,
                    @message, @intent, @aiResp, @aiLang,
                    @trTrans, @treatment, @generated)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("leadId", leadId.HasValue ? (object)leadId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("phone", patientPhone);
        cmd.Parameters.AddWithValue("lang", patientLang);
        cmd.Parameters.AddWithValue("country", (object?)patientCountry ?? DBNull.Value);
        cmd.Parameters.AddWithValue("message", patientMessage);
        cmd.Parameters.AddWithValue("intent", (object?)detectedIntent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("aiResp", (object?)aiResponse ?? DBNull.Value);
        cmd.Parameters.AddWithValue("aiLang", (object?)aiResponseLang ?? DBNull.Value);
        cmd.Parameters.AddWithValue("trTrans", (object?)trTranslation ?? DBNull.Value);
        cmd.Parameters.AddWithValue("treatment", (object?)treatmentInterest ?? DBNull.Value);
        cmd.Parameters.AddWithValue("generated", responseGenerated);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int id ? id : Convert.ToInt32(result);
    }

    public async Task<List<TourismConversationDto>> ListTourismConversationsAsync(
        int tenantId, string? lang, string? country, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, lead_id, patient_phone, patient_lang, patient_country,
                   patient_message, detected_intent, ai_response, ai_response_lang,
                   tr_translation, treatment_interest, response_generated, created_at
            FROM tourism_conversations
            WHERE tenant_id = @tid
              AND (@lang IS NULL OR patient_lang = @lang)
              AND (@country IS NULL OR patient_country = @country)
            ORDER BY created_at DESC
            LIMIT 200";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lang", (object?)lang ?? DBNull.Value);
        cmd.Parameters.AddWithValue("country", (object?)country ?? DBNull.Value);

        var results = new List<TourismConversationDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(ReadTourismConversationDto(reader));
        return results;
    }

    public async Task<TourismConvStatsDto> GetTourismConversationStatsAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                COUNT(*) AS total,
                COUNT(*) FILTER (WHERE response_generated = TRUE) AS responded,
                COUNT(DISTINCT patient_lang) AS unique_languages,
                COUNT(DISTINCT patient_country) FILTER (WHERE patient_country IS NOT NULL) AS unique_countries
            FROM tourism_conversations
            WHERE tenant_id = @tid";

        const string langBreakdownSql = @"
            SELECT patient_lang, COUNT(*) AS count
            FROM tourism_conversations
            WHERE tenant_id = @tid
            GROUP BY patient_lang
            ORDER BY count DESC
            LIMIT 20";

        await using var conn = await _db.OpenConnectionAsync(ct);

        // Main stats
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        long total = 0, responded = 0, uniqueLangs = 0, uniqueCountries = 0;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                total = reader.GetInt64(reader.GetOrdinal("total"));
                responded = reader.GetInt64(reader.GetOrdinal("responded"));
                uniqueLangs = reader.GetInt64(reader.GetOrdinal("unique_languages"));
                uniqueCountries = reader.GetInt64(reader.GetOrdinal("unique_countries"));
            }
        }

        // Language breakdown
        await using var cmd2 = new NpgsqlCommand(langBreakdownSql, conn);
        cmd2.Parameters.AddWithValue("tid", tenantId);

        var langBreakdown = new Dictionary<string, long>();
        await using (var reader2 = await cmd2.ExecuteReaderAsync(ct))
        {
            while (await reader2.ReadAsync(ct))
                langBreakdown[reader2.GetString(0)] = reader2.GetInt64(1);
        }

        return new TourismConvStatsDto
        {
            Total = total,
            Responded = responded,
            ResponseRate = total > 0 ? Math.Round((double)responded / total * 100, 1) : 0,
            UniqueLanguages = uniqueLangs,
            UniqueCountries = uniqueCountries,
            LanguageBreakdown = langBreakdown
        };
    }

    // ================================================================
    // HELPERS
    // ================================================================

    /// <summary>
    /// Generate crypto-random 8-char alphanumeric referral code.
    /// Format: REF-XXXXXXXX (uppercase letters + digits, no ambiguous chars I/O/0/1).
    /// </summary>
    internal static string GenerateReferralCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I,O,0,1
        Span<byte> randomBytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(randomBytes);

        var code = new char[8];
        for (int i = 0; i < 8; i++)
            code[i] = chars[randomBytes[i] % chars.Length];

        return $"REF-{new string(code)}";
    }

    private static ReviewRequestDto ReadReviewRequestDto(NpgsqlDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("id")),
        TenantId = r.GetInt32(r.GetOrdinal("tenant_id")),
        PatientPhone = r.GetString(r.GetOrdinal("patient_phone")),
        PatientName = r.IsDBNull(r.GetOrdinal("patient_name")) ? null : r.GetString(r.GetOrdinal("patient_name")),
        TreatmentType = r.IsDBNull(r.GetOrdinal("treatment_type")) ? null : r.GetString(r.GetOrdinal("treatment_type")),
        SatisfactionScore = r.IsDBNull(r.GetOrdinal("satisfaction_score")) ? null : r.GetInt16(r.GetOrdinal("satisfaction_score")),
        ReviewLinkUrl = r.IsDBNull(r.GetOrdinal("review_link_url")) ? null : r.GetString(r.GetOrdinal("review_link_url")),
        ReviewLinkSent = r.GetBoolean(r.GetOrdinal("review_link_sent")),
        ReviewPosted = r.GetBoolean(r.GetOrdinal("review_posted")),
        ReviewRating = r.IsDBNull(r.GetOrdinal("review_rating")) ? null : r.GetInt16(r.GetOrdinal("review_rating")),
        Platform = r.GetString(r.GetOrdinal("platform")),
        Status = r.GetString(r.GetOrdinal("status")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        UpdatedAt = r.GetDateTime(r.GetOrdinal("updated_at"))
    };

    private static ReferralDto ReadReferralDto(NpgsqlDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("id")),
        TenantId = r.GetInt32(r.GetOrdinal("tenant_id")),
        ReferrerPhone = r.GetString(r.GetOrdinal("referrer_phone")),
        ReferrerName = r.IsDBNull(r.GetOrdinal("referrer_name")) ? null : r.GetString(r.GetOrdinal("referrer_name")),
        RefereePhone = r.IsDBNull(r.GetOrdinal("referee_phone")) ? null : r.GetString(r.GetOrdinal("referee_phone")),
        RefereeName = r.IsDBNull(r.GetOrdinal("referee_name")) ? null : r.GetString(r.GetOrdinal("referee_name")),
        ReferralCode = r.GetString(r.GetOrdinal("referral_code")),
        DiscountPct = r.GetInt16(r.GetOrdinal("discount_pct")),
        ReferrerReward = r.IsDBNull(r.GetOrdinal("referrer_reward")) ? null : r.GetString(r.GetOrdinal("referrer_reward")),
        Status = r.GetString(r.GetOrdinal("status")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        RedeemedAt = r.IsDBNull(r.GetOrdinal("redeemed_at")) ? null : r.GetDateTime(r.GetOrdinal("redeemed_at"))
    };

    private static ReviewRiskDto ReadReviewRiskDto(NpgsqlDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("id")),
        TenantId = r.GetInt32(r.GetOrdinal("tenant_id")),
        CustomerPhone = r.GetString(r.GetOrdinal("customer_phone")),
        ConversationId = r.IsDBNull(r.GetOrdinal("conversation_id")) ? null : r.GetString(r.GetOrdinal("conversation_id")),
        RiskScore = r.GetInt16(r.GetOrdinal("risk_score")),
        RiskLevel = r.GetString(r.GetOrdinal("risk_level")),
        TriggerReason = r.IsDBNull(r.GetOrdinal("trigger_reason")) ? null : r.GetString(r.GetOrdinal("trigger_reason")),
        RescueStatus = r.GetString(r.GetOrdinal("rescue_status")),
        RescueStrategy = r.IsDBNull(r.GetOrdinal("rescue_strategy")) ? null : r.GetString(r.GetOrdinal("rescue_strategy")),
        RescueCost = r.IsDBNull(r.GetOrdinal("rescue_cost")) ? null : r.GetDecimal(r.GetOrdinal("rescue_cost")),
        CustomerResponse = r.IsDBNull(r.GetOrdinal("customer_response")) ? null : r.GetString(r.GetOrdinal("customer_response")),
        ReviewPosted = r.GetBoolean(r.GetOrdinal("review_posted")),
        ReviewRating = r.IsDBNull(r.GetOrdinal("review_rating")) ? null : r.GetInt16(r.GetOrdinal("review_rating")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        ResolvedAt = r.IsDBNull(r.GetOrdinal("resolved_at")) ? null : r.GetDateTime(r.GetOrdinal("resolved_at")),
        UpdatedAt = r.GetDateTime(r.GetOrdinal("updated_at"))
    };

    private static RescueTemplateDto ReadRescueTemplateDto(NpgsqlDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("id")),
        TenantId = r.GetInt32(r.GetOrdinal("tenant_id")),
        TemplateName = r.GetString(r.GetOrdinal("template_name")),
        RiskLevel = r.GetString(r.GetOrdinal("risk_level")),
        Strategy = r.GetString(r.GetOrdinal("strategy")),
        MessageTemplate = r.GetString(r.GetOrdinal("message_template")),
        MaxDiscountPct = r.IsDBNull(r.GetOrdinal("max_discount_pct")) ? null : r.GetInt16(r.GetOrdinal("max_discount_pct")),
        IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        UpdatedAt = r.GetDateTime(r.GetOrdinal("updated_at"))
    };

    private static TreatmentDto ReadTreatmentDto(NpgsqlDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("id")),
        TenantId = r.GetInt32(r.GetOrdinal("tenant_id")),
        TreatmentName = r.GetString(r.GetOrdinal("treatment_name")),
        TreatmentNameEn = r.IsDBNull(r.GetOrdinal("treatment_name_en")) ? null : r.GetString(r.GetOrdinal("treatment_name_en")),
        Category = r.IsDBNull(r.GetOrdinal("category")) ? null : r.GetString(r.GetOrdinal("category")),
        PriceMin = r.IsDBNull(r.GetOrdinal("price_min")) ? null : r.GetDecimal(r.GetOrdinal("price_min")),
        PriceMax = r.IsDBNull(r.GetOrdinal("price_max")) ? null : r.GetDecimal(r.GetOrdinal("price_max")),
        PriceCurrency = r.GetString(r.GetOrdinal("price_currency")),
        DurationDays = r.IsDBNull(r.GetOrdinal("duration_days")) ? null : r.GetInt16(r.GetOrdinal("duration_days")),
        RecoveryDays = r.IsDBNull(r.GetOrdinal("recovery_days")) ? null : r.GetInt16(r.GetOrdinal("recovery_days")),
        DescriptionTr = r.IsDBNull(r.GetOrdinal("description_tr")) ? null : r.GetString(r.GetOrdinal("description_tr")),
        DescriptionEn = r.IsDBNull(r.GetOrdinal("description_en")) ? null : r.GetString(r.GetOrdinal("description_en")),
        PackageIncludes = r.IsDBNull(r.GetOrdinal("package_includes")) ? null : r.GetString(r.GetOrdinal("package_includes")),
        IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        UpdatedAt = r.GetDateTime(r.GetOrdinal("updated_at"))
    };

    private static TourismConversationDto ReadTourismConversationDto(NpgsqlDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("id")),
        TenantId = r.GetInt32(r.GetOrdinal("tenant_id")),
        LeadId = r.IsDBNull(r.GetOrdinal("lead_id")) ? null : r.GetInt32(r.GetOrdinal("lead_id")),
        PatientPhone = r.GetString(r.GetOrdinal("patient_phone")),
        PatientLang = r.GetString(r.GetOrdinal("patient_lang")),
        PatientCountry = r.IsDBNull(r.GetOrdinal("patient_country")) ? null : r.GetString(r.GetOrdinal("patient_country")),
        PatientMessage = r.GetString(r.GetOrdinal("patient_message")),
        DetectedIntent = r.IsDBNull(r.GetOrdinal("detected_intent")) ? null : r.GetString(r.GetOrdinal("detected_intent")),
        AiResponse = r.IsDBNull(r.GetOrdinal("ai_response")) ? null : r.GetString(r.GetOrdinal("ai_response")),
        AiResponseLang = r.IsDBNull(r.GetOrdinal("ai_response_lang")) ? null : r.GetString(r.GetOrdinal("ai_response_lang")),
        TrTranslation = r.IsDBNull(r.GetOrdinal("tr_translation")) ? null : r.GetString(r.GetOrdinal("tr_translation")),
        TreatmentInterest = r.IsDBNull(r.GetOrdinal("treatment_interest")) ? null : r.GetString(r.GetOrdinal("treatment_interest")),
        ResponseGenerated = r.GetBoolean(r.GetOrdinal("response_generated")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("created_at"))
    };

    private static TourismLeadDto ReadTourismLeadDto(NpgsqlDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("id")),
        TenantId = r.GetInt32(r.GetOrdinal("tenant_id")),
        PatientPhone = r.GetString(r.GetOrdinal("patient_phone")),
        PatientName = r.IsDBNull(r.GetOrdinal("patient_name")) ? null : r.GetString(r.GetOrdinal("patient_name")),
        PatientCountry = r.IsDBNull(r.GetOrdinal("patient_country")) ? null : r.GetString(r.GetOrdinal("patient_country")),
        PatientLang = r.GetString(r.GetOrdinal("patient_lang")),
        TreatmentInterest = r.IsDBNull(r.GetOrdinal("treatment_interest")) ? null : r.GetString(r.GetOrdinal("treatment_interest")),
        AccommodationNeeded = r.GetBoolean(r.GetOrdinal("accommodation_needed")),
        TransferNeeded = r.GetBoolean(r.GetOrdinal("transfer_needed")),
        BudgetCurrency = r.IsDBNull(r.GetOrdinal("budget_currency")) ? null : r.GetString(r.GetOrdinal("budget_currency")),
        BudgetAmount = r.IsDBNull(r.GetOrdinal("budget_amount")) ? null : r.GetDecimal(r.GetOrdinal("budget_amount")),
        Source = r.IsDBNull(r.GetOrdinal("source")) ? null : r.GetString(r.GetOrdinal("source")),
        Notes = r.IsDBNull(r.GetOrdinal("notes")) ? null : r.GetString(r.GetOrdinal("notes")),
        Status = r.GetString(r.GetOrdinal("status")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        UpdatedAt = r.GetDateTime(r.GetOrdinal("updated_at"))
    };
}

// ================================================================
// DTOs
// ================================================================

public sealed class ReviewRequestDto
{
    public int Id { get; init; }
    public int TenantId { get; init; }
    public string PatientPhone { get; init; } = "";
    public string? PatientName { get; init; }
    public string? TreatmentType { get; init; }
    public short? SatisfactionScore { get; init; }
    public string? ReviewLinkUrl { get; init; }
    public bool ReviewLinkSent { get; init; }
    public bool ReviewPosted { get; init; }
    public short? ReviewRating { get; init; }
    public string Platform { get; init; } = "google";
    public string Status { get; init; } = "pending";
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class ReviewStatsDto
{
    public long Total { get; init; }
    public long Sent { get; init; }
    public long Posted { get; init; }
    public double AvgRating { get; init; }
    public double PostRate { get; init; }
}

public sealed class ReferralDto
{
    public int Id { get; init; }
    public int TenantId { get; init; }
    public string ReferrerPhone { get; init; } = "";
    public string? ReferrerName { get; init; }
    public string? RefereePhone { get; init; }
    public string? RefereeName { get; init; }
    public string ReferralCode { get; init; } = "";
    public short DiscountPct { get; init; }
    public string? ReferrerReward { get; init; }
    public string Status { get; init; } = "active";
    public DateTime CreatedAt { get; init; }
    public DateTime? RedeemedAt { get; init; }
}

public sealed class TourismLeadDto
{
    public int Id { get; init; }
    public int TenantId { get; init; }
    public string PatientPhone { get; init; } = "";
    public string? PatientName { get; init; }
    public string? PatientCountry { get; init; }
    public string PatientLang { get; init; } = "en";
    public string? TreatmentInterest { get; init; }
    public bool AccommodationNeeded { get; init; }
    public bool TransferNeeded { get; init; }
    public string? BudgetCurrency { get; init; }
    public decimal? BudgetAmount { get; init; }
    public string? Source { get; init; }
    public string? Notes { get; init; }
    public string Status { get; init; } = "new";
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class TourismStatsDto
{
    public long Total { get; init; }
    public long NewCount { get; init; }
    public long Contacted { get; init; }
    public long Consultation { get; init; }
    public long Booked { get; init; }
    public long Treated { get; init; }
    public long Lost { get; init; }
    public double ConversionRate { get; init; }
}

// ================================================================
// GR-3.24 DTOs
// ================================================================

public sealed class ReviewRiskDto
{
    public int Id { get; init; }
    public int TenantId { get; init; }
    public string CustomerPhone { get; init; } = "";
    public string? ConversationId { get; init; }
    public short RiskScore { get; init; }
    public string RiskLevel { get; init; } = "low";
    public string? TriggerReason { get; init; }
    public string RescueStatus { get; init; } = "pending";
    public string? RescueStrategy { get; init; }
    public decimal? RescueCost { get; init; }
    public string? CustomerResponse { get; init; }
    public bool ReviewPosted { get; init; }
    public short? ReviewRating { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class RescueStatsDto
{
    public long Total { get; init; }
    public long Pending { get; init; }
    public long InProgress { get; init; }
    public long Rescued { get; init; }
    public long Failed { get; init; }
    public long CriticalCount { get; init; }
    public long HighCount { get; init; }
    public long ReviewsPosted { get; init; }
    public double AvgReviewRating { get; init; }
    public decimal TotalRescueCost { get; init; }
    public double RescueRate { get; init; }
}

public sealed class RescueTemplateDto
{
    public int Id { get; init; }
    public int TenantId { get; init; }
    public string TemplateName { get; init; } = "";
    public string RiskLevel { get; init; } = "";
    public string Strategy { get; init; } = "";
    public string MessageTemplate { get; init; } = "";
    public short? MaxDiscountPct { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

// ================================================================
// GR-3.25 DTOs
// ================================================================

public sealed class TreatmentDto
{
    public int Id { get; init; }
    public int TenantId { get; init; }
    public string TreatmentName { get; init; } = "";
    public string? TreatmentNameEn { get; init; }
    public string? Category { get; init; }
    public decimal? PriceMin { get; init; }
    public decimal? PriceMax { get; init; }
    public string PriceCurrency { get; init; } = "EUR";
    public short? DurationDays { get; init; }
    public short? RecoveryDays { get; init; }
    public string? DescriptionTr { get; init; }
    public string? DescriptionEn { get; init; }
    public string? PackageIncludes { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class TourismConversationDto
{
    public int Id { get; init; }
    public int TenantId { get; init; }
    public int? LeadId { get; init; }
    public string PatientPhone { get; init; } = "";
    public string PatientLang { get; init; } = "";
    public string? PatientCountry { get; init; }
    public string PatientMessage { get; init; } = "";
    public string? DetectedIntent { get; init; }
    public string? AiResponse { get; init; }
    public string? AiResponseLang { get; init; }
    public string? TrTranslation { get; init; }
    public string? TreatmentInterest { get; init; }
    public bool ResponseGenerated { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class TourismConvStatsDto
{
    public long Total { get; init; }
    public long Responded { get; init; }
    public double ResponseRate { get; init; }
    public long UniqueLanguages { get; init; }
    public long UniqueCountries { get; init; }
    public Dictionary<string, long> LanguageBreakdown { get; init; } = new();
}
