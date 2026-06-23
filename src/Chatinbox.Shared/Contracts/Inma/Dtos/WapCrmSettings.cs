using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Inma.Dtos;

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

    /// <summary>
    /// FEAT-PROJELER / cxapi (PR-3a): tenant-default WapCRM instance (cxapi <c>instanceID</c>)
    /// used as the send channel for plain-text bulk cutover. Resolved at broadcast-create and
    /// stamped immutably onto the broadcast + message rows. Nullable: a cxapi-allowlisted tenant
    /// without it is rejected at create (INV-OB-062). bulk_send_jobs / project-level instance is PR-4/PKT-2.
    /// </summary>
    [JsonPropertyName("instance_id")]
    public int? InstanceId { get; init; }
}
