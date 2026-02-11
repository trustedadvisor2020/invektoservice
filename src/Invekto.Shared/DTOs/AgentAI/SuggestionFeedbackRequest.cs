using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.AgentAI;

public sealed class SuggestionFeedbackRequest
{
    [JsonPropertyName("suggestion_id")]
    public string? SuggestionId { get; set; }

    [JsonPropertyName("agent_action")]
    public string? AgentAction { get; set; }

    [JsonPropertyName("final_reply_text")]
    public string? FinalReplyText { get; set; }

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(SuggestionId) || string.IsNullOrWhiteSpace(AgentAction))
            return false;

        return AgentAction is "accepted" or "edited" or "rejected";
    }
}
