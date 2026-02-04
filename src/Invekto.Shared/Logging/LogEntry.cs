using System.Text.Json;
using System.Text.Json.Serialization;

namespace Invekto.Shared.Logging;

/// <summary>
/// JSON Lines log entry following Stage-0 log standard
/// ZORUNLU ALANLAR: timestamp, service, level, requestId, tenantId, chatId, route, durationMs, status, errorCode, message
/// </summary>
public sealed class LogEntry
{
    [JsonPropertyName("timestamp")]
    public required DateTime Timestamp { get; init; }

    [JsonPropertyName("service")]
    public required string Service { get; init; }

    [JsonPropertyName("level")]
    public required string Level { get; init; }

    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    [JsonPropertyName("tenantId")]
    public required string TenantId { get; init; }

    [JsonPropertyName("chatId")]
    public required string ChatId { get; init; }

    [JsonPropertyName("route")]
    public string? Route { get; init; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    // Traffic logging fields (optional)
    [JsonPropertyName("method")]
    public string? Method { get; init; }

    [JsonPropertyName("requestBody")]
    public string? RequestBody { get; init; }

    [JsonPropertyName("responseBody")]
    public string? ResponseBody { get; init; }

    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJsonLine() => JsonSerializer.Serialize(this, JsonOptions);
}
