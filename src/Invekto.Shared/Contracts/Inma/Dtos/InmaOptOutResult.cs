namespace Invekto.Shared.Contracts.Inma.Dtos;

/// <summary>
/// FEAT-J2: Internal normalized result returned by IInmaContactOptOutClient.
/// Decouples consumers (InmaOptOutSyncJob) from INMA raw HTTP/JSON details.
/// </summary>
public sealed class InmaOptOutResult
{
    /// <summary>True when INMA returned application-level success (Status:true).</summary>
    public required bool Success { get; init; }

    /// <summary>
    /// INMA raw StatusCode from response body: "0" (success), "909" (idempotent success —
    /// already opted out), "908" (contact not found), "906"/"907" (only relevant on
    /// chatoperation, not on /optout — included for completeness), or internal sentinels
    /// "SKIPPED-NOOP" / "PARSE-FAIL" / "NETWORK".
    /// </summary>
    public required string StatusCode { get; init; }

    /// <summary>INMA Message field (or internal error detail).</summary>
    public string? Message { get; init; }

    /// <summary>HTTP status code (0 when no response received — NoOp / network fail).</summary>
    public int HttpStatusCode { get; init; }

    /// <summary>INMA AlreadyOptedOut flag (true when idempotent replay).</summary>
    public bool AlreadyOptedOut { get; init; }

    /// <summary>Number of chat rows updated on the INMA side.</summary>
    public int AffectedChatCount { get; init; }

    /// <summary>Convenience check: success OR idempotent replay (909).</summary>
    public bool IsSuccessOrAlreadyOut => Success || StatusCode == "909";
}
