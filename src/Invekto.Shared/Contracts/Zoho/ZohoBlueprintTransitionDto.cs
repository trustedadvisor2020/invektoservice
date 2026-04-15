namespace Invekto.Shared.Contracts.Zoho;

/// <summary>
/// Adim 4: Single Blueprint transition projection for Stage Mapping editor dropdown.
/// Module-level (state-bagimsiz). From GET /api/v1/zoho/blueprint/transitions.
/// </summary>
public sealed class ZohoBlueprintTransitionDto
{
    public required string TransitionId { get; init; }
    public required string Name { get; init; }
    public string? NextState { get; init; }
}

public sealed class ZohoBlueprintTransitionsResponse
{
    public required IReadOnlyList<ZohoBlueprintTransitionDto> Items { get; init; }
    public required bool FromCache { get; init; }
}

/// <summary>
/// Adim 4: Stage mapping test (dry-run) request. Validates transition_id against live Blueprint transitions whitelist.
/// No Zoho PUT performed.
/// </summary>
public sealed class ZohoStageMappingTestRequest
{
    public required string ZohoEvent { get; init; }
    public required string ZohoTransitionId { get; init; }
}

public sealed class ZohoStageMappingTestResponse
{
    public required bool Valid { get; init; }
    public string? TransitionName { get; init; }
    public string? NextState { get; init; }
    public string? Reason { get; init; }
    /// <summary>INV-INT-122 when Valid=false (transition not in blueprint whitelist); null when Valid=true.</summary>
    public string? ErrorCode { get; init; }
}
