using System.Net.Http.Headers;
using System.Net.Http.Json;
using Invekto.Appointments.Data;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;

namespace Invekto.Appointments.Services;

/// <summary>
/// Background service that checks for pending appointment reminders every N minutes.
/// Sends reminders via Outbound trigger API (appointment_reminder event).
/// Uses Interlocked.CompareExchange for overlap prevention.
/// Graceful shutdown via CancellationToken.
/// </summary>
public sealed class ReminderSchedulerService : IHostedService, IDisposable
{
    private readonly AppointmentsRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;
    private readonly int _intervalMs;
    private readonly int _batchSize;

    private Timer? _timer;
    private int _isProcessing; // 0 = idle, 1 = processing (interlocked)
    private CancellationTokenSource? _cts;

    public ReminderSchedulerService(
        AppointmentsRepository repository,
        IHttpClientFactory httpClientFactory,
        JwtGenerator jwtGenerator,
        JsonLinesLogger logger,
        int intervalMs = 300_000,
        int batchSize = 50)
    {
        _repository = repository;
        _httpClientFactory = httpClientFactory;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
        _intervalMs = intervalMs;
        _batchSize = batchSize;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger.SystemInfo($"ReminderSchedulerService starting (interval={_intervalMs}ms, batch={_batchSize})");

        // Delay first run by intervalMs to allow service to fully start
        _timer = new Timer(ProcessReminders, null, _intervalMs, _intervalMs);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.SystemInfo("ReminderSchedulerService stopping (graceful shutdown)");
        _timer?.Change(Timeout.Infinite, 0);
        _cts?.Cancel();

        // Wait for current processing to finish (max 30s for reminder completion)
        var waitCount = 0;
        while (Interlocked.CompareExchange(ref _isProcessing, 0, 0) == 1 && waitCount < 300)
        {
            await Task.Delay(100, cancellationToken);
            waitCount++;
        }

        if (waitCount >= 300)
            _logger.SystemWarn("ReminderSchedulerService: graceful shutdown timed out after 30s");
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _cts?.Dispose();
    }

    private async void ProcessReminders(object? state)
    {
        // Prevent overlapping processing
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
            return;

        try
        {
            var ct = _cts?.Token ?? CancellationToken.None;
            if (ct.IsCancellationRequested) return;

            // Process T-48h reminders
            await ProcessReminderBatchAsync("48h", ct);

            // Process T-2h reminders
            if (!ct.IsCancellationRequested)
                await ProcessReminderBatchAsync("2h", ct);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown, expected
        }
        catch (Exception ex)
        {
            _logger.SystemError($"ReminderSchedulerService error: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }

    private async Task ProcessReminderBatchAsync(string reminderType, CancellationToken ct)
    {
        var candidates = reminderType switch
        {
            "48h" => await _repository.GetPending48hRemindersAsync(_batchSize, ct),
            "2h" => await _repository.GetPending2hRemindersAsync(_batchSize, ct),
            _ => throw new ArgumentException($"Unknown reminder type: {reminderType}")
        };

        if (candidates.Count == 0) return;

        _logger.SystemInfo($"Processing {candidates.Count} pending {reminderType} reminders");
        var sentCount = 0;
        var failCount = 0;

        foreach (var candidate in candidates)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var sent = await SendReminderToOutboundAsync(candidate, reminderType, ct);
                if (sent)
                {
                    await _repository.MarkReminderSentAsync(candidate.TenantId, candidate.AppointmentId, reminderType, ct);
                    sentCount++;
                }
                else
                {
                    failCount++;
                    _logger.SystemWarn(
                        $"[{ErrorCodes.AppointmentReminderSendFailed}] Reminder {reminderType} send failed: " +
                        $"appointment={candidate.AppointmentId}, tenant={candidate.TenantId}, " +
                        $"phone={candidate.PatientPhone}");
                }
            }
            catch (HttpRequestException ex)
            {
                failCount++;
                _logger.SystemError(
                    $"[{ErrorCodes.AppointmentOutboundUnavailable}] Outbound unavailable for reminder " +
                    $"{reminderType}: appointment={candidate.AppointmentId}, error={ex.Message}");
                // Don't mark as sent — scheduler will retry next cycle
            }
            catch (TaskCanceledException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AppointmentReminderSendFailed}] Reminder {reminderType} cancelled/timeout: " +
                    $"appointment={candidate.AppointmentId}, processed={sentCount + failCount}/{candidates.Count}, " +
                    $"reason={ex.Message}");
                break;
            }
            catch (Exception ex)
            {
                failCount++;
                _logger.SystemError(
                    $"[{ErrorCodes.AppointmentReminderSendFailed}] Unexpected error for reminder " +
                    $"{reminderType}: appointment={candidate.AppointmentId}, error={ex.Message}");
            }
        }

        if (sentCount > 0 || failCount > 0)
            _logger.SystemInfo($"Reminder {reminderType} batch complete: sent={sentCount}, failed={failCount}");
    }

    private async Task<bool> SendReminderToOutboundAsync(
        Shared.DTOs.Appointments.ReminderCandidate candidate,
        string reminderType,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("Outbound");

        // Use shared TriggerWebhookRequest DTO for correct JSON property names
        var payload = new TriggerWebhookRequest
        {
            Event = "appointment_reminder",
            Phone = candidate.PatientPhone,
            Variables = new Dictionary<string, string>
            {
                ["patient_name"] = candidate.PatientName,
                ["appointment_date"] = candidate.AppointmentDate.ToString("dd.MM.yyyy"),
                ["start_time"] = candidate.StartTime.ToString("HH:mm"),
                ["end_time"] = candidate.EndTime.ToString("HH:mm"),
                ["reminder_type"] = reminderType == "48h" ? "48 saat" : "2 saat"
            }
        };

        // Generate service JWT for the tenant (Outbound requires JWT auth on /api/v1/)
        var token = _jwtGenerator.GenerateServiceToken(candidate.TenantId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhook/trigger");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(payload);

        using var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return true;

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.SystemWarn(
            $"Outbound trigger returned {(int)response.StatusCode} for appointment " +
            $"{candidate.AppointmentId}: {body}");
        return false;
    }
}
