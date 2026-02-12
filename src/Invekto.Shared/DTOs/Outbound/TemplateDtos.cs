using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Outbound;

/// <summary>
/// POST /api/v1/templates request body.
/// </summary>
public sealed class TemplateCreateRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("trigger_event")]
    public string TriggerEvent { get; set; } = "manual";

    [JsonPropertyName("message_template")]
    public string MessageTemplate { get; set; } = "";

    [JsonPropertyName("variables_json")]
    public Dictionary<string, string>? VariablesJson { get; set; }
}

/// <summary>
/// PUT /api/v1/templates/{id} request body. All fields optional.
/// </summary>
public sealed class TemplateUpdateRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("trigger_event")]
    public string? TriggerEvent { get; set; }

    [JsonPropertyName("message_template")]
    public string? MessageTemplate { get; set; }

    [JsonPropertyName("variables_json")]
    public Dictionary<string, string>? VariablesJson { get; set; }
}

/// <summary>
/// Template list item in GET /api/v1/templates response.
/// </summary>
public sealed class TemplateDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("trigger_event")]
    public string TriggerEvent { get; set; } = "";

    [JsonPropertyName("message_template")]
    public string MessageTemplate { get; set; } = "";

    [JsonPropertyName("variables_json")]
    public Dictionary<string, string>? VariablesJson { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
