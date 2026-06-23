using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Leads;

/// <summary>
/// FEAT-LIW Chunk C: POST /api/v1/tenant/landing/apikey/rotate response.
/// Returns the PLAINTEXT new active key exactly once (caller must copy it
/// immediately; backend stores only the value and subsequent GETs will return
/// the masked form). <see cref="MaskedOld"/> + <see cref="OldExpiresAt"/>
/// describe the 24h grace window for the previous key; both are null on
/// first-time rotation (no old key to grace). <see cref="RowVersion"/> is the
/// new tenant_landing_settings.updated_at — client threads it into the next
/// mutation's If-Match header.
/// </summary>
public sealed class RotateApiKeyResponse
{
    [JsonPropertyName("active_plaintext")]
    public string ActivePlaintext { get; set; } = "";

    [JsonPropertyName("masked_old")]
    public string? MaskedOld { get; set; }

    [JsonPropertyName("old_expires_at")]
    public DateTime? OldExpiresAt { get; set; }

    [JsonPropertyName("row_version")]
    public DateTime RowVersion { get; set; }
}
