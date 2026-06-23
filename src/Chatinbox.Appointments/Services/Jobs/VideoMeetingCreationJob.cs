using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hangfire;
using Chatinbox.Appointments.Data;
using Chatinbox.Appointments.Services.Video;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Contracts.Video;
using Chatinbox.Shared.DTOs.Outbound;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Appointments.Services.Jobs;

/// <summary>
/// FEAT-VCP Chunk B: per-appointment Hangfire job enqueued after the create endpoint
/// returns 201. Flow:
/// <list type="number">
/// <item>Load appointment — skip if missing (null row) or status != <c>confirmed</c>
/// (audit INV-INT-145) or <c>meeting_link</c> already set (idempotency).</item>
/// <item>Resolve tenant timezone (default <c>Europe/Istanbul</c>) and compose UTC start.</item>
/// <item>HTTP hop to Integrations <c>/internal/video/meetings</c>.
/// <c>Skipped</c> → log and return; <c>Failed</c> → log and return; <c>Success</c> → continue.</item>
/// <item>Persist <c>meeting_link</c>, <c>meeting_provider</c>, <c>calendar_event_id</c>.</item>
/// <item>Schedule two <see cref="VideoReminderJob"/> invocations at
/// <c>start_at - 24h</c> and <c>start_at - 1h</c>; persist the Hangfire job ids so a
/// later cancel hook can call <c>BackgroundJob.Delete</c>.</item>
/// <item>Fire the immediate <c>video_meeting_confirmed</c> outbound trigger (best-effort;
/// Outbound 5xx throws INV-INT-146 and retries this whole job).</item>
/// </list>
/// <para>
/// <b>Idempotency layering (Codex iter 0 Q3 clarification).</b> Three layers guard
/// against duplicate provider calls across Hangfire retries and rare cross-worker
/// races:
/// </para>
/// <list type="number">
/// <item><b>Hangfire lock</b> — <see cref="DisableConcurrentExecutionAttribute"/> (60 s
/// timeout) is a distributed lock over PG storage; two workers cannot enter
/// <see cref="RunAsync"/> for the same job id in parallel.</item>
/// <item><b>DB guard</b> — the row-read at the top skips when
/// <c>meeting_link IS NOT NULL</c>, and
/// <see cref="AppointmentsRepository.SetMeetingLinkAsync"/> enforces
/// <c>UPDATE ... WHERE meeting_link IS NULL</c>; after one worker wins the update,
/// any second attempt short-circuits before the persist and schedule side effects.</item>
/// <item><b>Provider contract</b> — <see cref="IVideoConsultProvider"/>'s Chunk A XML
/// doc requires implementations to be "deterministic OR idempotent for the same
/// (TenantId, Title, StartAtUtc) tuple so retry storms cannot double-book
/// calendars." <c>GoogleMeetMockProvider</c> satisfies this via SHA-256 hash (same
/// inputs produce the same link); the Chunk C <c>GoogleMeetProvider</c> will pass
/// a client-side idempotency token to <c>events.insert</c>.</item>
/// </list>
/// <para>
/// The provider call deliberately runs <i>before</i> the guarded UPDATE because the
/// provider's return value feeds the UPDATE. A worker crash between the provider
/// call and the UPDATE leaves the appointment in <c>meeting_link IS NULL</c> so a
/// Hangfire retry re-calls the provider — layer 3 makes that a no-op (identical
/// link) rather than a double booking.
/// </para>
/// Queue: <c>appointments</c>.
/// </summary>
[Queue("appointments")]
[DisableConcurrentExecution(timeoutInSeconds: 60)]
public sealed class VideoMeetingCreationJob
{
    private const string DefaultTimezone = "Europe/Istanbul";

    private readonly AppointmentsRepository _repository;
    private readonly IntegrationsVideoClient _videoClient;
    private readonly IBackgroundJobEnqueuer _enqueuer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;

