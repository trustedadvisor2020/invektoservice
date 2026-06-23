using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Inma.Dtos;

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
/// Tuning for <see cref="Chatinbox.Shared.Contracts.Inma.WapCrmFeatureGroupCatalogClient"/>.
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

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C4 — the Automation -> Backend INTERNAL proxy request for the
/// 'Set Customer Status' flow action (POST /api/internal/customer-feature-groups/update).
/// Lives in Shared so Automation (sender) and Backend (receiver) share one contract without a
/// cross-service project reference. <see cref="TenantId"/> is cross-checked against the caller's
/// signed service-JWT claim by Backend (it is NOT trusted on its own). Carries NO secret — Backend
/// resolves the tenant WapCRM secret itself. One of <see cref="CustomerId"/> / <see cref="Phone"/>
/// must be present (the handler guards both-empty before calling).
/// </summary>
public sealed class SetCustomerStatusProxyRequest
{
    [JsonPropertyName("tenantId")] public int TenantId { get; init; }
    [JsonPropertyName("customerId")] public int? CustomerId { get; init; }
    [JsonPropertyName("phone")] public string? Phone { get; init; }
    [JsonPropertyName("featureGroupId")] public int FeatureGroupId { get; init; }
    [JsonPropertyName("featureIds")] public IReadOnlyList<int> FeatureIds { get; init; } = Array.Empty<int>();
    [JsonPropertyName("clientRequestId")] public string? ClientRequestId { get; init; }
}

// ============================================================================
// FEAT-INMA-PIPELINE-V2 C4 — cxapi customer-feature-groups UPDATE (WRITE).
// Wire DTOs for POST https://cxapi.wapcrm.net/api/customer-feature-groups/update
// (WapCRM PDF §6.3). The 'Set Customer Status' flow action's write payload, proxied
// through the Backend internal endpoint. FULL-LIST semantics: FeatureIDs[] is the new
// COMPLETE selection for the group (the backend computes add/remove + audits Source='api');
// [] clears the group. Single + Multi only — §6.3 documents NO text-mode (selectionMode=3)
// write, so text groups are disabled in the picker. Request keys are PascalCase per the
// vendor doc examples ({ "CustomerID": 17, "FeatureGroupID": 1, "FeatureIDs": [19] }).
// ============================================================================

/// <summary>
/// Write payload sent to cxapi <c>api/customer-feature-groups/update</c>. Lookup: CustomerID first,
/// Phone fallback (one MUST be non-null; the handler guards both-empty before calling). ClientRequestID
/// echoes back as the webhook event's <c>data.originRequestId</c> (loop-suppress correlation; carries the
/// <c>invekto-</c> prefix). Serialized with explicit PascalCase names so the wire shape is independent of
/// server JsonOptions.
/// </summary>
public sealed class WapCrmFeatureGroupUpdateRequest
{
    [JsonPropertyName("CustomerID")] public int? CustomerId { get; init; }
    [JsonPropertyName("Phone")] public string? Phone { get; init; }
    [JsonPropertyName("FeatureGroupID")] public int FeatureGroupId { get; init; }
    [JsonPropertyName("FeatureIDs")] public IReadOnlyList<int> FeatureIds { get; init; } = Array.Empty<int>();
    [JsonPropertyName("ClientRequestID")] public string? ClientRequestId { get; init; }
}

/// <summary>How the cxapi update round-trip resolved (the client only RETURNS these "vendor answered" cases;
/// it THROWS <see cref="Chatinbox.Shared.Contracts.Inma.WapCrmFeatureGroupUpdateException"/> for transport /
/// timeout / 5xx / malformed / rate-limit / unknown-outcome).</summary>
public enum WapCrmFeatureGroupUpdateOutcome
{
    /// <summary>Provider envelope status=true — the selection was applied.</summary>
    Success,
    /// <summary>Deterministic business rejection (provider statusCode 920/921/922/923/903). NOT retry-safe.</summary>
    BusinessError,
    /// <summary>Auth rejected (HTTP/provider 401/403 — invalid/expired secret). Config class, NOT retry-safe.</summary>
    AuthRejected
}

/// <summary>Result of a cxapi customer-feature-groups update. Never carries the secret.</summary>
public sealed class WapCrmFeatureGroupUpdateResult
{
    public required WapCrmFeatureGroupUpdateOutcome Outcome { get; init; }
    /// <summary>Provider payload statusCode (e.g. "920", "903", "200"); null if the envelope omitted it.</summary>
    public string? ProviderStatusCode { get; init; }
    /// <summary>Provider-supplied message (sanitized vendor text; never a secret). For logging/operator display.</summary>
    public string? ProviderMessage { get; init; }
    /// <summary>HTTP status of the round-trip (for diagnostics).</summary>
    public int HttpStatusCode { get; init; }
}

/// <summary>
/// Tuning for <see cref="Chatinbox.Shared.Contracts.Inma.WapCrmFeatureGroupUpdateClient"/>. Bound from the
/// optional "WapCrmFeatureGroupUpdate" config section; safe defaults apply when absent. Carries NO secrets.
/// <see cref="BaseUrl"/> is a FIXED server-side value — the tenant's own <c>WapCrmSettings.ApiUrl</c> is
/// NEVER used as the egress target (SSRF / secret-exfiltration mitigation), mirroring
/// <see cref="WapCrmFeatureGroupCatalogOptions"/>.
/// </summary>
public sealed class WapCrmFeatureGroupUpdateOptions
{
    public const string SectionName = "WapCrmFeatureGroupUpdate";

    /// <summary>cxapi base URL. The client posts to <c>api/customer-feature-groups/update</c> relative to this.</summary>
    public string BaseUrl { get; set; } = "https://cxapi.wapcrm.net/";

    /// <summary>Per-attempt timeout (ms), enforced by a per-attempt linked CTS inside the client.</summary>
    public int TimeoutMs { get; set; } = 8_000;

    /// <summary>Buffer (ms) added to <see cref="TimeoutMs"/> to derive the FINITE HttpClient.Timeout backstop (floored at 1s).</summary>
    public int HttpClientBackstopBufferMs { get; set; } = 5_000;

    /// <summary>FINITE HttpClient.Timeout hard backstop (ms) = <see cref="TimeoutMs"/> + buffer (buffer floored at 1s) — never Infinite.</summary>
    public int HttpClientBackstopMs => TimeoutMs + Math.Max(HttpClientBackstopBufferMs, 1_000);
}
