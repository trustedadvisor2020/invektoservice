using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.Outbound;

/// <summary>
/// Consent record request (GR-3.26).
/// </summary>
public sealed class ConsentRecordRequest
{
    [JsonPropertyName("customer_phone")]
    public string CustomerPhone { get; set; } = "";

    [JsonPropertyName("consent_type")]
    public string ConsentType { get; set; } = "all"; // marketing, utility, all

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = ""; // whatsapp, web_form, order_confirmation, appointment

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("opted_in")]
    public bool OptedIn { get; set; } = true;
}

/// <summary>
/// Consent record response.
/// </summary>
public sealed class ConsentRecordResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("customer_phone")]
    public string CustomerPhone { get; set; } = "";

    [JsonPropertyName("consent_type")]
    public string ConsentType { get; set; } = "";

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; set; }

    [JsonPropertyName("opted_in")]
    public bool OptedIn { get; set; }

    [JsonPropertyName("opted_in_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? OptedInAt { get; set; }

    [JsonPropertyName("opted_out_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? OptedOutAt { get; set; }
}

/// <summary>
/// Consent check result.
/// </summary>
public sealed class ConsentCheckResponse
{
    [JsonPropertyName("customer_phone")]
    public string CustomerPhone { get; set; } = "";

    [JsonPropertyName("has_marketing_consent")]
    public bool HasMarketingConsent { get; set; }

    [JsonPropertyName("has_utility_consent")]
    public bool HasUtilityConsent { get; set; }

    [JsonPropertyName("consent_records")]
    public List<ConsentRecordResponse> Records { get; set; } = new();
}

/// <summary>
/// Data deletion request (GR-3.29).
/// </summary>
public sealed class DataDeletionRequest
{
    [JsonPropertyName("customer_phone")]
    public string CustomerPhone { get; set; } = "";

    [JsonPropertyName("requested_by")]
    public string? RequestedBy { get; set; }
}

/// <summary>
/// Data deletion status response.
/// </summary>
public sealed class DataDeletionResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("customer_phone")]
    public string CustomerPhone { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("services_cleaned")]
    public List<string> ServicesCleaned { get; set; } = new();

    [JsonPropertyName("error_message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("completed_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}
