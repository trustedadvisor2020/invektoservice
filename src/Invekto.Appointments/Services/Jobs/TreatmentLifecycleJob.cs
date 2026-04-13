using Hangfire;

namespace Invekto.Appointments.Services.Jobs;

/// <summary>
/// G7 Faz 2: Hangfire recurring job replacing <c>TreatmentLifecycleService</c> scheduler tick.
/// Delegates to <see cref="TreatmentLifecycleService.ProcessDueStepsAsync"/>; business logic
/// (step send, escalation, template resolve) stays in the service class because the
/// complaint-escalation endpoint still shares those helpers.
///
/// Queue: <c>appointments</c>. Recurring id: <c>appointments:treatment-lifecycle</c> (cron */5 min).
/// [DisableConcurrentExecution] replaces the prior Interlocked overlap guard; timeout 300s
/// accommodates large batches (batch_size=50, each step makes an Outbound HTTP call).
/// </summary>
[Queue("appointments")]
[DisableConcurrentExecution(timeoutInSeconds: 300)]
public sealed class TreatmentLifecycleJob
{
    private readonly TreatmentLifecycleService _service;

    public TreatmentLifecycleJob(TreatmentLifecycleService service)
    {
        _service = service;
    }

    public Task RunAsync(CancellationToken ct = default) =>
        _service.ProcessDueStepsAsync(ct);
}
