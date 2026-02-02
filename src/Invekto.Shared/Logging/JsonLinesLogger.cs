using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;

namespace Invekto.Shared.Logging;

/// <summary>
/// Simple JSON Lines file logger for Stage-0
/// Writes to: logs\YYYY-MM-DD.jsonl
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

    public void Info(string message, RequestContext? context = null, string? route = null, long? durationMs = null)
        => Log("INFO", message, context, route, durationMs, "ok");

    public void Warn(string message, RequestContext? context = null, string? route = null, string? errorCode = null)
        => Log("WARN", message, context, route, status: "partial", errorCode: errorCode);

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
