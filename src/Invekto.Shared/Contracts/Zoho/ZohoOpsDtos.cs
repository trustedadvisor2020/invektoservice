namespace Invekto.Shared.Contracts.Zoho;

/// <summary>
/// Adim 3 Paket 3-C: Super-admin cross-tenant ops dashboard DTOs.
/// Tum response'larda tenant_id explicit — ops kullanicisi hangi firmaya ait oldugunu gorur.
/// </summary>
public sealed class ZohoOpsConnectionEntryDto
{
    public required int TenantId { get; init; }
    public required string Region { get; init; }
    public string? ZohoUserEmail { get; init; }
    public required DateTimeOffset ConnectedAt { get; init; }
    public DateTimeOffset? LastRefreshedAt { get; init; }
    public DateTimeOffset? DisconnectedAt { get; init; }
}

public sealed class ZohoOpsConnectionListResponse
{
    public required IReadOnlyList<ZohoOpsConnectionEntryDto> Items { get; init; }
    public required int ConnectedCount { get; init; }
    public required int DisconnectedCount { get; init; }
    public required int FailedLast24hCount { get; init; }
}

public sealed class ZohoOpsSyncLogEntryDto
{
    public required long Id { get; init; }
    public required int TenantId { get; init; }
    public required string ZohoEvent { get; init; }
    public required string SourceLeadId { get; init; }
    public string? ZohoLeadId { get; init; }
    public required string Status { get; init; }
    public required int AttemptCount { get; init; }
    public string? LastErrorCode { get; init; }
    public string? LastErrorMessage { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

public sealed class ZohoOpsSyncLogPageResponse
{
    public required IReadOnlyList<ZohoOpsSyncLogEntryDto> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
}

/// <summary>
/// Batch retry request: max 50 ids per call. Ids may span multiple tenants;
/// tenant_id is resolved server-side from each row (cross-tenant trust via shared-secret).
/// </summary>
public sealed class ZohoOpsBatchRetryRequest
{
    public required IReadOnlyList<long> Ids { get; init; }
}

/// <summary>
/// Per-id skip reporting: a row may be skipped because it's not in 'failed' status,
/// or the id does not exist. UI shows reason banner.
/// </summary>
public sealed class ZohoOpsBatchRetrySkipEntry
{
    public required long Id { get; init; }
    public required string Reason { get; init; }
}

public sealed class ZohoOpsBatchRetryResponse
{
    public required int Requested { get; init; }
    public required int Updated { get; init; }
    public required IReadOnlyList<ZohoOpsBatchRetrySkipEntry> Skipped { get; init; }
}

public sealed class ZohoOpsDisconnectResponse
{
    public required int TenantId { get; init; }
    public required bool TokenRevoked { get; init; }
}
