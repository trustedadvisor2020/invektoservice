using Hangfire;

namespace Invekto.Appointments.Services.Jobs;

/// <summary>
/// G7 Faz 2: Hangfire recurring job replacing <c>WaitlistService</c> expiration timer.
/// Delegates to <see cref="WaitlistService.ExpireWaitlistEntriesAsync"/>; helper logic
/// stays in the service class since the cancel endpoint still consumes it.
///
/// Queue: <c>appointments</c>. Recurring id: <c>appointments:waitlist</c> (cron */5 min).
/// </summary>
[Queue("appointments")]
[DisableConcurrentExecution(timeoutInSeconds: 30)]
public sealed class WaitlistJob
{
    private readonly WaitlistService _service;

    public WaitlistJob(WaitlistService service)
    {
        _service = service;
    }

    public Task RunAsync(CancellationToken ct = default) =>
        _service.ExpireWaitlistEntriesAsync(ct);
}
