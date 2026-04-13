using System.Net.Http.Headers;
using System.Net.Http.Json;
using Invekto.Appointments.Data;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Appointments;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;

namespace Invekto.Appointments.Services;

/// <summary>
/// GR-3.20 + GR-3.41 + GR-3.43: Treatment lifecycle helper service.
/// <para>
/// G7 Faz 2: Scheduling migrated to Hangfire recurring <see cref="Jobs.TreatmentLifecycleJob"/>.
/// This class retains endpoint-invoked helpers (<see cref="StartLifecycleAsync"/>,
/// <see cref="HandleComplaintEscalationAsync"/>) plus the tick body
/// <see cref="ProcessDueStepsAsync"/> called by the job. Overlap prevention, graceful
/// shutdown, and retry now delegated to Hangfire.
/// </para>
/// </summary>
public sealed class TreatmentLifecycleService
{
    private readonly AppointmentsRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;
    private readonly int _batchSize;

    public TreatmentLifecycleService(
        AppointmentsRepository repository,
        IHttpClientFactory httpClientFactory,
        JwtGenerator jwtGenerator,
        JsonLinesLogger logger,
        int batchSize = 50)
    {
        _repository = repository;
        _httpClientFactory = httpClientFactory;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
        _batchSize = batchSize;
    }

    /// <summary>
    /// Process all due lifecycle steps in a single batch. Invoked by
    /// <see cref="Jobs.TreatmentLifecycleJob"/> on cron */5 min.
    /// Per-candidate typed catches preserved (so one bad row does not abort the batch).
    /// Top-level unexpected exceptions BUBBLE to Hangfire — AutomaticRetry + FinalFailureLogger
    /// (INV-JOB-005) surface them in the dashboard. Only OperationCanceledException is trapped
    /// because cancellation is a graceful shutdown, not a failure.
    /// </summary>
    public async Task ProcessDueStepsAsync(CancellationToken ct = default)
    {
        try
        {
            if (ct.IsCancellationRequested)
            {
                _logger.SystemInfo("TreatmentLifecycleJob: skipping (cancellation requested)");
                return;
            }

            var candidates = await _repository.GetDueStepsAsync(_batchSize, ct);
            if (candidates.Count == 0)
            {
                _logger.SystemInfo("TreatmentLifecycleJob: idle cycle (no due steps)");
                return;
            }

            _logger.SystemInfo($"Processing {candidates.Count} due lifecycle steps");
            var sentCount = 0;
            var failCount = 0;

            foreach (var candidate in candidates)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var sent = await SendStepMessageAsync(candidate, ct);
                    if (sent)
                    {
                        await _repository.MarkStepSentAsync(candidate.TenantId, candidate.StepId, ct);
                        sentCount++;

                        if (candidate.StepOrder == candidate.TotalSteps)
                            await HandleLastStepAsync(candidate, ct);
                    }
                    else
                    {
                        failCount++;
                        _logger.SystemWarn(
                            $"[{ErrorCodes.LifecycleStepSendFailed}] Lifecycle step send failed: " +
                            $"step={candidate.StepId}, followup={candidate.FollowupId}, " +
                            $"tenant={candidate.TenantId}, phone={candidate.PatientPhone}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    failCount++;
                    _logger.SystemError(
                        $"[{ErrorCodes.AppointmentOutboundUnavailable}] Outbound unavailable for lifecycle step: " +
                        $"step={candidate.StepId}, followup={candidate.FollowupId}, error={ex.Message}");
                }
                catch (TaskCanceledException ex)
                {
                    _logger.SystemWarn(
                        $"[{ErrorCodes.LifecycleStepSendFailed}] Lifecycle step cancelled/timeout: " +
                        $"step={candidate.StepId}, processed={sentCount + failCount}/{candidates.Count}, " +
                        $"reason={ex.Message}");
                    break;
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    failCount++;
                    _logger.SystemError(
                        $"[{ErrorCodes.DatabaseConnectionFailed}] DB error for lifecycle step: " +
                        $"step={candidate.StepId}, followup={candidate.FollowupId}, error={ex.Message}");
                }
            }

            if (sentCount > 0 || failCount > 0)
                _logger.SystemInfo($"Lifecycle step batch complete: sent={sentCount}, failed={failCount}");
        }
        catch (OperationCanceledException)
        {
            _logger.SystemInfo("TreatmentLifecycleJob: processing cancelled (graceful shutdown)");
        }
        // Other exceptions bubble to Hangfire (AutomaticRetry + INV-JOB-005 on final failure).
    }

