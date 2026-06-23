using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.Appointments;

// ================================================================
// GR-3.20 + GR-3.41 + GR-3.43: Treatment Lifecycle DTOs
// ================================================================

/// <summary>
/// Request to start a treatment lifecycle follow-up chain.
/// </summary>
public sealed class LifecycleStartRequest
{
    [JsonPropertyName("lifecycle_type")]
    public string? LifecycleType { get; set; }

    [JsonPropertyName("patient_phone")]
    public string? PatientPhone { get; set; }

    [JsonPropertyName("patient_name")]
    public string? PatientName { get; set; }

    [JsonPropertyName("treatment_type")]
    public string? TreatmentType { get; set; }

    [JsonPropertyName("appointment_id")]
    public long? AppointmentId { get; set; }

    /// <summary>
    /// ISO 8601 datetime string. For post_treatment: treatment completion date.
    /// For plan_approval: plan sent date. For pre_op: appointment date.
    /// </summary>
    [JsonPropertyName("reference_date")]
    public string? ReferenceDate { get; set; }
}

/// <summary>
/// Request to record a patient response to a lifecycle step.
/// </summary>
public sealed class LifecycleResponseRequest
{
    [JsonPropertyName("step_id")]
    public int StepId { get; set; }

    [JsonPropertyName("response_text")]
    public string? ResponseText { get; set; }

    [JsonPropertyName("complaint_detected")]
    public bool ComplaintDetected { get; set; }
}

/// <summary>
/// Treatment lifecycle instance (returned by list/get endpoints).
/// </summary>
public sealed class LifecycleDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("lifecycle_type")]
    public string LifecycleType { get; set; } = "";

    [JsonPropertyName("patient_phone")]
    public string PatientPhone { get; set; } = "";

    [JsonPropertyName("patient_name")]
    public string PatientName { get; set; } = "";

    [JsonPropertyName("treatment_type")]
    public string? TreatmentType { get; set; }

    [JsonPropertyName("appointment_id")]
    public long? AppointmentId { get; set; }

    [JsonPropertyName("reference_date")]
    public string ReferenceDate { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("steps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LifecycleStepDto>? Steps { get; set; }
}

/// <summary>
/// Individual step within a treatment lifecycle.
/// </summary>
public sealed class LifecycleStepDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("step_order")]
    public int StepOrder { get; set; }

    [JsonPropertyName("step_key")]
    public string StepKey { get; set; } = "";

    [JsonPropertyName("scheduled_at")]
    public string ScheduledAt { get; set; } = "";

    [JsonPropertyName("sent_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SentAt { get; set; }

    [JsonPropertyName("patient_responded")]
    public bool PatientResponded { get; set; }

    [JsonPropertyName("response_text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResponseText { get; set; }

    [JsonPropertyName("complaint_detected")]
    public bool ComplaintDetected { get; set; }

    [JsonPropertyName("escalated")]
    public bool Escalated { get; set; }

    [JsonPropertyName("escalation_target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EscalationTarget { get; set; }
}

/// <summary>
/// Candidate step returned by GetDueStepsAsync for processing by TreatmentLifecycleService.
/// </summary>
public sealed class DueStepCandidate
{
    public int StepId { get; set; }
    public int FollowupId { get; set; }
    public int TenantId { get; set; }
    public string StepKey { get; set; } = "";
    public string MessageTemplate { get; set; } = "";
    public string PatientPhone { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string LifecycleType { get; set; } = "";
    public string? TreatmentType { get; set; }
    public string? EscalationTarget { get; set; }
    public int StepOrder { get; set; }
    public int TotalSteps { get; set; }
}
