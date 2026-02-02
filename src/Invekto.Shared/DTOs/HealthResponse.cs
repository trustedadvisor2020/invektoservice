namespace Invekto.Shared.DTOs;

/// <summary>
/// Standard health check response
/// </summary>
public sealed class HealthResponse
{
    public required string Status { get; init; }
    public required string Service { get; init; }
    public required DateTime Timestamp { get; init; }

    public static HealthResponse Ok(string serviceName) => new()
    {
        Status = "ok",
        Service = serviceName,
        Timestamp = DateTime.UtcNow
    };
}
