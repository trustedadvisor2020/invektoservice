using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Followup;

/// <summary>
/// Lifecycle states for an individual <c>event_followup_runs</c> row (one per lead per stage).
/// Persisted as VARCHAR(32) snake-cased; the DB CHECK constraint mirrors this enum.
///
/// Transitions:
///   Scheduled → Sent           (FollowupStageJob.Execute completed an outbound send)
///   Scheduled → SkippedOptout  (lead opted out before stage fired; INV-MK-052)
///   Scheduled → SkippedDisabled(sequence enabled flag flipped to FALSE; INV-MK-054)
///   Scheduled → Failed         (transient or terminal error during execute; error_code populated)
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FollowupRunStatus
{
    Scheduled = 1,
    Sent = 2,
    SkippedOptout = 3,
    SkippedDisabled = 4,
    Failed = 5
}

/// <summary>
/// Wire-format helpers — the JSON enum converter writes "Scheduled" but the DB column
/// stores "scheduled" (snake_case). These constants are the canonical strings used in
/// SQL parameters, log lines, and event_followup_runs.status comparisons.
/// </summary>
public static class FollowupRunStatusValues
{
    public const string Scheduled = "scheduled";
    public const string Sent = "sent";
    public const string SkippedOptout = "skipped_optout";
    public const string SkippedDisabled = "skipped_disabled";
    public const string Failed = "failed";

    public static string ToWire(FollowupRunStatus status) => status switch
    {
        FollowupRunStatus.Scheduled => Scheduled,
        FollowupRunStatus.Sent => Sent,
        FollowupRunStatus.SkippedOptout => SkippedOptout,
        FollowupRunStatus.SkippedDisabled => SkippedDisabled,
        FollowupRunStatus.Failed => Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, $"Unknown FollowupRunStatus: {status}")
    };
}
