using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Inma.Dtos;

/// <summary>
/// WapCRM integration settings stored in tenant_registry.settings_json->'wapcrm'.
/// Maps snake_case JSON keys from DB to PascalCase C# properties.
/// </summary>
public sealed class WapCrmSettings
{
    [JsonPropertyName("secret_key")]
    public string? SecretKey { get; init; }

    [JsonPropertyName("api_url")]
    public string? ApiUrl { get; init; }

    [JsonPropertyName("user_id")]
    public int UserId { get; init; }
}
