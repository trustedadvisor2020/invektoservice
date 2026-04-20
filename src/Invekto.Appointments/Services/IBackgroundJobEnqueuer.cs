namespace Invekto.Appointments.Services;

/// <summary>
/// FEAT-VCP Chunk B: thin abstraction over Hangfire's static <c>BackgroundJob.Schedule</c>
/// and <c>BackgroundJob.Delete</c>. Exists solely to make <c>VideoMeetingCreationJob</c>
/// and the cancel hook unit-testable — callers can substitute a Moq-backed instance
/// to assert scheduling without spinning a Hangfire server or a PG storage connection.
/// Keep this surface minimal: any extra method must be Hangfire-agnostic (no filter
/// attributes, no job storage parameters) so tests stay pure.
/// </summary>
public interface IBackgroundJobEnqueuer
{
    /// <summary>
    /// Schedule a method invocation to run after <paramref name="delay"/>. Returns the
    /// Hangfire job id which callers persist so a subsequent cancel path can delete
    /// the pending job. Negative delays are permitted (Hangfire normalises to
    /// <c>TimeSpan.Zero</c> → immediate enqueue) — callers need no clock check.
    /// </summary>
    string Schedule<T>(System.Linq.Expressions.Expression<Func<T, Task>> methodCall, TimeSpan delay);

    /// <summary>
    /// Cancel a previously scheduled job. Returns <c>false</c> when the id is unknown /
    /// already executed / already deleted. No-op when <paramref name="jobId"/> is null
    /// or empty so cancel hooks can pipe repository columns without null checks.
    /// </summary>
    bool Delete(string? jobId);
}
