using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Leads;

/// <summary>
/// FEAT-LIW Chunk C: GET /api/v1/tenant/landing/settings response envelope.
/// Consumed by the Dashboard LeadIntakeSettingsPage. Returns the tenant's
/// current LIW configuration with keys masked (last 4 chars visible) and a
/// live flow_status lookup for the FlowWarningBanner. Also serves first-time
/// tenants — when no tenant_landing_settings row exists yet, the endpoint
/// returns a sentinel envelope (has_active_key=false, masked_active_key=null,
/// updated_at=null) so the UI can render the "Generate first key" state.
/// row_version is the tenant_landing_settings.updated_at ISO string used for
/// optimistic concurrency on subsequent mutations.
/// </summary>
public sealed class TenantLandingSettingsDto
{
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    /// <summary>Masked active key ('liw_live_****abcd') or null when not initialized / revoked.</summary>
    [JsonPropertyName("masked_active_key")]
    public string? MaskedActiveKey { get; set; }

    /// <summary>Masked previous key during the 24h grace window; null when no grace in progress.</summary>
    [JsonPropertyName("masked_old_key")]
    public string? MaskedOldKey { get; set; }

    /// <summary>Grace-window expiry for the old key; null when no grace in progress.</summary>
    [JsonPropertyName("old_expires_at")]
    public DateTime? OldExpiresAt { get; set; }

    /// <summary>
    /// Parsed landing_field_map as <b>{ source_field -> canonical_field }</b> — the
    /// UI-natural authoring direction (tenants think in terms of their form field
    /// names first). The stored JSONB shape is the inverse (canonical -> source);
    /// LiwSettingsService.BuildSettingsDto inverts it on GET and SerializeFieldMapForStorage
    /// inverts it back on PUT. Does NOT include the reserved 'phone.country_hint' key
    /// (surfaced separately on <see cref="PhoneCountryHint"/> for UI clarity; the save
    /// endpoint folds it back into the JSONB).
    /// </summary>
    [JsonPropertyName("field_map")]
    public Dictionary<string, string> FieldMap { get; set; } = new();

    /// <summary>Phone country hint (ISO 3166-1 alpha-2) or null.</summary>
    [JsonPropertyName("phone_country_hint")]
    public string? PhoneCountryHint { get; set; }

    /// <summary>Welcome flow slug configured by the tenant; null falls back to 'welcome_default' at runtime.</summary>
    [JsonPropertyName("welcome_flow_slug")]
    public string? WelcomeFlowSlug { get; set; }

    [JsonPropertyName("dup_window_days")]
    public int DupWindowDays { get; set; }

    /// <summary>
    /// Live chatbot_flows existence check for the resolved welcome slug. When
    /// exists=false the Dashboard renders FlowWarningBanner so the tenant fixes
    /// the config gap BEFORE a real lead arrives.
    /// </summary>
    [JsonPropertyName("flow_status")]
    public FlowStatusDto FlowStatus { get; set; } = new();

    /// <summary>row_version — ISO string of tenant_landing_settings.updated_at. Null when no row exists yet.</summary>
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>Convenience flag: masked_active_key != null (UI doesn't need to null-check the masked string).</summary>
    [JsonPropertyName("has_active_key")]
    public bool HasActiveKey { get; set; }
}

public sealed class FlowStatusDto
{
    /// <summary>Slug used at runtime (welcome_flow_slug or 'welcome_default' fallback).</summary>
    [JsonPropertyName("resolved_slug")]
    public string ResolvedSlug { get; set; } = "welcome_default";

    /// <summary>True when an active chatbot_flows row matches (tenant_id, flow_name=resolved_slug, is_active=true).</summary>
    [JsonPropertyName("exists")]
    public bool Exists { get; set; }

    [JsonPropertyName("flow_id")]
    public int? FlowId { get; set; }

    [JsonPropertyName("flow_display_name")]
    public string? FlowDisplayName { get; set; }
}
