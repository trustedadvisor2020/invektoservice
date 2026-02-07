using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs;

/// <summary>
/// Describes a single API endpoint for discovery/documentation
/// </summary>
public sealed class EndpointInfo
{
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("auth")]
    public string? Auth { get; init; }

    [JsonPropertyName("category")]
    public required string Category { get; init; }
}

/// <summary>
/// Response from /api/ops/endpoints discovery endpoint
/// </summary>
public sealed class EndpointDiscoveryResponse
{
    [JsonPropertyName("service")]
    public required string Service { get; init; }

    [JsonPropertyName("port")]
    public required int Port { get; init; }

    [JsonPropertyName("endpoints")]
    public required List<EndpointInfo> Endpoints { get; init; }
}
