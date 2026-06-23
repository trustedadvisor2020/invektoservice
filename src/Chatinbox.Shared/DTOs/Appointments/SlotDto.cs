using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.Appointments;

/// <summary>
/// Slot read/list response item. GET /api/v1/slots
/// </summary>
public sealed class SlotDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("doctor_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DoctorId { get; set; }

    /// <summary>0=Sunday, 1=Monday ... 6=Saturday</summary>
    [JsonPropertyName("day_of_week")]
    public int DayOfWeek { get; set; }

    /// <summary>HH:mm format</summary>
    [JsonPropertyName("start_time")]
    public string StartTime { get; set; } = "";

    /// <summary>HH:mm format</summary>
    [JsonPropertyName("end_time")]
    public string EndTime { get; set; } = "";

    [JsonPropertyName("max_bookings")]
    public int MaxBookings { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// POST /api/v1/slots request body.
/// </summary>
public sealed class SlotCreateRequest
{
    /// <summary>0=Sunday, 1=Monday ... 6=Saturday</summary>
    [JsonPropertyName("day_of_week")]
    public int DayOfWeek { get; set; }

    /// <summary>HH:mm format (e.g. "09:00")</summary>
    [JsonPropertyName("start_time")]
    public string StartTime { get; set; } = "";

    /// <summary>HH:mm format (e.g. "10:00")</summary>
    [JsonPropertyName("end_time")]
    public string EndTime { get; set; } = "";

    [JsonPropertyName("max_bookings")]
    public int MaxBookings { get; set; } = 1;

    [JsonPropertyName("doctor_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DoctorId { get; set; }
}

/// <summary>
/// PUT /api/v1/slots/{id} request body. All fields optional.
/// </summary>
public sealed class SlotUpdateRequest
{
    [JsonPropertyName("day_of_week")]
    public int? DayOfWeek { get; set; }

    [JsonPropertyName("start_time")]
    public string? StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public string? EndTime { get; set; }

    [JsonPropertyName("max_bookings")]
    public int? MaxBookings { get; set; }

    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }

    [JsonPropertyName("doctor_id")]
    public int? DoctorId { get; set; }
}