    private async Task HandleLastStepAsync(DueStepCandidate candidate, CancellationToken ct)
    {
        try
        {
            var (allSent, lastStepResponded, _) =
                await _repository.CheckFollowupCompletionAsync(
                    candidate.TenantId, candidate.FollowupId, ct);

            if (!allSent)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.LifecycleStepSendFailed}] HandleLastStepAsync: not all steps sent yet " +
                    $"(unexpected), followup={candidate.FollowupId}, tenant={candidate.TenantId}");
                return;
            }

            if (!string.IsNullOrEmpty(candidate.EscalationTarget) && !lastStepResponded)
            {
                await SendEscalationAlertAsync(candidate, ct);
                await _repository.MarkStepEscalatedAsync(candidate.TenantId, candidate.StepId, ct);
                await _repository.EscalateFollowupAsync(candidate.TenantId, candidate.FollowupId, ct);
                _logger.SystemInfo(
                    $"Lifecycle escalated (no response): followup={candidate.FollowupId}, " +
                    $"tenant={candidate.TenantId}, target={candidate.EscalationTarget}");
            }
            else
            {
                await _repository.CompleteFollowupAsync(candidate.TenantId, candidate.FollowupId, ct);
                _logger.SystemInfo(
                    $"Lifecycle completed: followup={candidate.FollowupId}, tenant={candidate.TenantId}");
            }
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.DatabaseConnectionFailed}] DB error during lifecycle completion check: " +
                $"followup={candidate.FollowupId}, error={ex.Message}");
        }
    }

    private async Task<bool> SendStepMessageAsync(DueStepCandidate candidate, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("Outbound");

        var resolvedMessage = ResolveTemplate(candidate.MessageTemplate, candidate);

        var payload = new TriggerWebhookRequest
        {
            Event = $"lifecycle_{candidate.LifecycleType}",
            Phone = candidate.PatientPhone,
            Variables = new Dictionary<string, string>
            {
                ["patient_name"] = candidate.PatientName,
                ["treatment_type"] = candidate.TreatmentType ?? "",
                ["step_key"] = candidate.StepKey,
                ["message"] = resolvedMessage
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
            $"[{ErrorCodes.LifecycleStepSendFailed}] Outbound trigger returned {(int)response.StatusCode} " +
            $"for lifecycle step {candidate.StepId}: {body}");
        return false;
    }

    private async Task SendEscalationAlertAsync(DueStepCandidate candidate, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Outbound");

            var payload = new TriggerWebhookRequest
            {
                Event = $"lifecycle_escalation_{candidate.EscalationTarget}",
                Phone = candidate.PatientPhone,
                Variables = new Dictionary<string, string>
                {
                    ["patient_name"] = candidate.PatientName,
                    ["treatment_type"] = candidate.TreatmentType ?? "",
                    ["lifecycle_type"] = candidate.LifecycleType,
                    ["escalation_target"] = candidate.EscalationTarget ?? "",
                    ["message"] = $"Dikkat: {candidate.PatientName} ({candidate.PatientPhone}) " +
                        $"{candidate.LifecycleType} takibinde yanit vermiyor. Eskalasyon: {candidate.EscalationTarget}"
                }
            };

            var token = _jwtGenerator.GenerateServiceToken(candidate.TenantId);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhook/trigger");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(payload);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.SystemWarn(
                    $"[{ErrorCodes.LifecycleStepSendFailed}] Escalation alert failed for followup " +
                    $"{candidate.FollowupId}: status={response.StatusCode}, body={body}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemError(
                $"[{ErrorCodes.AppointmentOutboundUnavailable}] Outbound unavailable for escalation: " +
                $"followup={candidate.FollowupId}, error={ex.Message}");
        }
    }

    private static string ResolveTemplate(string template, DueStepCandidate candidate)
    {
        return template
            .Replace("{{patient_name}}", candidate.PatientName)
            .Replace("{{treatment_type}}", candidate.TreatmentType ?? "tedavi");
    }

    /// <summary>
    /// Start a new treatment lifecycle from an API request.
    /// Creates the followup + all steps with calculated scheduled_at dates.
    /// </summary>
    public async Task<int> StartLifecycleAsync(
        int tenantId, LifecycleStartRequest request, CancellationToken ct = default)
    {
        var lifecycleType = request.LifecycleType
            ?? throw new ArgumentException($"[{ErrorCodes.LifecycleInvalidPayload}] lifecycle_type is required");
        var referenceDateStr = request.ReferenceDate
            ?? throw new ArgumentException($"[{ErrorCodes.LifecycleInvalidPayload}] reference_date is required");
        if (!DateTimeOffset.TryParse(referenceDateStr, out var referenceDate))
            throw new ArgumentException($"[{ErrorCodes.LifecycleInvalidPayload}] reference_date is not valid ISO 8601");
        var patientPhone = request.PatientPhone
            ?? throw new ArgumentException($"[{ErrorCodes.LifecycleInvalidPayload}] patient_phone is required");
        var patientName = request.PatientName
            ?? throw new ArgumentException($"[{ErrorCodes.LifecycleInvalidPayload}] patient_name is required");

        var stepDefs = LifecycleStepDefinitions.GetSteps(lifecycleType);
        if (stepDefs == null)
            throw new ArgumentException($"[{ErrorCodes.LifecycleInvalidType}] Invalid lifecycle type: {lifecycleType}");

        var followupId = await _repository.CreateFollowupAsync(
            tenantId, lifecycleType,
            patientPhone, patientName,
            request.TreatmentType, request.AppointmentId,
            referenceDate, ct);

        var steps = new List<(int stepOrder, string stepKey, string messageTemplate,
            DateTimeOffset scheduledAt, string? escalationTarget)>();

        foreach (var def in stepDefs)
        {
            var scheduledAt = referenceDate.AddHours(def.OffsetHours);

            var resolvedMessage = def.MessageTemplate
                .Replace("{{patient_name}}", patientName)
                .Replace("{{treatment_type}}", request.TreatmentType ?? "tedavi");

            steps.Add((def.StepOrder, def.StepKey, resolvedMessage,
                scheduledAt, def.EscalationTarget));
        }

        await _repository.CreateFollowupStepsAsync(followupId, tenantId, steps, ct);

        _logger.SystemInfo(
            $"Lifecycle started: id={followupId}, type={lifecycleType}, " +
            $"tenant={tenantId}, patient={request.PatientPhone}, steps={steps.Count}");

        return followupId;
    }

    /// <summary>
    /// Handle a patient response: escalate to doctor if complaint detected.
    /// </summary>
    public async Task HandleComplaintEscalationAsync(
        int tenantId, int followupId, DueStepCandidate? stepContext, CancellationToken ct = default)
    {
        if (stepContext == null)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.LifecycleInvalidPayload}] HandleComplaintEscalationAsync: " +
                $"null stepContext for followup={followupId}, tenant={tenantId}");
            return;
        }

        await _repository.MarkStepEscalatedAsync(tenantId, stepContext.StepId, ct);

        var alertCandidate = new DueStepCandidate
        {
            StepId = stepContext.StepId,
            FollowupId = followupId,
            TenantId = tenantId,
            PatientPhone = stepContext.PatientPhone,
            PatientName = stepContext.PatientName,
            LifecycleType = stepContext.LifecycleType,
            TreatmentType = stepContext.TreatmentType,
            EscalationTarget = "doctor"
        };

        await SendEscalationAlertAsync(alertCandidate, ct);

        _logger.SystemInfo(
            $"Complaint escalated to doctor: followup={followupId}, tenant={tenantId}, " +
            $"patient={stepContext.PatientPhone}");
    }
}
