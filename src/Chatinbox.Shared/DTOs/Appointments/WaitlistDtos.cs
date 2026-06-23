using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.Appointments;

/// <summary>
/// Waitlist entry response. GET /api/v1/waitlist
/// </summary>
public sealed class WaitlistDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("patient_phone")]
    public string PatientPhone { get; set; } = "";

    [JsonPropertyName("patient_name")]
    public string PatientName { get; set; } = "";

    [JsonPropertyName("preferred_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreferredDate { get; set; }

    [JsonPropertyName("preferred_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreferredTime { get; set; }

    [JsonPropertyName("service_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceType { get; set; }

    [JsonPropertyName("doctor_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DoctorId { get; set; }

    /// <summary>waiting | notified | booked | expired | cancelled</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "waiting";

    [JsonPropertyName("notified_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? NotifiedAt { get; set; }

    [JsonPropertyName("expires_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ExpiresAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// POST /api/v1/waitlist request body.
/// </summary>
public sealed class WaitlistCreateRequest
{
    [JsonPropertyName("patient_phone")]
    public string PatientPhone { get; set; } = "";

    [JsonPropertyName("patient_name")]
    public string PatientName { get; set; } = "";

    /// <summary>yyyy-MM-dd format (optional)</summary>
    [JsonPropertyName("preferred_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreferredDate { get; set; }

    /// <summary>HH:mm format (optional)</summary>
    [JsonPropertyName("preferred_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreferredTime { get; set; }

    [JsonPropertyName("service_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceType { get; set; }

    [JsonPropertyName("doctor_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DoctorId { get; set; }
}

/// <summary>
/// No-show statistics for a patient. GET /api/v1/appointments/no-show-stats
/// </summary>
public sealed class NoShowStatsDto
{
    [JsonPropertyName("patient_phone")]
    public string PatientPhone { get; set; } = "";

    [JsonPropertyName("no_show_count")]
    public int NoShowCount { get; set; }

    [JsonPropertyName("total_appointments")]
    public int TotalAppointments { get; set; }

    [JsonPropertyName("no_show_rate")]
    public double NoShowRate { get; set; }

    [JsonPropertyName("exceeds_threshold")]
    public bool ExceedsThreshold { get; set; }

    [JsonPropertyName("threshold")]
    public int Threshold { get; set; }
}
