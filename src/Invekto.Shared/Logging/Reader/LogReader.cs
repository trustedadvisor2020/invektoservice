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

    /// <summary>
    /// Query logs with filters (for dashboard log stream)
    /// </summary>
    public async Task<LogQueryResult> QueryLogsAsync(LogQueryOptions options)
    {
        var entries = new List<LogEntryDto>();
        var limit = options.Limit ?? 100;

        foreach (var file in GetLogFilesSortedByDate())
        {
            var fileEntries = await ReadEntriesWithIdFromFileAsync(file, e =>
            {
                // Level filter
                if (options.Levels?.Length > 0 && !options.Levels.Contains(e.Level))
                    return false;

                // Service filter
                if (!string.IsNullOrEmpty(options.Service) &&
                    !e.Service?.Contains(options.Service, StringComparison.OrdinalIgnoreCase) == true)
                    return false;

                // Search filter
                if (!string.IsNullOrEmpty(options.Search) &&
                    !e.Message?.Contains(options.Search, StringComparison.OrdinalIgnoreCase) == true &&
                    !e.RequestId?.Contains(options.Search, StringComparison.OrdinalIgnoreCase) == true)
                    return false;

                // After filter
                if (options.After.HasValue && e.Timestamp < options.After.Value)
                    return false;

                // Category filter
                if (options.Categories?.Length > 0)
                {
                    var cat = InferCategory(e);
                    if (!options.Categories.Contains(cat, StringComparer.OrdinalIgnoreCase))
                        return false;
                }

                return true;
            });

            entries.AddRange(fileEntries);

            if (entries.Count >= limit + 1)
                break;
        }

        // Sort by timestamp descending
        var sorted = entries.OrderByDescending(e => e.Timestamp).ToList();
        var hasMore = sorted.Count > limit;

        return new LogQueryResult
        {
            Entries = sorted.Take(limit).ToList(),
            HasMore = hasMore,
            NextCursor = hasMore ? sorted[limit].Id : null
        };
    }

    /// <summary>
    /// Query logs grouped by requestId (for operation-based view)
    /// </summary>
    public async Task<LogGroupedResult> QueryLogsGroupedAsync(LogQueryOptions options)
    {
        var limit = options.Limit ?? 50;
        // Fetch more raw entries to form complete groups
        var rawOptions = new LogQueryOptions
        {
            Levels = options.Levels,
            Service = options.Service,
            Search = options.Search,
            After = options.After,
            Categories = options.Categories,
            Limit = limit * 10 // Fetch extra to ensure complete groups
        };

        var rawResult = await QueryLogsAsync(rawOptions);
        var entries = rawResult.Entries;

        // Group by requestId
        var groups = new List<LogGroupDto>();

        var grouped = entries
            .GroupBy(e => e.RequestId ?? "-")
            .ToList();

        foreach (var group in grouped)
        {
            var orderedEntries = group.OrderBy(e => e.Timestamp).ToList();
            var firstEntry = orderedEntries.First();
            var lastEntry = orderedEntries.Last();

            // Determine highest severity: ERROR > WARN > INFO
            var highestLevel = "INFO";
            if (group.Any(e => e.Level == "ERROR")) highestLevel = "ERROR";
            else if (group.Any(e => e.Level == "WARN")) highestLevel = "WARN";

            // Duration: prefer explicit durationMs, fallback to timestamp diff
            long? durationMs = group
                .Where(e => e.DurationMs.HasValue)
                .Select(e => e.DurationMs)
                .FirstOrDefault();

            if (!durationMs.HasValue && firstEntry.Timestamp.HasValue && lastEntry.Timestamp.HasValue)
            {
                var diff = (lastEntry.Timestamp.Value - firstEntry.Timestamp.Value).TotalMilliseconds;
                if (diff > 0) durationMs = (long)diff;
            }

            // Find the most informative message (prefer non-system messages)
            var summary = group
                .Where(e => !string.IsNullOrEmpty(e.Message) && e.Message != "-")
                .OrderByDescending(e => e.Level == "ERROR" ? 2 : e.Level == "WARN" ? 1 : 0)
                .Select(e => e.Message)
                .FirstOrDefault() ?? "-";

            // Determine category from first entry with explicit category, or infer
            var category = group
                .Select(e => e.Category)
                .FirstOrDefault(c => !string.IsNullOrEmpty(c))
                ?? InferCategory(firstEntry);

            groups.Add(new LogGroupDto
            {
                RequestId = group.Key,
                StartTime = firstEntry.Timestamp,
                EndTime = lastEntry.Timestamp,
                DurationMs = durationMs,
                Service = firstEntry.Service ?? "-",
                Level = highestLevel,
                Route = group.Select(e => e.Route).FirstOrDefault(r => !string.IsNullOrEmpty(r)),
                Status = group.Select(e => e.Status).FirstOrDefault(s => !string.IsNullOrEmpty(s)),
                ErrorCode = group.Select(e => e.ErrorCode).FirstOrDefault(c => !string.IsNullOrEmpty(c)),
                EntryCount = orderedEntries.Count,
                Category = category,
                Summary = summary!,
                Entries = orderedEntries
            });
        }

        // Sort groups by most recent first
        var sorted = groups
            .OrderByDescending(g => g.StartTime)
            .Take(limit)
            .ToList();

        return new LogGroupedResult
        {
            Groups = sorted,
            HasMore = groups.Count > limit
        };
    }

    /// <summary>
    /// Get context around a specific log entry (+-N lines)
    /// </summary>
    public async Task<LogContextResult> GetLogContextAsync(string fileName, int lineNumber, int range = 10)
    {
        var result = new LogContextResult();

        foreach (var dir in _logDirectories)
        {
            var filePath = Path.Combine(dir, fileName);
            if (!File.Exists(filePath))
                continue;

            try
            {
                var lines = await ReadAllLinesWithShareAsync(filePath);
                var startLine = Math.Max(0, lineNumber - range - 1);
                var endLine = Math.Min(lines.Length - 1, lineNumber + range - 1);

                for (int i = startLine; i <= endLine; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                        continue;

                    try
                    {
                        var entry = JsonSerializer.Deserialize<LogEntryDto>(lines[i], JsonOptions);
                        if (entry == null) continue;

                        entry.Id = $"{Path.GetFileNameWithoutExtension(fileName)}_line_{i + 1}";

                        if (i + 1 < lineNumber)
                            result.Before.Add(entry);
                        else if (i + 1 == lineNumber)
                            result.Target = entry;
                        else
                            result.After.Add(entry);
                    }
                    catch
                    {
                        // Skip malformed lines
                    }
                }

                if (result.Target != null)
                    break;
            }
            catch
            {
                // File may be locked
            }
        }

        return result;
    }

    /// <summary>
    /// Get error count by hour for the last N hours (for timeline chart)
    /// </summary>
    public async Task<ErrorStatsResult> GetErrorStatsAsync(int hours = 24)
    {
        var buckets = new Dictionary<DateTime, int>();
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-hours);

        // Initialize buckets
        for (int i = 0; i < hours; i++)
        {
            var hour = now.AddHours(-i).Date.AddHours(now.AddHours(-i).Hour);
            buckets[hour] = 0;
        }

        foreach (var file in GetLogFilesSortedByDate())
        {
            var fileEntries = await ReadEntriesFromFileAsync(file, e =>
                e.Level == "ERROR" && e.Timestamp >= cutoff);

            foreach (var entry in fileEntries)
            {
                if (entry.Timestamp.HasValue)
                {
                    var hour = entry.Timestamp.Value.Date.AddHours(entry.Timestamp.Value.Hour);
                    if (buckets.ContainsKey(hour))
                        buckets[hour]++;
                }
            }
        }

        return new ErrorStatsResult
        {
            Buckets = buckets
                .OrderBy(b => b.Key)
                .Select(b => new ErrorStatsBucket { Hour = b.Key, Count = b.Value })
                .ToList(),
            Total = buckets.Values.Sum()
        };
    }

    /// <summary>
    /// Infer category for old log entries that don't have explicit category field
    /// </summary>
    private static string InferCategory(LogEntryDto entry)
    {
        if (!string.IsNullOrEmpty(entry.Category))
            return entry.Category;

        // Health check routes
        if (entry.Route is "/health" or "/ready")
            return "health";

        // System entries (no real requestId)
        if (string.IsNullOrEmpty(entry.RequestId) || entry.RequestId == "-")
            return "system";

        // Traffic entries (HTTP method + route + status pattern)
        if (!string.IsNullOrEmpty(entry.Message) &&
            System.Text.RegularExpressions.Regex.IsMatch(entry.Message, @"^(GET|POST|PUT|DELETE|PATCH|OPTIONS|HEAD)\s+/"))
            return "traffic";

        // Step entries: has requestId but no route, tenantId="-" (background processing steps)
        if (string.IsNullOrEmpty(entry.Route) && entry.TenantId == "-")
            return "step";

        // Default: business API
        return "api";
    }

    private async Task<List<LogEntryDto>> ReadEntriesWithIdFromFileAsync(string filePath, Func<LogEntryDto, bool> predicate)
    {
        var entries = new List<LogEntryDto>();
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        try
        {
            // Use FileShare.ReadWrite to allow reading while logger is writing
            var lines = await ReadAllLinesWithShareAsync(filePath);

            // Read from end for recency
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                try
                {
                    var entry = JsonSerializer.Deserialize<LogEntryDto>(lines[i], JsonOptions);
                    if (entry != null && predicate(entry))
                    {
                        entry.Id = $"{fileName}_line_{i + 1}";
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
            // Use FileShare.ReadWrite to allow reading while logger is writing
            var lines = await ReadAllLinesWithShareAsync(filePath);

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

    /// <summary>
    /// Read all lines from file with FileShare.ReadWrite to allow concurrent access
    /// </summary>
    private static async Task<string[]> ReadAllLinesWithShareAsync(string filePath)
    {
        var lines = new List<string>();

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lines.Add(line);
        }

        return lines.ToArray();
    }
}

/// <summary>
/// DTO for reading log entries (matches LogEntry structure)
/// Uses JsonPropertyName to match camelCase output from LogEntry
/// </summary>
public sealed class LogEntryDto
{
    /// <summary>
    /// Unique identifier for this entry (format: "YYYY-MM-DD_line_N")
    /// Set by LogReader when reading from file
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

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

    [JsonPropertyName("category")]
    public string? Category { get; set; }
}

/// <summary>
/// Options for log query
/// </summary>
public sealed class LogQueryOptions
{
    public string[]? Levels { get; set; }
    public string? Service { get; set; }
    public string? Search { get; set; }
    public DateTime? After { get; set; }
    public int? Limit { get; set; }
    public string? Cursor { get; set; }
    public string[]? Categories { get; set; }
}

/// <summary>
/// Result of log query
/// </summary>
public sealed class LogQueryResult
{
    public List<LogEntryDto> Entries { get; set; } = new();
    public bool HasMore { get; set; }
    public string? NextCursor { get; set; }
}

/// <summary>
/// Result of log context query
/// </summary>
public sealed class LogContextResult
{
    public LogEntryDto? Target { get; set; }
    public List<LogEntryDto> Before { get; set; } = new();
    public List<LogEntryDto> After { get; set; } = new();
}

/// <summary>
/// A group of log entries sharing the same requestId (one operation)
/// </summary>
public sealed class LogGroupDto
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = "-";

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("service")]
    public string Service { get; set; } = "-";

    [JsonPropertyName("level")]
    public string Level { get; set; } = "INFO";

    [JsonPropertyName("route")]
    public string? Route { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("entryCount")]
    public int EntryCount { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "-";

    [JsonPropertyName("entries")]
    public List<LogEntryDto> Entries { get; set; } = new();
}

/// <summary>
/// Result of grouped log query
/// </summary>
public sealed class LogGroupedResult
{
    public List<LogGroupDto> Groups { get; set; } = new();
    public bool HasMore { get; set; }
}

/// <summary>
/// Error statistics result
/// </summary>
public sealed class ErrorStatsResult
{
    public List<ErrorStatsBucket> Buckets { get; set; } = new();
    public int Total { get; set; }
}

/// <summary>
/// Error count for a specific hour
/// </summary>
public sealed class ErrorStatsBucket
{
    [JsonPropertyName("hour")]
    public DateTime Hour { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
