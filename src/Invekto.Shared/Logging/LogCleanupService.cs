using Invekto.Shared.Constants;

namespace Invekto.Shared.Logging;

/// <summary>
/// Background service to clean up old log files
/// Stage-0: 30 day retention
/// </summary>
public sealed class LogCleanupService : IDisposable
{
    private readonly string _logDirectory;
    private readonly int _retentionDays;
    private readonly Timer _timer;

    public LogCleanupService(string logDirectory, int retentionDays = ServiceConstants.LogRetentionDays)
    {
        _logDirectory = logDirectory;
        _retentionDays = retentionDays;

        // Run cleanup daily at startup and then every 24 hours
        _timer = new Timer(Cleanup, null, TimeSpan.Zero, TimeSpan.FromHours(24));
    }

    private void Cleanup(object? state)
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
                return;

            var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);

            foreach (var file in Directory.GetFiles(_logDirectory, "*.jsonl"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);

                // Parse date from filename (YYYY-MM-DD)
                if (DateTime.TryParse(fileName, out var fileDate))
                {
                    if (fileDate < cutoffDate)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // File may be locked
                        }
                    }
                }
            }
        }
        catch
        {
            // Swallow cleanup errors - non-critical
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
