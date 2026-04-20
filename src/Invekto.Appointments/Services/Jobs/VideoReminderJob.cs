using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hangfire;
using Invekto.Appointments.Data;
using Invekto.Appointments.Services.Video;
using Invekto.Shared.Auth;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;

namespace Invekto.Appointments.Services.Jobs;

/// <summary>
/// FEAT-VCP Chunk B: per-meeting Hangfire reminder fired at <c>start_at - 24h</c>
/// and <c>start_at - 1h</c> by <see cref="VideoMeetingCreationJob"/>. Two-layer
/// state-change defense:
/// <list type="number">
/// <item>Primary — <c>AppointmentsRepository.ClearScheduledReminderJobIdsAsync</c>
/// + <c>BackgroundJob.Delete</c> in the cancel hook removes the scheduled job.</item>
/// <item>Secondary — this job's own guards: appointment missing / status != <c>confirmed</c>
/// / <c>meeting_link</c> null / reminder already marked sent → audit INV-INT-145 and
/// return without dispatching.</item>
/// </list>
/// Outbound dispatch failures surface INV-INT-146 and bubble so Hangfire's
/// AutomaticRetry re-fires (missing <c>outbound_templates</c> row is the most
/// common pilot-time cause, fixed by ops-side INSERT rather than code retry).
/// </summary>
[Queue("appointments")]
public sealed class VideoReminderJob
{
    private readonly AppointmentsRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;

    public VideoReminderJob(
        AppointmentsRepository repository,
        IHttpClientFactory httpClientFactory,
        JwtGenerator jwtGenerator,
        JsonLinesLogger logger)
    {
        _repository = repository;
        _httpClientFactory = httpClientFactory;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
    }

    public async Task SendAsync(
        int tenantId, long appointmentId, string reminderType, CancellationToken ct = default)
    {
        if (reminderType is not ("24h" or "1h"))
        {
            throw new ArgumentException(
                $"reminderType must be '24h' or '1h' (got '{reminderType}')",
                nameof(reminderType));
        }

        var row = await _repository.GetAppointmentVideoRowAsync(tenantId, appointmentId, ct)
            .ConfigureAwait(false);
        if (row is null)
        {
            _logger.SystemInfo(
                $"[{VideoHopErrorCodes.VideoReminderSkippedStateChanged}] " +
                $"VideoReminderJob {reminderType}: appointment missing tenant={tenantId} id={appointmentId}");
            return;
        }

        // Capture-into-local pattern (no null-forgiving operator). The guard below
        // narrows MeetingLink to non-null so we can pass it to the Variables dict
        // without suppressing nullability. row.Status != "confirmed" also gates the
        // stale-state audit branch.
        var meetingLink = row.MeetingLink;
        if (row.Status != "confirmed" || string.IsNullOrEmpty(meetingLink))
        {
            _logger.SystemInfo(
                $"[{VideoHopErrorCodes.VideoReminderSkippedStateChanged}] " +
                $"VideoReminderJob {reminderType}: state changed (status={row.Status} " +
                $"link={(meetingLink is null ? "null" : "set")}) " +
                $"tenant={tenantId} id={appointmentId}");
            return;
        }

        var alreadySent = reminderType == "24h"
            ? row.VideoReminder24hSentAt.HasValue
            : row.VideoReminder1hSentAt.HasValue;
        if (alreadySent)
        {
            _logger.SystemInfo(
                $"VideoReminderJob {reminderType}: already sent (idempotency skip) " +
                $"tenant={tenantId} id={appointmentId}");
            return;
        }

        var timezone = await _repository.GetTenantTimezoneAsync(tenantId, ct).ConfigureAwait(false)
            ?? "Europe/Istanbul";
        var startAtUtc = VideoMeetingCreationJob.ComposeUtcStart(row.AppointmentDate, row.StartTime, timezone);
        var startLocal = VideoMeetingCreationJob.ConvertToLocal(startAtUtc, timezone);

        var payload = new TriggerWebhookRequest
        {
            Event = reminderType == "24h" ? "video_reminder_24h" : "video_reminder_1h",
            Phone = row.PatientPhone,
            Variables = new Dictionary<string, string>
            {
                ["patient_name"] = row.PatientName,
                ["meeting_link"] = meetingLink,
                ["meeting_start_local"] = startLocal.ToString("yyyy-MM-dd HH:mm"),
                ["dentist_name"] = row.DoctorName ?? "Klinik",
                ["hours_remaining"] = reminderType == "24h" ? "24" : "1"
            }
        };

        var client = _httpClientFactory.CreateClient("Outbound");
        var token = _jwtGenerator.GenerateServiceToken(tenantId);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhook/trigger");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(payload);

        using var response = await client.SendAsync(httpRequest, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if ((int)response.StatusCode >= 500)
            {
                // Retryable — rethrow for Hangfire AutomaticRetry.
                throw new HttpRequestException(
                    $"[{VideoHopErrorCodes.VideoReminderSendFailed}] Outbound trigger " +
                    $"{payload.Event} returned {(int)response.StatusCode}: {body}");
            }

            // 4xx — missing template / bad payload. No retry. Log and return so sent_at
            // stays null (ops can re-trigger after fixing the template).
            _logger.SystemWarn(
                $"[{VideoHopErrorCodes.VideoReminderSendFailed}] Outbound trigger {payload.Event} " +
                $"returned {(int)response.StatusCode} tenant={tenantId} id={appointmentId}: {body}");
            return;
        }

        await _repository.MarkVideoReminderSentAsync(tenantId, appointmentId, reminderType, ct)
            .ConfigureAwait(false);
    }
}
