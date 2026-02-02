using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.ChatAnalysis;

/// <summary>
/// WapCRM API response wrapper
/// </summary>
public sealed class WapCrmApiResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("status")]
    public bool Status { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("statusCode")]
    public string? StatusCode { get; init; }

    [JsonPropertyName("requestID")]
    public string? RequestId { get; init; }
}

/// <summary>
/// WapCRM message from /api/messagelistforphone
/// </summary>
public sealed class WapCrmMessage
{
    /// <summary>
    /// Channel phone number
    /// </summary>
    [JsonPropertyName("channelnumber")]
    public string? ChannelNumber { get; init; }

    /// <summary>
    /// Message date (YYYY-MM-DD)
    /// </summary>
    [JsonPropertyName("sentdate")]
    public string? SentDate { get; init; }

    /// <summary>
    /// Message time (HH:mm:ss)
    /// </summary>
    [JsonPropertyName("senttime")]
    public string? SentTime { get; init; }

    /// <summary>
    /// Customer phone number
    /// </summary>
    [JsonPropertyName("customernumber")]
    public string? CustomerNumber { get; init; }

    /// <summary>
    /// Message content
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Message source: ME (agent), CUSTOMER
    /// </summary>
    [JsonPropertyName("messagesource")]
    public string? MessageSource { get; init; }
}
