using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.Appointments;

/// <summary>
/// Appointment read/list response item. GET /api/v1/appointments
/// </summary>
public sealed class AppointmentDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("slot_id")]
    public int SlotId { get; set; }

    [JsonPropertyName("doctor_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DoctorId { get; set; }

    [JsonPropertyName("patient_name")]
    public string PatientName { get; set; } = "";

    [JsonPropertyName("patient_phone")]
    public string PatientPhone { get; set; } = "";

    /// <summary>yyyy-MM-dd format</summary>
    [JsonPropertyName("appointment_date")]
    public string AppointmentDate { get; set; } = "";

    /// <summary>HH:mm format</summary>
    [JsonPropertyName("start_time")]
    public string StartTime { get; set; } = "";

    /// <summary>HH:mm format</summary>
    [JsonPropertyName("end_time")]
    public string EndTime { get; set; } = "";

    /// <summary>confirmed | cancelled | completed | no_show</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "confirmed";

    [JsonPropertyName("reminder_48h_sent")]
    public bool Reminder48hSent { get; set; }

    [JsonPropertyName("reminder_2h_sent")]
    public bool Reminder2hSent { get; set; }

    [JsonPropertyName("cancel_reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CancelReason { get; set; }

    [JsonPropertyName("notes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// POST /api/v1/appointments/book request body.
/// </summary>
public sealed class AppointmentBookRequest
{
    [JsonPropertyName("slot_id")]
    public int SlotId { get; set; }

    [JsonPropertyName("patient_name")]
    public string PatientName { get; set; } = "";

    [JsonPropertyName("patient_phone")]
    public string PatientPhone { get; set; } = "";

    /// <summary>yyyy-MM-dd format</summary>
    [JsonPropertyName("appointment_date")]
    public string AppointmentDate { get; set; } = "";

    [JsonPropertyName("notes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; set; }
}

/// <summary>
/// POST /api/v1/appointments/{id}/cancel request body.
/// </summary>
public sealed class AppointmentCancelRequest
{
    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }
}

/// <summary>
/// Available slot item in GET /api/v1/appointments/available-slots response.
/// Shows a slot definition with current booking count for the requested date.
/// </summary>
public sealed class AvailableSlotDto
{
    [JsonPropertyName("slot_id")]
    public int SlotId { get; set; }

    [JsonPropertyName("start_time")]
    public string StartTime { get; set; } = "";

    [JsonPropertyName("end_time")]
    public string EndTime { get; set; } = "";

    [JsonPropertyName("max_bookings")]
    public int MaxBookings { get; set; }

    [JsonPropertyName("current_bookings")]
    public int CurrentBookings { get; set; }

    [JsonPropertyName("available")]
    public bool Available => CurrentBookings < MaxBookings;

    [JsonPropertyName("doctor_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DoctorId { get; set; }
}

/// <summary>
/// Internal DTO for reminder scheduler. Not exposed via API.
/// </summary>
public sealed class ReminderCandidate
{
    public long AppointmentId { get; set; }
    public int TenantId { get; set; }
    public string PatientName { get; set; } = "";
    public string PatientPhone { get; set; } = "";
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? CallbackUrl { get; set; }
}
