using Invekto.Contracts.DTOs.Responses;

namespace Invekto.Contracts.Interfaces;

/// <summary>
/// Marker interface for standard API responses.
/// </summary>
public interface IStandardResponse
{
    /// <summary>
    /// Whether the request was successful.
    /// </summary>
    bool Success { get; }

    /// <summary>
    /// Error details (null on success).
    /// </summary>
    ErrorInfo? Error { get; }

    /// <summary>
    /// Response metadata.
    /// </summary>
    ResponseMetadata Meta { get; }
}
