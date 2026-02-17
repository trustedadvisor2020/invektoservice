using Invekto.Appointments.Data;
using Invekto.Shared.Auth;
using Invekto.Shared.DTOs.Appointments;
using Invekto.Shared.Logging;

namespace Invekto.Appointments.Services;

/// <summary>
/// GR-3.19: Waitlist background service.
/// - Expires stale waitlist entries every 5 minutes.
/// - Provides ProcessCancelledAppointment for inline cancel-flow integration.
/// Pattern: same as ReminderSchedulerService (Interlocked overlap prevention).
/// </summary>
public sealed class WaitlistService : IHostedService, IDisposable
{
    private readonly AppointmentsRepository _repo;
    private readonly IHttpClientFactory _httpFactory;
    private readonly JwtGenerator _jwtGen;
    private readonly JsonLinesLogger _logger;
    private readonly int _intervalMs;

    private Timer? _timer;
    private int _isRunning;

    public WaitlistService(
        AppointmentsRepository repo,
        IHttpClientFactory httpFactory,
        JwtGenerator jwtGen,
        JsonLinesLogger logger,
        int intervalMs = 300_000) // 5 minutes default
    {
        _repo = repo;
        _httpFactory = httpFactory;
        _jwtGen = jwtGen;
        _logger = logger;
        _intervalMs = intervalMs;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("WaitlistService started");
        _timer = new Timer(OnTick, null, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(_intervalMs));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("WaitlistService stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

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
                // Mark as notified
                await _repo.UpdateWaitlistStatusAsync(tenantId, entry.Id, "notified", ct);

                // Fire-and-forget Outbound notification
                _ = SendWaitlistNotificationAsync(tenantId, entry);
            }
        }
        catch (Npgsql.NpgsqlException ex)
        {
            // Waitlist processing must never break the cancel flow
            _logger.SystemWarn($"WaitlistService.ProcessCancelledAppointment DB error: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"WaitlistService.ProcessCancelledAppointment HTTP error: {ex.Message}");
        }
    }

    private async void OnTick(object? state)
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            return;

        try
        {
            var expired = await _repo.ExpireWaitlistEntriesAsync();
            if (expired > 0)
            {
                _logger.StepInfo($"WaitlistService: expired {expired} waitlist entries", "-");
            }
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemWarn($"WaitlistService tick failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

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
            _logger.SystemWarn($"Waitlist notification HTTP error for entry {entry.Id}: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            _logger.SystemWarn($"Waitlist notification timeout for entry {entry.Id}: {ex.Message}");
        }
    }
}
