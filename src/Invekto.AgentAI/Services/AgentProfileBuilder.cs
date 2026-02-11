using System.Text;
using Invekto.AgentAI.Data;
using Invekto.Shared.Logging;

namespace Invekto.AgentAI.Services;

public sealed class AgentProfileBuilder
{
    private readonly AgentAIRepository _repository;
    private readonly JsonLinesLogger _logger;
    private readonly int _maxHistory;

    public AgentProfileBuilder(AgentAIRepository repository, JsonLinesLogger logger, int maxHistory = 20)
    {
        _repository = repository;
        _logger = logger;
        _maxHistory = maxHistory;
    }

    /// <summary>
    /// Builds a text profile of this agent's feedback patterns for Claude prompt injection.
    /// Returns null if no feedback history exists.
    /// </summary>
    public async Task<string?> BuildProfileAsync(
        int tenantId, int agentId, CancellationToken ct = default)
    {
        List<FeedbackRecord> records;
        try
        {
            records = await _repository.GetRecentFeedbackAsync(tenantId, agentId, _maxHistory, ct);
        }
        catch (Exception ex)
        {
            _logger.StepError($"Failed to fetch agent feedback history: {ex.Message}", "-");
            return null;
        }

        if (records.Count == 0)
            return null;

        var accepted = records.Count(r => r.AgentAction == "accepted");
        var edited = records.Count(r => r.AgentAction == "edited");
        var rejected = records.Count(r => r.AgentAction == "rejected");
        var total = records.Count;

        var sb = new StringBuilder();
        sb.AppendLine($"[Agent Profile -- son {total} etkileşim]");
        sb.AppendLine($"- Kabul: %{100 * accepted / total} ({accepted}/{total})");
        sb.AppendLine($"- Düzenleme: %{100 * edited / total} ({edited}/{total})");
        sb.AppendLine($"- Reddetme: %{100 * rejected / total} ({rejected}/{total})");

        // Extract editing patterns (show last 5 edits as examples)
        var editExamples = records
            .Where(r => r.AgentAction == "edited" && r.FinalReplyText != null)
            .Take(5)
            .ToList();

        if (editExamples.Count > 0)
        {
            sb.AppendLine("- Düzenleme örnekleri:");
            foreach (var ex in editExamples)
            {
                var originalTrunc = Truncate(ex.SuggestedReply, 80);
                var finalTrunc = Truncate(ex.FinalReplyText, 80);
                sb.AppendLine($"  AI: \"{originalTrunc}\" -> Agent: \"{finalTrunc}\"");
            }
        }

        // Show rejected intents
        var rejectedIntents = records
            .Where(r => r.AgentAction == "rejected" && r.Intent != null)
            .GroupBy(r => r.Intent)
            .Select(g => new { Intent = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(3)
            .ToList();

        if (rejectedIntents.Count > 0)
        {
            var intentList = string.Join(", ", rejectedIntents.Select(x => $"{x.Intent}({x.Count})"));
            sb.AppendLine($"- Sık reddedilen konular: {intentList}");
        }

        sb.AppendLine("Bu agent'ın tarzına uygun öneri üret.");
        return sb.ToString();
    }

    private static string? Truncate(string? text, int maxLen)
    {
        if (text == null) return null;
        return text.Length <= maxLen ? text : text[..maxLen] + "...";
    }
}
