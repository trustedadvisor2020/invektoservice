using Invekto.Contracts.Interfaces;

namespace Invekto.Contracts.DTOs.Responses;

/// <summary>
/// Standard API response format for all Invekto services.
/// </summary>
/// <typeparam name="T">Type of the response data.</typeparam>
public sealed class StandardResponse<T> : IStandardResponse
{
    /// <summary>
    /// Whether the request was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Response payload (null on error).
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// Error details (null on success).
    /// </summary>
    public ErrorInfo? Error { get; init; }

    /// <summary>
    /// Response metadata.
    /// </summary>
    public required ResponseMetadata Meta { get; init; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static StandardResponse<T> Ok(T data, ResponseMetadata meta) => new()
    {
        Success = true,
        Data = data,
        Error = null,
        Meta = meta
    };

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static StandardResponse<T> Fail(ErrorInfo error, ResponseMetadata meta) => new()
    {
        Success = false,
        Data = default,
        Error = error,
        Meta = meta
    };
}

/// <summary>
/// Non-generic standard response for endpoints with no data.
/// </summary>
public sealed class StandardResponse : IStandardResponse
{
    /// <summary>
    /// Whether the request was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error details (null on success).
    /// </summary>
    public ErrorInfo? Error { get; init; }

    /// <summary>
    /// Response metadata.
    /// </summary>
    public required ResponseMetadata Meta { get; init; }

    /// <summary>
    /// Creates a successful response with no data.
    /// </summary>
    public static StandardResponse Ok(ResponseMetadata meta) => new()
    {
        Success = true,
        Error = null,
        Meta = meta
    };

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static StandardResponse Fail(ErrorInfo error, ResponseMetadata meta) => new()
    {
        Success = false,
        Error = error,
        Meta = meta
    };
}