    public VideoMeetingCreationJob(
        AppointmentsRepository repository,
        IntegrationsVideoClient videoClient,
        IBackgroundJobEnqueuer enqueuer,
        IHttpClientFactory httpClientFactory,
        JwtGenerator jwtGenerator,
        JsonLinesLogger logger)
    {
        _repository = repository;
        _videoClient = videoClient;
        _enqueuer = enqueuer;
        _httpClientFactory = httpClientFactory;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
    }

    public async Task RunAsync(int tenantId, long appointmentId, CancellationToken ct = default)
    {
        var row = await _repository.GetAppointmentVideoRowAsync(tenantId, appointmentId, ct)
            .ConfigureAwait(false);
        if (row is null)
        {
            _logger.SystemWarn(
                $"[{VideoHopErrorCodes.VideoReminderSkippedStateChanged}] " +
                $"VideoMeetingCreationJob: appointment missing tenant={tenantId} id={appointmentId}");
            return;
        }

        if (row.Status != "confirmed")
        {
            _logger.SystemInfo(
                $"[{VideoHopErrorCodes.VideoReminderSkippedStateChanged}] " +
                $"VideoMeetingCreationJob: status={row.Status} (not confirmed) " +
                $"tenant={tenantId} id={appointmentId}");
            return;
        }

        if (!string.IsNullOrEmpty(row.MeetingLink))
        {
            _logger.SystemInfo(
                $"VideoMeetingCreationJob: meeting_link already set (idempotency skip) " +
                $"tenant={tenantId} id={appointmentId}");
            return;
        }

        var timezone = await _repository.GetTenantTimezoneAsync(tenantId, ct).ConfigureAwait(false)
            ?? DefaultTimezone;
        var startAtUtc = ComposeUtcStart(row.AppointmentDate, row.StartTime, timezone);
        var durationMinutes = ComputeDurationMinutes(row.StartTime, row.EndTime);
        if (durationMinutes <= 0)
        {
            _logger.SystemWarn(
                $"[{VideoHopErrorCodes.VideoReminderSkippedStateChanged}] " +
                $"VideoMeetingCreationJob: non-positive duration " +
                $"tenant={tenantId} id={appointmentId} start={row.StartTime} end={row.EndTime}");
            return;
        }

        var attendees = new List<AttendeeDto>
        {
            new(
                Name: row.PatientName,
                Email: null,
                PhoneE164: row.PatientPhone,
                TimeZoneId: timezone,
                Role: "lead")
        };

        var request = new MeetingCreateRequest(
            TenantId: tenantId,
            Title: $"Video konsultasyon — {row.PatientName}",
            StartAtUtc: startAtUtc,
            DurationMinutes: durationMinutes,
            DentistTimeZoneId: timezone,
            Attendees: attendees);

        var outcome = await _videoClient.CreateMeetingAsync(request, ct).ConfigureAwait(false);

        switch (outcome.Kind)
        {
            case VideoMeetingHopOutcomeKind.Skipped:
                _logger.SystemInfo(
                    $"[{outcome.ErrorCode}] VideoMeetingCreationJob: provider not configured, skipping " +
                    $"tenant={tenantId} id={appointmentId}");
                return;
            case VideoMeetingHopOutcomeKind.Failed:
                _logger.SystemError(
                    $"[{outcome.ErrorCode}] VideoMeetingCreationJob: provider rejected input " +
                    $"tenant={tenantId} id={appointmentId}");
                return;
            case VideoMeetingHopOutcomeKind.Success:
                break;
        }

        // Capture-into-local pattern (no null-forgiving operator). Success outcomes are
        // constructed via VideoMeetingHopOutcome.Success(...) which always populates
        // Meeting, but we assert explicitly for defense-in-depth against future refactors
        // of the outcome factory.
        var meeting = outcome.Meeting;
        if (meeting is null)
        {
            _logger.SystemError(
                $"[{VideoHopErrorCodes.VideoMeetingHopFailed}] VideoMeetingCreationJob: " +
                $"success outcome without meeting payload (contract violation) " +
                $"tenant={tenantId} id={appointmentId}");
            return;
        }

        var persisted = await _repository.SetMeetingLinkAsync(
            tenantId, appointmentId, meeting.MeetingLink, meeting.Provider, meeting.CalendarEventId, ct)
            .ConfigureAwait(false);
        if (!persisted)
        {
            _logger.SystemInfo(
                $"VideoMeetingCreationJob: meeting_link UPDATE 0 rows (already persisted or status changed) " +
                $"tenant={tenantId} id={appointmentId}");
            return;
        }

        var now = DateTime.UtcNow;
        var delay24 = startAtUtc.AddHours(-24) - now;
        var delay1 = startAtUtc.AddHours(-1) - now;
        var job24Id = _enqueuer.Schedule<VideoReminderJob>(
            j => j.SendAsync(tenantId, appointmentId, "24h", default),
            delay24);
        var job1Id = _enqueuer.Schedule<VideoReminderJob>(
            j => j.SendAsync(tenantId, appointmentId, "1h", default),
            delay1);

        await _repository.SetVideoReminderJobIdsAsync(tenantId, appointmentId, job24Id, job1Id, ct)
            .ConfigureAwait(false);

        await SendConfirmationOutboundAsync(
            tenantId, row, meeting, timezone, startAtUtc, ct).ConfigureAwait(false);
    }

