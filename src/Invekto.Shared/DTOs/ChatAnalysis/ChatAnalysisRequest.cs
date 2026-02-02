using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.ChatAnalysis;

/// <summary>
/// Request DTO for chat analysis endpoint
/// Contract: arch/contracts/chat-analysis.json
/// </summary>
public sealed class ChatAnalysisRequest
{
    /// <summary>
    /// Customer phone number (e.g., 905xxxxxxxxx)
    /// </summary>
    [JsonPropertyName("phoneNumber")]
    public required string PhoneNumber { get; init; }

    /// <summary>
    /// WapCRM instance ID from /api/Instances
    /// </summary>
    [JsonPropertyName("instanceId")]
    public required int InstanceId { get; init; }
}
