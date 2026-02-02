using System.Text.Json;
using System.Text.Json.Serialization;

namespace Invekto.Shared.Logging.Reader;

/// <summary>
/// Reads and queries JSON Lines log files for /ops endpoint
/// Stage-0: Last 100 errors, last 100 slow requests, requestId search
/// Supports multiple log directories (backend + microservice)
/// </summary>
public sealed class LogReader
{
    private readonly string[] _logDirectories;
    private readonly int _slowThresholdMs;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Create log reader for single directory
    /// </summary>
    public LogReader(string logDirectory, int slowThresholdMs = 500)
        : this(new[] { logDirectory }, slowThresholdMs)
    {
    }

    /// <summary>
    /// Create log reader for multiple directories (aggregated view)
    /// </summary>
    public LogReader(string[] logDirectories, int slowThresholdMs = 500)
    {
        _logDirectories = logDirectories;
        _slowThresholdMs = slowThresholdMs;
    }

    /// <summary>
    /// Get last N error log entries (most recent first, true global across all directories)
    /// </summary>
    public async Task<List<LogEntryDto>> GetLastErrorsAsync(int count = 100)
    {
        var entries = new List<LogEntryDto>();
        var hasMultipleDirectories = _logDirectories.Length > 1;

        foreach (var file in GetLogFilesSortedByDate())
        {
            var fileEntries = await ReadEntriesFromFileAsync(file, e => e.Level == "ERROR");
            entries.AddRange(fileEntries);

            // Only early-break for single directory; for multiple dirs, scan all for true global
            if (!hasMultipleDirectories && entries.Count >= count)
                break;
        }

        // Sort by timestamp descending to ensure most recent first across all directories
        return entries
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get last N slow requests (durationMs > threshold, most recent first, true global)
    /// </summary>
    public async Task<List<LogEntryDto>> GetLastSlowRequestsAsync(int count = 100)
    {
        var entries = new List<LogEntryDto>();
        var hasMultipleDirectories = _logDirectories.Length > 1;

        foreach (var file in GetLogFilesSortedByDate())
        {
            var fileEntries = await ReadEntriesFromFileAsync(file, e =>
                e.DurationMs.HasValue && e.DurationMs.Value > _slowThresholdMs);
            entries.AddRange(fileEntries);

            // Only early-break for single directory
            if (!hasMultipleDirectories && entries.Count >= count)
                break;
        }

        // Sort by timestamp descending
        return entries
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Search logs by requestId (scans all directories)
    /// </summary>
    public async Task<List<LogEntryDto>> SearchByRequestIdAsync(string requestId)
    {
        var entries = new List<LogEntryDto>();

        foreach (var file in GetLogFilesSortedByDate())
        {
            var fileEntries = await ReadEntriesFromFileAsync(file, e =>
                e.RequestId?.Contains(requestId, StringComparison.OrdinalIgnoreCase) == true);
            entries.AddRange(fileEntries);
        }

        // Sort by timestamp descending
        return entries.OrderByDescending(e => e.Timestamp).ToList();
    }

    private IEnumerable<string> GetLogFilesSortedByDate()
    {
        var allFiles = new List<string>();

        foreach (var dir in _logDirectories)
        {
            if (Directory.Exists(dir))
            {
                allFiles.AddRange(Directory.GetFiles(dir, "*.jsonl"));
            }
        }

        // Sort by filename descending (YYYY-MM-DD.jsonl format)
        return allFiles.OrderByDescending(f => Path.GetFileName(f));
    }

    private async Task<List<LogEntryDto>> ReadEntriesFromFileAsync(string filePath, Func<LogEntryDto, bool> predicate)
    {
        var entries = new List<LogEntryDto>();

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);

            // Read from end for recency (newest entries last in file)
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                try
                {
                    var entry = JsonSerializer.Deserialize<LogEntryDto>(lines[i], JsonOptions);
                    if (entry != null && predicate(entry))
                    {
                        entries.Add(entry);
                    }
                }
                catch
                {
                    // Skip malformed lines
                }
            }
        }
        catch
        {
            // File may be locked or deleted
        }

        return entries;
    }
}

/// <summary>
/// DTO for reading log entries (matches LogEntry structure)
/// Uses JsonPropertyName to match camelCase output from LogEntry
/// </summary>
public sealed class LogEntryDto
{
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }

    [JsonPropertyName("service")]
    public string? Service { get; set; }

    [JsonPropertyName("level")]
    public string? Level { get; set; }

    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }

    [JsonPropertyName("chatId")]
    public string? ChatId { get; set; }

    [JsonPropertyName("route")]
    public string? Route { get; set; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
