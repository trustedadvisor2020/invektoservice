using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Models;

namespace Invekto.WhatsAppAnalytics.Data;

/// <summary>
/// Repository for wa_outcome_feedback (RI Faz 6: Ground Truth Flywheel).
/// </summary>
public sealed class FeedbackRepository
{
    private readonly AnalyticsConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public FeedbackRepository(AnalyticsConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Upsert feedback for a conversation. ON CONFLICT updates existing.
    /// </summary>
    public async Task UpsertFeedbackAsync(int tenantId, int userId, FeedbackRequest req, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO wa_outcome_feedback (tenant_id, conversation_id, original_label, is_agree, corrected_label, user_id)
            VALUES (@tid, @cid, @orig, @agree, @corrected, @uid)
            ON CONFLICT (tenant_id, conversation_id) DO UPDATE SET
                is_agree = EXCLUDED.is_agree,
                corrected_label = EXCLUDED.corrected_label,
                created_at = NOW()";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("cid", req.ConversationId);
        cmd.Parameters.AddWithValue("orig", req.OriginalLabel);
        cmd.Parameters.AddWithValue("agree", req.IsAgree);
        cmd.Parameters.AddWithValue("corrected", (object?)req.CorrectedLabel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("uid", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Get feedback summary for a tenant (agree/disagree rates per label).
    /// </summary>
    public async Task<FeedbackSummary> GetFeedbackSummaryAsync(int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT original_label, is_agree, COUNT(*) as cnt
            FROM wa_outcome_feedback
            WHERE tenant_id = @tid
            GROUP BY original_label, is_agree
            ORDER BY original_label";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var labelData = new Dictionary<string, (int agree, int disagree)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var label = reader.GetString(0);
            var isAgree = reader.GetBoolean(1);
            var count = reader.GetInt32(2);

            if (!labelData.ContainsKey(label))
                labelData[label] = (0, 0);

            var (a, d) = labelData[label];
            labelData[label] = isAgree ? (a + count, d) : (a, d + count);
        }

        var summary = new FeedbackSummary();
        foreach (var (label, (agree, disagree)) in labelData)
        {
            var total = agree + disagree;
            summary.AgreeCount += agree;
            summary.DisagreeCount += disagree;
            summary.PerLabel.Add(new LabelFeedbackBreakdown
            {
                Label = label,
                Total = total,
                Agree = agree,
                Disagree = disagree,
                AgreeRate = total > 0 ? (float)agree / total * 100f : 0
            });
        }

        summary.TotalFeedback = summary.AgreeCount + summary.DisagreeCount;
        summary.AgreeRate = summary.TotalFeedback > 0
            ? (float)summary.AgreeCount / summary.TotalFeedback * 100f
            : 0;

        return summary;
    }
}