    private async Task SendConfirmationOutboundAsync(
        int tenantId,
        AppointmentVideoRow row,
        MeetingResult meeting,
        string timezone,
        DateTime startAtUtc,
        CancellationToken ct)
    {
        var startLocal = ConvertToLocal(startAtUtc, timezone);

        var payload = new TriggerWebhookRequest
        {
            Event = "video_meeting_confirmed",
            Phone = row.PatientPhone,
            Variables = new Dictionary<string, string>
            {
                ["patient_name"] = row.PatientName,
                ["meeting_link"] = meeting.MeetingLink,
                ["meeting_start_local"] = startLocal.ToString("yyyy-MM-dd HH:mm"),
                ["dentist_name"] = row.DoctorName ?? "Klinik"
            }
        };

        var client = _httpClientFactory.CreateClient("Outbound");
        var token = _jwtGenerator.GenerateServiceToken(tenantId);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhook/trigger");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(payload);

        using var response = await client.SendAsync(httpRequest, ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if ((int)response.StatusCode >= 500)
        {
            // Retryable — rethrow so Hangfire's AutomaticRetry picks it up (and the idempotency
            // guard on UPDATE ... WHERE meeting_link IS NULL prevents the persist step from
            // firing the provider a second time).
            throw new HttpRequestException(
                $"[{VideoHopErrorCodes.VideoReminderSendFailed}] Outbound trigger " +
                $"video_meeting_confirmed returned {(int)response.StatusCode}: {body}");
        }

        // 4xx = missing template / bad payload; no retry. Log + return so the meeting link
        // stays persisted and reminders stay scheduled.
        _logger.SystemWarn(
            $"[{VideoHopErrorCodes.VideoReminderSendFailed}] Outbound trigger video_meeting_confirmed " +
            $"returned {(int)response.StatusCode} tenant={tenantId} id={row.Id}: {body}");
    }

    internal static DateTime ComposeUtcStart(DateOnly date, TimeOnly time, string timezone)
    {
        var tz = ResolveTimeZone(timezone);
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    internal static DateTime ConvertToLocal(DateTime utc, string timezone)
    {
        var tz = ResolveTimeZone(timezone);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
    }

    private static TimeZoneInfo ResolveTimeZone(string timezone)
    {
        // Windows uses its own ids ("Turkey Standard Time") while Linux/containers use
        // IANA ("Europe/Istanbul"). .NET 8 accepts both on both platforms but falls back
        // to UTC with a log if the id is genuinely unknown.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    internal static int ComputeDurationMinutes(TimeOnly start, TimeOnly end)
    {
        var diff = end - start;
        return (int)Math.Round(diff.TotalMinutes);
    }
}
