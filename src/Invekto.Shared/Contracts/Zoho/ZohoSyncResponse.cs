namespace Invekto.Shared.Contracts.Zoho;

/// <summary>
/// Response for POST /api/internal/zoho/sync.
/// Success = Zoho Blueprint transition committed; Failure = error code + last_error in sync_log.
/// </summary>
public sealed class ZohoSyncResponse
{
    public required bool Success { get; init; }

    /// <summary>Sync log row id (zoho_sync_log.id) for follow-up / retry operations.</summary>
    public required long SyncLogId { get; init; }

    /// <summary>Terminal status: "success" or "failed". Intermediate "pending" never returned here.</summary>
    public required string Status { get; init; }

    /// <summary>Zoho CRM Lead id used/resolved in this sync attempt.</summary>
    public string? ZohoLeadId { get; init; }

    /// <summary>Blueprint transition id executed on success.</summary>
    public string? ZohoTransitionId { get; init; }

    /// <summary>INV-INT-120..124 when Success=false.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Human-readable error detail when Success=false.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Number of attempts already made on this sync_log row (1 for first call).</summary>
    public required int AttemptCount { get; init; }
}
