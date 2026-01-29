using System.Diagnostics;
using Invekto.Contracts.DTOs.Responses;

namespace Invekto.Infrastructure.Http;

/// <summary>
/// Factory for creating standard API responses.
/// </summary>
public sealed class StandardResponseFactory
{
    private readonly string? _serviceVersion;
    private readonly Stopwatch _stopwatch;

    public StandardResponseFactory(string? serviceVersion = null)
    {
        _serviceVersion = serviceVersion;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public StandardResponse<T> Success<T>(T data, string requestId, string? jobId = null)
    {
        return StandardResponse<T>.Ok(data, CreateMeta(requestId, jobId));
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public StandardResponse<T> Error<T>(
        string errorCode,
        string message,
        string requestId,
        string traceId,
        string? details = null)
    {
        var error = new ErrorInfo
        {
            Code = errorCode,
            Message = message,
            Details = details,
            TraceId = traceId
        };

        return StandardResponse<T>.Fail(error, CreateMeta(requestId));
    }

    /// <summary>
    /// Creates a degraded response (serving from cache/fallback).
    /// </summary>
    public StandardResponse<T> Degraded<T>(
        T data,
        string requestId,
        string degradedReason)
    {
        return new StandardResponse<T>
        {
            Success = true,
            Data = data,
            Error = null,
            Meta = new ResponseMetadata
            {
                RequestId = requestId,
                ProcessingTimeMs = _stopwatch.ElapsedMilliseconds,
                ServiceVersion = _serviceVersion,
                IsDegraded = true,
                DegradedReason = degradedReason
            }
        };
    }

    private ResponseMetadata CreateMeta(string requestId, string? jobId = null)
    {
        return new ResponseMetadata
        {
            RequestId = requestId,
            ProcessingTimeMs = _stopwatch.ElapsedMilliseconds,
            ServiceVersion = _serviceVersion,
            IsDegraded = false,
            DegradedReason = null,
            JobId = jobId
        };
    }
}
