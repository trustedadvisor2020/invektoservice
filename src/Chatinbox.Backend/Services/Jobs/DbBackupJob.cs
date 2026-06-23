using System.ComponentModel;
using System.Diagnostics;
using Hangfire;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Chatinbox.Backend.Services.Jobs;

/// <summary>
/// FEAT-DBBK: Hangfire recurring job that runs <c>pg_dump --format=custom</c> against the
/// configured PostgreSQL cluster and writes the output to <c>DbBackup:OutputDir</c>.
/// Applies <c>DbBackup:RetentionDays</c> by deleting older <c>*.dump</c> files from the
/// same directory after a successful run.
///
/// Queue: <c>backend</c>. Recurring id: <c>backend:db-backup</c>.
///
/// <para><b>Cluster-level ops job (not tenant-scoped).</b> All tenants share a single
/// PostgreSQL cluster (see CLAUDE.md: "Dev PC'de PostgreSQL YOK! MCP postgres tool
/// PRODUCTION sunucuya baglanir."). A full <c>pg_dump</c> is the correct artifact for
/// disaster recovery; per-tenant backup would be overkill because schema is largely
/// shared. Restore procedure is documented in a separate ops runbook (out of scope).</para>
///
/// Password is passed via the <c>PGPASSWORD</c> environment variable so it never appears
/// in the OS process list. Partial dumps are cleaned up on failure so retention math and
/// disk usage stay honest. Top-level failures bubble to Hangfire AutomaticRetry + INV-JOB-005.
/// </summary>
[Queue("backend")]
[DisableConcurrentExecution(timeoutInSeconds: 3600)]
public sealed class DbBackupJob
{
    private readonly IConfiguration _config;
    private readonly JsonLinesLogger _logger;

    public DbBackupJob(IConfiguration config, JsonLinesLogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            if (!_config.GetValue("DbBackup:Enabled", true))
            {
                _logger.SystemInfo("DbBackupJob: skipped (DbBackup:Enabled=false)");
                return;
            }

            var pgDumpPath = _config["DbBackup:PgDumpPath"]
                             ?? @"C:\Program Files\PostgreSQL\18\bin\pg_dump.exe";
            var outputDir = _config["DbBackup:OutputDir"] ?? @"C:\Invekto\Backups";
            var retentionDays = _config.GetValue("DbBackup:RetentionDays", 14);
            var minFreeDiskGb = _config.GetValue("DbBackup:MinFreeDiskGb", 5);
            var timeoutMinutes = _config.GetValue("DbBackup:TimeoutMinutes", 60);
            var connectionString = _config.GetConnectionString("PostgreSQL");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.SystemError(
                    $"[{ErrorCodes.DbBackupConfigMissing}] DbBackupJob: ConnectionStrings:PostgreSQL missing; cannot run pg_dump. Next step: set ConnectionStrings:PostgreSQL in appsettings.Production.json and restart InvektoBackend service.");
                return;
            }

            if (!File.Exists(pgDumpPath))
            {
                _logger.SystemError(
                    $"[{ErrorCodes.DbBackupBinaryNotFound}] DbBackupJob: pg_dump binary not found at '{pgDumpPath}'. Next step: install PostgreSQL client tools or update DbBackup:PgDumpPath in appsettings.Production.json to a valid pg_dump.exe path.");
                return;
            }

            Directory.CreateDirectory(outputDir);

            if (!HasEnoughFreeDisk(outputDir, minFreeDiskGb, out var availableGb))
            {
                _logger.SystemError(
                    $"[{ErrorCodes.DbBackupDiskSpaceInsufficient}] DbBackupJob: free disk {availableGb:F1} GB below threshold {minFreeDiskGb} GB for '{outputDir}'; skipping run. Next step: free disk space or lower DbBackup:MinFreeDiskGb in appsettings.Production.json.");
                return;
            }

            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var outputFile = Path.Combine(
                outputDir,
                $"invekto-{DateTime.UtcNow:yyyyMMdd-HHmm}.dump");

            var started = Stopwatch.StartNew();
            await RunPgDumpAsync(pgDumpPath, builder, outputFile, timeoutMinutes, ct);
            started.Stop();

            var size = new FileInfo(outputFile).Length;
            _logger.SystemInfo(
                $"DbBackupJob: dump completed in {started.Elapsed.TotalSeconds:F1}s ({size / 1_048_576.0:F1} MB) -> {outputFile}");

