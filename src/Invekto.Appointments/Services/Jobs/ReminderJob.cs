using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hangfire;
using Invekto.Appointments.Data;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Appointments;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Appointments.Services.Jobs;

/// <summary>
/// G7: Hangfire recurring job replacing <c>ReminderSchedulerService</c>.
/// Processes T-48h then T-2h appointment reminder batches across all tenants,
/// dispatching via the Outbound trigger API. Business logic is byte-identical
/// to the prior IHostedService — only the scheduling layer changed.
///
/// Queue: <c>appointments</c>. Recurring id: <c>appointments:reminder</c> (cron */5 min).
/// Overlap protection: Hangfire <see cref="DisableConcurrentExecutionAttribute"/>
/// (cross-process advisory lock via PG storage) plus retry via
/// <see cref="AutomaticRetryAttribute"/> defaults.
/// </summary>
[Queue("appointments")]
[DisableConcurrentExecution(timeoutInSeconds: 30)]
public sealed class ReminderJob
{
    private readonly AppointmentsRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;
    private readonly int _batchSize;

    public ReminderJob(
        AppointmentsRepository repository,
        IHttpClientFactory httpClientFactory,
        JwtGenerator jwtGenerator,
        JsonLinesLogger logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _httpClientFactory = httpClientFactory;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
        _batchSize = configuration.GetValue<int>("Reminder:BatchSize", 50);
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        // Unexpected exceptions bubble to Hangfire — AutomaticRetry (default 10 attempts,
        // exponential backoff) records per-attempt failures and emits INV-JOB-005 when
        // retries are exhausted via HangfireJobFilters.FinalFailureLogger. We only trap
        // shutdown here because cancellation is not a failure.
        try
        {
            await ProcessReminderBatchAsync("48h", ct);
            if (!ct.IsCancellationRequested)
                await ProcessReminderBatchAsync("2h", ct);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown — expected during Hangfire server stop. Logged for ops visibility.
            _logger.SystemInfo("ReminderJob run cancelled (graceful shutdown)");
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
                // Don't mark as sent — Hangfire retry / next tick handles it.
            }
            catch (TaskCanceledException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AppointmentReminderSendFailed}] Reminder {reminderType} cancelled/timeout: " +
                    $"appointment={candidate.AppointmentId}, processed={sentCount + failCount}/{candidates.Count}, " +
                    $"reason={ex.Message}");
                break;
            }
            catch (NpgsqlException ex)
            {
                failCount++;
                _logger.SystemError(
                    $"[{ErrorCodes.AppointmentReminderSendFailed}] DB error for reminder " +
                    $"{reminderType}: appointment={candidate.AppointmentId}, error={ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                failCount++;
                _logger.SystemError(
                    $"[{ErrorCodes.AppointmentReminderSendFailed}] Invalid state for reminder " +
                    $"{reminderType}: appointment={candidate.AppointmentId}, error={ex.Message}");
            }
        }

        if (sentCount > 0 || failCount > 0)
            _logger.SystemInfo($"Reminder {reminderType} batch complete: sent={sentCount}, failed={failCount}");
    }

    private async Task<bool> SendReminderToOutboundAsync(
        ReminderCandidate candidate,
        string reminderType,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("Outbound");

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
