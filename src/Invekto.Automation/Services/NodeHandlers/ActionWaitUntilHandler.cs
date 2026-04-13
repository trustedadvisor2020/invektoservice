using System.Globalization;
using Invekto.Shared.Constants;

namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// G6: Restart-safe long wait node. Persists session state + schedules resume at an absolute UTC timestamp.
/// Config (first match wins): wait_until_iso | wait_days | wait_hours | wait_minutes | wait_seconds.
/// Bounds: min 1 second, max 30 days (clamped with warning; rejected if still invalid).
/// Simulation: instant skip with info message (no persistence).
/// </summary>
public sealed class ActionWaitUntilHandler : INodeHandler
{
    public string NodeType => "action_wait_until";

    /// <summary>Minimum wait duration. Below this use action_delay (in-memory).</summary>
    public static readonly TimeSpan MinWait = TimeSpan.FromSeconds(1);
    /// <summary>Upper bound: prevents infinite pending rows from buggy config.</summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromDays(30);

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        var (resumeAt, parseError) = ResolveResumeAt(node, now);

        if (parseError != null)
        {
            return Task.FromResult(new NodeResult
            {
                Action = NodeAction.Terminal,
                IsError = true,
                ErrorCode = ErrorCodes.AutomationFlowWaitConfigInvalid,
                ErrorMessage = parseError
            });
        }

        var duration = resumeAt - now;

        // Clamp to bounds (explicit; never silent for >MaxWait since that indicates config bug).
        if (duration > MaxWait)
        {
            ctx.Logger.SystemWarn($"[{ErrorCodes.AutomationFlowWaitConfigInvalid}] action_wait_until node {node.Id} wait {duration.TotalDays:F1}g > {MaxWait.TotalDays}g max, reddedildi");
            return Task.FromResult(new NodeResult
            {
                Action = NodeAction.Terminal,
                IsError = true,
                ErrorCode = ErrorCodes.AutomationFlowWaitConfigInvalid,
                ErrorMessage = $"Bekleme suresi max {MaxWait.TotalDays:F0} gun; istenen {duration.TotalDays:F1} gun"
            });
        }

        if (duration < MinWait)
        {
            return Task.FromResult(new NodeResult
            {
                Action = NodeAction.Terminal,
                IsError = true,
                ErrorCode = ErrorCodes.AutomationFlowWaitConfigInvalid,
                ErrorMessage = $"Bekleme suresi min 1sn; istenen {duration.TotalSeconds:F1}sn (eski tarihli wait_until_iso mi?)"
            });
        }

        if (ctx.IsSimulation)
        {
            // Simulation short-circuit: show info, continue immediately (no persistence).
            return Task.FromResult(new NodeResult
            {
                MessageText = $"Uzun bekleme: {FormatDuration(duration)} (simule edildi)",
                Action = NodeAction.Continue,
                OutputHandle = null
            });
        }

        // Production: signal engine to advance CurrentNodeId to post-wait and return to orchestrator for persistence.
        return Task.FromResult(new NodeResult
        {
            Action = NodeAction.WaitPersist,
            WaitResumeAt = resumeAt,
            OutputHandle = null
        });
    }

    private static (DateTimeOffset ResumeAt, string? Error) ResolveResumeAt(FlowNodeV2 node, DateTimeOffset now)
    {
        // 1. Absolute ISO timestamp
        var isoRaw = node.GetData("wait_until_iso");
        if (!string.IsNullOrWhiteSpace(isoRaw))
        {
            if (!DateTimeOffset.TryParse(isoRaw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var iso))
            {
                return (default, $"wait_until_iso parse edilemedi: '{isoRaw}'");
            }
            return (iso, null);
        }

        // 2. Relative duration (first non-empty wins)
        if (TryParsePositive(node.GetData("wait_days"), out var days))
            return (now.AddDays(days), null);
        if (TryParsePositive(node.GetData("wait_hours"), out var hours))
            return (now.AddHours(hours), null);
        if (TryParsePositive(node.GetData("wait_minutes"), out var minutes))
            return (now.AddMinutes(minutes), null);
        if (TryParsePositive(node.GetData("wait_seconds"), out var seconds))
            return (now.AddSeconds(seconds), null);

        return (default, "Bekleme suresi belirtilmedi (wait_until_iso / wait_days / wait_hours / wait_minutes / wait_seconds)");
    }

    private static bool TryParsePositive(string? raw, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
        return value > 0;
    }

    private static string FormatDuration(TimeSpan d)
    {
        if (d.TotalDays >= 1) return $"{d.TotalDays:F1} gun";
        if (d.TotalHours >= 1) return $"{d.TotalHours:F1} saat";
        if (d.TotalMinutes >= 1) return $"{d.TotalMinutes:F0} dk";
        return $"{d.TotalSeconds:F0} sn";
    }
}
