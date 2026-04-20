using System.Linq.Expressions;
using Hangfire;

namespace Invekto.Appointments.Services;

/// <summary>
/// FEAT-VCP Chunk B: default <see cref="IBackgroundJobEnqueuer"/> implementation backed
/// by Hangfire's static <see cref="BackgroundJob"/> facade. No state — safe to register
/// as a singleton. Negative delays coerce to <see cref="TimeSpan.Zero"/> so
/// <c>VideoMeetingCreationJob</c> can compute <c>start_at - 24h - now</c> without a
/// conditional (Hangfire interprets zero-or-negative as "enqueue now").
/// </summary>
public sealed class BackgroundJobEnqueuer : IBackgroundJobEnqueuer
{
    public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay)
    {
        var normalised = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        return BackgroundJob.Schedule(methodCall, normalised);
    }

    public bool Delete(string? jobId)
    {
        if (string.IsNullOrEmpty(jobId)) return false;
        return BackgroundJob.Delete(jobId);
    }
}
