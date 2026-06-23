using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Leads;

/// <summary>
/// FEAT-LIW Chunk C: service-to-service response shape for
/// GET /api/v1/automation/flows/lookup. Backend calls this endpoint (via
/// <c>FlowLookupClient</c>) to populate TenantLandingSettingsDto.FlowStatus
/// without doing a direct cross-service DB read. The canonical
/// microservice isolation rule (CLAUDE.md) allows HTTP, not direct DB,
/// for cross-service reads — this DTO is that HTTP contract.
/// <see cref="Exists"/> is false when no active chatbot_flows row matches
/// (tenant_id, flow_name); the other fields are null in that case.
/// </summary>
public sealed class FlowLookupResponse
{
    [JsonPropertyName("exists")]
    public bool Exists { get; set; }

    [JsonPropertyName("flow_id")]
    public int? FlowId { get; set; }

    [JsonPropertyName("flow_name")]
    public string? FlowName { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}
