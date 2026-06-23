using Chatinbox.Appointments.Data;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.DTOs.Appointments;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Appointments.Services;

/// <summary>
/// GR-3.19: Waitlist helper service.
/// Provides <see cref="ProcessCancelledAppointmentAsync"/> for inline cancel-flow integration
/// and <see cref="ExpireWaitlistEntriesAsync"/> invoked by the Hangfire recurring
/// <see cref="Jobs.WaitlistJob"/> (G7 Faz 2). IHostedService scheduling removed — only the
/// tick logic migrated; endpoint-invoked members remain here.
/// </summary>
public sealed class WaitlistService
{
    private readonly AppointmentsRepository _repo;
    private readonly IHttpClientFactory _httpFactory;
    private readonly JwtGenerator _jwtGen;
    private readonly JsonLinesLogger _logger;

    public WaitlistService(
        AppointmentsRepository repo,
        IHttpClientFactory httpFactory,
        JwtGenerator jwtGen,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _httpFactory = httpFactory;
        _jwtGen = jwtGen;
        _logger = logger;
    }

    /// <summary>
    /// Expires stale waitlist entries. Invoked by <see cref="Jobs.WaitlistJob"/> on cron */5 min.
    /// Exceptions are NOT swallowed here — they bubble to Hangfire so AutomaticRetry (exponential
    /// backoff) and the FinalFailureLogger filter (INV-JOB-005) can surface failures on the
    /// dashboard. Matches Faz 1 ReminderJob pattern.
    /// </summary>
    public async Task<int> ExpireWaitlistEntriesAsync(CancellationToken ct = default)
    {
        var expired = await _repo.ExpireWaitlistEntriesAsync();
        if (expired > 0)
            _logger.StepInfo($"WaitlistJob: expired {expired} waitlist entries", "-");
        return expired;
    }

    /// <summary>
    /// Called by cancel endpoint after successful cancellation.
    /// Finds matching waitlist entries and notifies via Outbound (fire-and-forget).
    /// </summary>
    public async Task ProcessCancelledAppointmentAsync(
        int tenantId, DateOnly appointmentDate, int? doctorId, CancellationToken ct = default)
    {
        try
        {
            var matches = await _repo.FindMatchingWaitlistAsync(tenantId, appointmentDate, doctorId, ct);
            if (matches.Count == 0) return;

            _logger.StepInfo(
                $"Waitlist: found {matches.Count} matches for cancelled appointment (tenant={tenantId}, date={appointmentDate})",
                "-");

            foreach (var entry in matches)
            {
                await _repo.UpdateWaitlistStatusAsync(tenantId, entry.Id, "notified", ct);
                _ = SendWaitlistNotificationAsync(tenantId, entry);
            }
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.DatabaseConnectionFailed}] WaitlistService.ProcessCancelledAppointment DB error: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AppointmentOutboundUnavailable}] WaitlistService.ProcessCancelledAppointment HTTP error: {ex.Message}");
        }
    }

    /// <summary>
    /// Fire-and-forget notification dispatch — by design, the cancel endpoint response must not
    /// wait on Outbound. Failures are best-effort logged with INV codes so ops can correlate
    /// customer complaints to missed notifications; they are not surfaced to the caller.
    /// </summary>
    private async Task SendWaitlistNotificationAsync(int tenantId, WaitlistDto entry)
    {
        try
        {
            var client = _httpFactory.CreateClient("Outbound");
            var token = _jwtGen.GenerateServiceToken(tenantId);

            var triggerPayload = new
            {
                event_type = "waitlist_slot_available",
                customer_phone = entry.PatientPhone,
                data = new
                {
                    patient_name = entry.PatientName,
                    preferred_date = entry.PreferredDate,
                    service_type = entry.ServiceType,
                    waitlist_id = entry.Id
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhook/trigger");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = System.Net.Http.Json.JsonContent.Create(triggerPayload);

            using var response = await client.SendAsync(request);
            _logger.StepInfo(
                $"Waitlist notification sent: entry={entry.Id}, phone={entry.PatientPhone}, status={response.StatusCode}",
                "-");
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AppointmentOutboundUnavailable}] Waitlist notification HTTP error for entry {entry.Id}: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AppointmentOutboundUnavailable}] Waitlist notification timeout for entry {entry.Id}: {ex.Message}");
        }
    }
}
