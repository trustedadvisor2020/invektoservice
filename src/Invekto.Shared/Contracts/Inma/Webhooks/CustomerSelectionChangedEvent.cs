using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Inma.Webhooks;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C2 — INMA's SIGNED system event posted to the existing
/// /api/v1/webhook/event?companyId=X webhook when an agent changes a customer's
/// feature-group selection. Discriminated from inbound messages / delivery acks by the
/// root <c>type</c> == "customer.selection_changed" (messages have a root <c>messages[]</c>,
/// acks have <c>Status</c>/<c>InstanceMessageID</c> — never a root <c>type</c>).
///
/// Camel-case wire shape per arch/contracts/inma-customer-status-webhook.json v2.0 (FINAL,
/// INMA's official answer). The body <c>companyId</c> is INMA's OWN Company ID (e.g. 11 for
/// tenant 5050) — it is cross-checked/audited only; the authoritative tenant is the
/// query-string ?companyId resolved into TenantContext upstream.
/// </summary>
public sealed class CustomerSelectionChangedEvent
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("version")]
    public int? Version { get; init; }

    [JsonPropertyName("occurredAt")]
    public DateTimeOffset? OccurredAt { get; init; }

    /// <summary>INMA's own Company ID (NOT our tenant_id). Cross-check/audit only.</summary>
    [JsonPropertyName("companyId")]
    public int? CompanyId { get; init; }

    [JsonPropertyName("actor")]
    public SelectionActor? Actor { get; init; }

    [JsonPropertyName("data")]
    public SelectionData? Data { get; init; }

    /// <summary>The single root discriminator value. Matched as an exact const.</summary>
    public const string TypeValue = "customer.selection_changed";
}

/// <summary>Who triggered the change. C2 only PERSISTS this for C3's suppression logic.</summary>
public sealed class SelectionActor
{
    /// <summary>"user" (panel agent), "api" (external API incl. our own flow action), "system", "zoho-webhook".</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("id")]
    public int? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public sealed class SelectionData
{
    [JsonPropertyName("customerId")]
    public int? CustomerId { get; init; }

    /// <summary>First active phone, digits only (country code NOT guaranteed). May be absent.</summary>
    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    /// <summary>ALL active phones (digits, de-duplicated). The most robust match surface.</summary>
    [JsonPropertyName("phones")]
    public List<string>? Phones { get; init; }

    [JsonPropertyName("featureGroupId")]
    public int? FeatureGroupId { get; init; }

    [JsonPropertyName("featureGroupName")]
    public string? FeatureGroupName { get; init; }

    /// <summary>"single", "multi", or "text".</summary>
    [JsonPropertyName("selectionMode")]
    public string? SelectionMode { get; init; }

    /// <summary>Echo of our update ClientRequestID (loop-suppress key — used by C3, persisted here).</summary>
    [JsonPropertyName("originRequestId")]
    public string? OriginRequestId { get; init; }

    [JsonPropertyName("before")]
    public SelectionState? Before { get; init; }

    [JsonPropertyName("after")]
    public SelectionState? After { get; init; }
}

public sealed class SelectionState
{
    [JsonPropertyName("featureIds")]
    public List<int>? FeatureIds { get; init; }

    [JsonPropertyName("featureNames")]
    public List<string>? FeatureNames { get; init; }

    /// <summary>Only for selectionMode == "text". Absent means "no value in this direction".</summary>
    [JsonPropertyName("textValue")]
    public string? TextValue { get; init; }
}

/// <summary>
/// Pure, host-free derivation of the OPAQUE leads.customer_status from an event's after-state.
/// Extracted as a tested static (mirrors <c>WapCrmAckMapping</c>) so the one piece of real logic
/// in the C2 branch is unit-covered without a Backend test host. Rules per contract v2.0:
///   single -> after.featureNames[0]
///   featureIds empty (cleared) -> null
///   multi   -> after.featureNames joined alphabetically (", ")
///   text    -> after.textValue (absent/empty -> null)
/// </summary>
public static class CustomerStatusMapping
{
    public const string MultiSeparator = ", ";

    public static string? Derive(string? selectionMode, SelectionState? after)
    {
        if (after is null) return null;

        var mode = selectionMode?.Trim().ToLowerInvariant();

        if (mode == "text")
            return string.IsNullOrEmpty(after.TextValue) ? null : after.TextValue;

        // Non-text modes are driven by the feature set. An empty set means "cleared" -> null.
        var ids = after.FeatureIds;
        if (ids is null || ids.Count == 0) return null;

        var names = after.FeatureNames;
        if (names is null || names.Count == 0) return null;

        if (mode == "single")
            return names[0];

        // "multi" (and any other set-bearing mode) -> alphabetical CSV of names.
        return string.Join(MultiSeparator, names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
    }
}
