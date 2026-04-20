using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Inma.Dtos;

/// <summary>
/// FEAT-DMP: INMA <c>/api/dynamicfields</c> envelope.
/// Mirrors the standard WapCRM response: <c>{Status, Message, StatusCode, RequestID, Data}</c>.
/// </summary>
public sealed class InmaDynamicFieldsResponse
{
    [JsonPropertyName("Status")]
    public bool Status { get; init; }

    [JsonPropertyName("Message")]
    public string? Message { get; init; }

    [JsonPropertyName("StatusCode")]
    public string? StatusCode { get; init; }

    [JsonPropertyName("RequestID")]
    public string? RequestId { get; init; }

    [JsonPropertyName("Data")]
    public List<InmaDynamicField>? Data { get; init; }
}
