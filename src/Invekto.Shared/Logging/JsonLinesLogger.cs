using Invekto.Shared.DTOs;

namespace Invekto.Shared.Logging;

/// <summary>
/// Simple JSON Lines file logger for Stage-0
/// Writes to: logs\YYYY-MM-DD.jsonl
///
/// Two log modes:
/// - LogRequest: For API requests (enforces all required fields)
/// - LogSystem: For startup/health/internal (relaxed fields)
/// </summary>
public sealed class JsonLinesLogger : IDisposable
{
    private readonly string _serviceName;
    private readonly string _logDirectory;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private string? _currentFileName;

    public JsonLinesLogger(string serviceName, string logDirectory)
    {
        _serviceName = serviceName;
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    /// <summary>
    /// Log an API request/response with all required fields (Stage-0 contract)
    /// </summary>
    public void LogRequest(
        string level,
        string message,
        RequestContext context,
        string route,
        long durationMs,
        string status,
        string? errorCode = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Service = _serviceName,
            Level = level,
            RequestId = context.RequestId,
            TenantId = context.TenantId,
            ChatId = context.ChatId,
            Route = route,
            DurationMs = durationMs,
            Status = status,
            ErrorCode = errorCode,
            Message = message
        };

        WriteLine(entry.ToJsonLine());
    }

    /// <summary>
    /// Log system/internal events (startup, health checks, etc.)
    /// Relaxed field requirements
    /// </summary>
    public void LogSystem(string level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Service = _serviceName,
            Level = level,
            RequestId = "-",
            TenantId = "-",
            ChatId = "-",
            Message = message
        };

        WriteLine(entry.ToJsonLine());
    }

    // Convenience methods for request logging
    public void RequestInfo(string message, RequestContext context, string route, long durationMs)
        => LogRequest("INFO", message, context, route, durationMs, "ok");

    public void RequestWarn(string message, RequestContext context, string route, long durationMs, string errorCode)
        => LogRequest("WARN", message, context, route, durationMs, "partial", errorCode);

    public void RequestError(string message, RequestContext context, string route, long durationMs, string errorCode)
        => LogRequest("ERROR", message, context, route, durationMs, "fail", errorCode);

    // Convenience methods for system logging
    public void SystemInfo(string message) => LogSystem("INFO", message);
    public void SystemWarn(string message) => LogSystem("WARN", message);
    public void SystemError(string message) => LogSystem("ERROR", message);

    // Legacy methods (deprecated - use specific methods above)
    [Obsolete("Use LogRequest or LogSystem instead")]
    public void Log(
        string level,
        string message,
        RequestContext? context = null,
        string? route = null,
        long? durationMs = null,
        string? status = null,
        string? errorCode = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Service = _serviceName,
            Level = level,
            RequestId = context?.RequestId ?? "-",
            TenantId = context?.TenantId ?? "-",
            ChatId = context?.ChatId ?? "-",
            Route = route,
            DurationMs = durationMs,
            Status = status,
            ErrorCode = errorCode,
            Message = message
        };

        WriteLine(entry.ToJsonLine());
    }

    [Obsolete("Use RequestInfo instead")]
    public void Info(string message, RequestContext? context = null, string? route = null, long? durationMs = null)
        => Log("INFO", message, context, route, durationMs, "ok");

    [Obsolete("Use RequestWarn instead")]
    public void Warn(string message, RequestContext? context = null, string? route = null, string? errorCode = null)
        => Log("WARN", message, context, route, status: "partial", errorCode: errorCode);

    [Obsolete("Use RequestError instead")]
    public void Error(string message, RequestContext? context = null, string? route = null, string? errorCode = null)
        => Log("ERROR", message, context, route, status: "fail", errorCode: errorCode);

    private void WriteLine(string line)
    {
        lock (_lock)
        {
            var fileName = Path.Combine(_logDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl");

            if (_currentFileName != fileName)
            {
                _writer?.Dispose();
                _writer = new StreamWriter(fileName, append: true) { AutoFlush = true };
                _currentFileName = fileName;
            }

            _writer!.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
