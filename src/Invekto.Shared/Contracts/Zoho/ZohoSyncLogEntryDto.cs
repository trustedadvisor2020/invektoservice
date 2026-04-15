namespace Invekto.Shared.Contracts.Zoho;

/// <summary>
/// Adim 3 Paket 3-B1: Dashboard sync log listing icin tek satir projeksiyonu.
/// Source table: zoho_sync_log. UpdatedAt listeleme sirasi icin kullanilir.
/// </summary>
public sealed class ZohoSyncLogEntryDto
{
    public required long Id { get; init; }
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

/// <summary>
/// Adim 3 Paket 3-B1: paginated sync log response (GET /api/v1/zoho/sync-log).
/// Offset-based pagination: Items = current page; TotalCount = filtered total.
/// </summary>
public sealed class ZohoSyncLogPageResponse
{
    public required IReadOnlyList<ZohoSyncLogEntryDto> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
}

/// <summary>
/// Adim 3 Paket 3-B1: sync log retry response (POST /api/v1/zoho/sync-log/{id}/retry).
/// Success path: RetriedId set. Failure paths return ErrorResponse envelope (not this DTO).
/// </summary>
public sealed class ZohoSyncLogRetryResponse
{
    public required long RetriedId { get; init; }
    public required int NewAttemptCount { get; init; }
}
