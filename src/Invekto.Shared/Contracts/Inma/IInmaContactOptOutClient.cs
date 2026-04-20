using Invekto.Shared.Contracts.Inma.Dtos;

namespace Invekto.Shared.Contracts.Inma;

/// <summary>
/// FEAT-J2: Client abstraction for pushing INSE opt-out state to INMA contact flags
/// via POST https://cxapi.wapcrm.net/api/optout and /api/optin.
///
/// Implementations:
///   - NoOpInmaContactOptOutClient: emergency kill-switch fallback. Returns
///     StatusCode='SKIPPED-NOOP' so the outbox job marks rows as 'skipped_noop'
///     (queued, can be drained via /api/ops/outbox/retry-skipped).
///   - HttpInmaContactOptOutClient: production path. Issues authenticated POST
///     requests to INMA, parses response body, normalizes to InmaOptOutResult.
///
/// Wire contract reference: wapcrm-marketing-api.md sections 5.1 and 5.2.
/// </summary>
public interface IInmaContactOptOutClient
{
    /// <summary>POST /api/optout — registers marketing opt-out on INMA side.</summary>
    Task<InmaOptOutResult> PushOptOutAsync(InmaOptOutRequest request, CancellationToken ct = default);

    /// <summary>POST /api/optin — revokes prior opt-out on INMA side.</summary>
    Task<InmaOptOutResult> PushOptInAsync(InmaOptOutRequest request, CancellationToken ct = default);
}
