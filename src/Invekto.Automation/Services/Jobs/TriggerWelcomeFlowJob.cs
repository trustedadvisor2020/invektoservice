using Hangfire;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services.Jobs;

/// <summary>
/// FEAT-LIW Chunk A: Hangfire job enqueued by Backend's intake endpoint on a
/// fresh lead. Stage-0 implementation only records the intended trigger — the
/// actual welcome-flow dispatch integration (resolve flow by slug, run
/// FlowEngine.Execute) is wired in Chunk B together with the Automation-side
/// slug lookup. This placeholder keeps <c>BackgroundJob.Enqueue&lt;T&gt;()</c>
/// resolvable for Backend's compile-only ProjectReference (G7 §12) and lets
/// deploys go out without a cross-service ordering constraint.
///
/// Queue: <c>automation</c>. No retry (welcome is best-effort; next intake
/// re-engagement will re-enqueue when the tenant's dup window expires).
/// </summary>
[Queue("automation")]
[AutomaticRetry(Attempts = 0)]
public sealed class TriggerWelcomeFlowJob
{
    private readonly JsonLinesLogger _logger;

    public TriggerWelcomeFlowJob(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(int tenantId, string welcomeFlowSlug, int leadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(welcomeFlowSlug))
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationWelcomeFlowSlugMissing}] TriggerWelcomeFlowJob: empty slug, skipping tenant={tenantId} lead={leadId}");
            return Task.CompletedTask;
        }

        _logger.StepInfo(
            $"TriggerWelcomeFlowJob: received tenant={tenantId} slug={welcomeFlowSlug} lead={leadId} (Chunk B wires real dispatch)",
            "welcome-flow-trigger");

        return Task.CompletedTask;
    }
}
