using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Inma.Dtos;

// ============================================================================
// FEAT-INMA-PIPELINE-V2 C3b — cxapi customer-feature-groups CATALOG (READ-ONLY).
// Wire DTOs for POST https://cxapi.wapcrm.net/api/customer-feature-groups/catalog
// (WapCRM PDF §6.1). Powers the Flow Builder `customer_status_changed` trigger's
// feature_group_id picker via a Backend proxy + 24h cache. There is NO write path
// here (C4 = .../update). The verified response shape lives durably in
// arch/contracts/inma-customer-status-webhook.json (feature_groups_catalog).
// ============================================================================

/// <summary>One feature group from the catalog <c>data[]</c> (cxapi camelCase wire shape).</summary>
public sealed class WapCrmFeatureGroupCatalogDto
{
    /// <summary>Feature group id (= the webhook event's <c>featureGroupId</c>; the trigger stores it as <c>feature_group_id</c>).</summary>
    [JsonPropertyName("id")] public int Id { get; init; }

    /// <summary>Stable system key (e.g. <c>customer_stage</c>). Present in the catalog only (not the webhook event).</summary>
    [JsonPropertyName("systemKey")] public string? SystemKey { get; init; }

    /// <summary>Group display name (e.g. "Lead Aşaması").</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>1 = multi (çoklu), 2 = single (tek), 3 = text (metin). INTEGER in the catalog.</summary>
    [JsonPropertyName("selectionMode")] public int SelectionMode { get; init; }

    [JsonPropertyName("sortOrder")] public int SortOrder { get; init; }

    /// <summary>Selectable options within the group (<c>{ id, name, rgbCode }</c>).</summary>
    [JsonPropertyName("features")] public List<WapCrmFeatureDto>? Features { get; init; }

    /// <summary>Channel whitelist: empty = all channels; populated = only those instances.</summary>
    [JsonPropertyName("instanceIDs")] public List<int>? InstanceIDs { get; init; }
}

/// <summary>One selectable option inside a feature group (cxapi <c>features[]</c>).</summary>
public sealed class WapCrmFeatureDto
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("rgbCode")] public string? RgbCode { get; init; }
}

/// <summary>
/// Trimmed, SPA-facing projection returned by the Backend proxy. Drops vendor-internal
/// fields (systemKey / rgbCode / sortOrder / instanceIDs) — the Flow Builder only needs
/// <see cref="Id"/> (picker value), <see cref="Name"/> (label), <see cref="SelectionMode"/>
/// (text-mode disable) and the option names (read-only "fires on any change in this group"
/// explanation). camelCase is pinned so the SPA contract is independent of server JsonOptions.
/// </summary>
public sealed class CustomerFeatureGroupView
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("selectionMode")] public int SelectionMode { get; init; }
    [JsonPropertyName("features")] public IReadOnlyList<CustomerFeatureView> Features { get; init; } = Array.Empty<CustomerFeatureView>();
}

/// <summary>Trimmed option (read-only context for the over-fire explanation; not selectable as a from/to filter).</summary>
public sealed class CustomerFeatureView
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
}

/// <summary>
/// Tuning for <see cref="Invekto.Shared.Contracts.Inma.WapCrmFeatureGroupCatalogClient"/>.
/// Bound from the optional "WapCrmFeatureGroups" config section; safe defaults apply when
/// absent. Carries NO secrets. <see cref="BaseUrl"/> is a FIXED server-side value — the
/// tenant's own <c>WapCrmSettings.ApiUrl</c> is intentionally NEVER used as the egress
/// target (SSRF / secret-exfiltration mitigation), mirroring <see cref="WapCrmTemplateOptions"/>.
/// </summary>
public sealed class WapCrmFeatureGroupCatalogOptions
{
    public const string SectionName = "WapCrmFeatureGroups";

    /// <summary>cxapi base URL. The client posts to <c>api/customer-feature-groups/catalog</c> relative to this.</summary>
    public string BaseUrl { get; set; } = "https://cxapi.wapcrm.net/";

    /// <summary>Per-attempt timeout (ms), enforced by a per-attempt linked CTS inside the client.</summary>
    public int TimeoutMs { get; set; } = 10_000;

    /// <summary>Buffer (ms) added to <see cref="TimeoutMs"/> to derive the FINITE HttpClient.Timeout backstop (floored at 1s).</summary>
    public int HttpClientBackstopBufferMs { get; set; } = 5_000;

    /// <summary>FINITE HttpClient.Timeout hard backstop (ms) = <see cref="TimeoutMs"/> + buffer (buffer floored at 1s) — never Infinite.</summary>
    public int HttpClientBackstopMs => TimeoutMs + Math.Max(HttpClientBackstopBufferMs, 1_000);
}
