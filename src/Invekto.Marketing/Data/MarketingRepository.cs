using System.Security.Cryptography;
using Invekto.Shared.Data;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Marketing.Data;

/// <summary>
/// Repository for Marketing service tables: review_requests, referrals, medical_tourism_leads.
/// GR-3.21: Google Yorum + Referans Motoru.
/// GR-3.22: Medikal Turizm Lead Capture.
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
