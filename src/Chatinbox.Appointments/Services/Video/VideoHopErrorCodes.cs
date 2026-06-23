// FEAT-VCP Chunk B: service-local error code constants for Appointments-side video
// meeting orchestration. Mirrors arch/errors.md INV-INT-144..146.
//
// Pattern follows Chatinbox.Integrations.Services.Video.VideoErrorCodes (Chunk A) —
// service-scoped error codes live in the consuming service rather than
// Chatinbox.Shared.Constants.ErrorCodes (reserved for cross-service generics such as
// INV-INT-001..004 webhook/callback). Microservice
// isolation requirement: Appointments does NOT reference Chatinbox.Integrations, so it
// cannot reuse VideoErrorCodes directly for 140/141/142/143 — those codes surface
// from the HTTP hop response body and are logged as-received strings.

namespace Chatinbox.Appointments.Services.Video;

public static class VideoHopErrorCodes
{
    /// <summary>
    /// Chunk B — Appointments → Integrations <c>POST /internal/video/meetings</c> HTTP
    /// hop failed (5xx status / network failure / timeout). Hangfire AutomaticRetry
    /// (default 10 attempts exponential backoff) target — retry re-issues the POST
    /// once Integrations or the network recovers. Distinct from:
    /// <list type="bullet">
    /// <item>INV-INT-141 (provider <c>CreateMeetingAsync</c> threw — hop completed with 400)</item>
    /// <item>INV-INT-142 (tenant not configured — hop completed with 200 + skipped flag)</item>
    /// <item>INV-INT-143 (DB outage inside factory resolve — hop completed with 503)</item>
    /// </list>
    /// </summary>
    public const string VideoMeetingHopFailed = "INV-INT-144";

    /// <summary>
    /// Chunk B — <c>VideoReminderJob</c> or <c>VideoMeetingCreationJob</c> fired but
    /// the appointment state changed between scheduling and firing: status is no
    /// longer <c>confirmed</c>, <c>meeting_link</c> is null, or the reminder was
    /// already marked sent (idempotency guard). Informational; no retry. Expected
    /// after cancel/complete transitions; frequent occurrences for one appointment
    /// signal orphan-job cleanup failure.
    /// </summary>
    public const string VideoReminderSkippedStateChanged = "INV-INT-145";

    /// <summary>
    /// Chunk B — Outbound <c>POST /api/v1/outbound/trigger</c> for
    /// <c>video_meeting_confirmed</c> / <c>video_reminder_24h</c> / <c>video_reminder_1h</c>
    /// returned a non-success status (5xx / network / timeout). Hangfire AutomaticRetry
    /// target. Distinct from INV-INT-144 (provider hop) — this is the customer-facing
    /// WA dispatch failure. Most common pilot-time root cause is a missing
    /// <c>outbound_templates</c> row for <c>(tenant_id, trigger_event)</c>.
    /// </summary>
    public const string VideoReminderSendFailed = "INV-INT-146";
}
