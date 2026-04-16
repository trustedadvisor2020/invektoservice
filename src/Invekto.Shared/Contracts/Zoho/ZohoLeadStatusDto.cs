namespace Invekto.Shared.Contracts.Zoho;

/// <summary>
/// Adim 4 P4.1: Single Lead_Status picklist value (from Zoho /crm/v6/settings/fields).
/// Used by Stage Mapping editor info banner + manuel input mode hint.
/// </summary>
public sealed class ZohoLeadStatusDto
{
    public required string Value { get; init; }
    public required string DisplayValue { get; init; }
}

public sealed class ZohoLeadStatusListResponse
{
    public required IReadOnlyList<ZohoLeadStatusDto> Items { get; init; }
    public required bool FromCache { get; init; }
}
