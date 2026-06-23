using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs;

public sealed class IntentCreateRequest
{
    [JsonPropertyName("intent_name")]
    public string IntentName { get; set; } = "";

    [JsonPropertyName("keywords")]
    public string[]? Keywords { get; set; }
}