            var deleted = DeleteExpiredDumps(outputDir, retentionDays);
            if (deleted > 0)
                _logger.SystemInfo($"DbBackupJob: retention removed {deleted} dump(s) older than {retentionDays} days");
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown / host stop — informational, not a failure.
            // INFO-level with no error code tag (INV-JOB-005 semantically means "retries exhausted").
            _logger.SystemInfo("DbBackupJob: cancelled (graceful shutdown)");
        }
        // Other exceptions bubble to Hangfire AutomaticRetry + INV-JOB-005 (retries exhausted).
    }

    private async Task RunPgDumpAsync(
        string pgDumpPath,
        NpgsqlConnectionStringBuilder conn,
        string outputFile,
        int timeoutMinutes,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = pgDumpPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("--host=" + (conn.Host ?? "localhost"));
        psi.ArgumentList.Add("--port=" + (conn.Port == 0 ? 5432 : conn.Port));
        psi.ArgumentList.Add("--username=" + (conn.Username ?? string.Empty));
        psi.ArgumentList.Add("--dbname=" + (conn.Database ?? string.Empty));
        psi.ArgumentList.Add("--format=custom");
        psi.ArgumentList.Add("--no-password");
        psi.ArgumentList.Add("--file=" + outputFile);

        // Password travels via env var so it never lands in the OS process list.
        psi.Environment["PGPASSWORD"] = conn.Password ?? string.Empty;

        using var process = new Process { StartInfo = psi };
        var stderr = new System.Text.StringBuilder();
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        if (!process.Start())
            throw new InvalidOperationException(
                $"[{ErrorCodes.DbBackupPgDumpFailed}] DbBackupJob: pg_dump failed to start");

        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            TryDelete(outputFile);
            throw;
        }

        if (process.ExitCode != 0)
        {
            TryDelete(outputFile);
            var tail = TailStderr(stderr.ToString());
            _logger.SystemError(
                $"[{ErrorCodes.DbBackupPgDumpFailed}] DbBackupJob: pg_dump exit {process.ExitCode}; stderr: {tail}");
            throw new InvalidOperationException(
                $"[{ErrorCodes.DbBackupPgDumpFailed}] pg_dump exited with code {process.ExitCode}");
        }
    }

    /// <summary>
    /// Checks free disk space on the drive hosting <paramref name="path"/>.
    /// <b>Fails closed</b>: any IO/permission error is logged with INV-JOB-011 and the
    /// method returns <c>false</c> so the caller skips the backup instead of proceeding
    /// with an unknown disk state (preferring a skipped run over a silent corrupt dump).
    /// </summary>
    private bool HasEnoughFreeDisk(string path, int minFreeGb, out double availableGb)
    {
        availableGb = 0;
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root))
            {
                _logger.SystemError(
                    $"[{ErrorCodes.DbBackupDiskSpaceInsufficient}] DbBackupJob: cannot resolve drive root for '{path}'; failing closed");
                return false;
            }
            var drive = new DriveInfo(root);
            availableGb = drive.AvailableFreeSpace / 1_073_741_824.0;
            return availableGb >= minFreeGb;
        }
        catch (IOException ex)
        {
            LogDiskCheckFailure(path, ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            LogDiskCheckFailure(path, ex);
            return false;
        }
        catch (ArgumentException ex)
        {
            LogDiskCheckFailure(path, ex);
            return false;
        }
    }

    private void LogDiskCheckFailure(string path, Exception ex)
    {
        _logger.SystemError(
            $"[{ErrorCodes.DbBackupDiskSpaceInsufficient}] DbBackupJob: disk check failed for '{path}': {ex.GetType().Name}: {ex.Message}");
    }

    /// <summary>
    /// Deletes <c>*.dump</c> files whose <c>LastWriteTimeUtc</c> is older than the cutoff.
    /// Per-file errors are logged with INV-JOB-005 (so a single locked or missing file
    /// does not stop the retention sweep for the rest of the directory).
    /// </summary>
    private int DeleteExpiredDumps(string outputDir, int retentionDays)
    {
        if (retentionDays <= 0) return 0;
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(outputDir, "*.dump"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                    count++;
                }
            }
            catch (IOException ex)
            {
                LogRetentionFailure(file, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogRetentionFailure(file, ex);
            }
        }
        return count;
    }

    private void LogRetentionFailure(string file, Exception ex)
    {
        _logger.SystemWarn(
            $"[{ErrorCodes.JobExecutionFailed}] DbBackupJob: retention delete failed for '{file}': {ex.GetType().Name}: {ex.Message}");
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException ex)
        {
            LogKillFailure(ex);
        }
        catch (Win32Exception ex)
        {
            LogKillFailure(ex);
        }
        catch (NotSupportedException ex)
        {
            LogKillFailure(ex);
        }
    }

    private void LogKillFailure(Exception ex)
    {
        _logger.SystemWarn(
            $"[{ErrorCodes.JobExecutionFailed}] DbBackupJob: pg_dump kill failed: {ex.GetType().Name}: {ex.Message}");
    }

    private void TryDelete(string file)
    {
        try
        {
            if (File.Exists(file)) File.Delete(file);
        }
        catch (IOException ex)
        {
            LogPartialDeleteFailure(file, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogPartialDeleteFailure(file, ex);
        }
    }

    private void LogPartialDeleteFailure(string file, Exception ex)
    {
        _logger.SystemWarn(
            $"[{ErrorCodes.JobExecutionFailed}] DbBackupJob: partial file delete failed for '{file}': {ex.GetType().Name}: {ex.Message}");
    }

    private static string TailStderr(string stderr)
    {
        if (string.IsNullOrEmpty(stderr)) return "(empty)";
        const int maxLen = 800;
        return stderr.Length <= maxLen ? stderr.Trim() : stderr[^maxLen..].Trim();
    }
}
