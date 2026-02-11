using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.AgentAI;

public sealed class SuggestReplyResponse
{
    [JsonPropertyName("suggestion_id")]
    public string SuggestionId { get; set; } = "";

    [JsonPropertyName("suggested_reply")]
    public string SuggestedReply { get; set; } = "";

    [JsonPropertyName("intent")]
    public string? Intent { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("processing_time_ms")]
    public long ProcessingTimeMs { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("warning")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Warning { get; set; }
}
