using Chatinbox.Shared.Contracts.Inma.Dtos;

namespace Chatinbox.Shared.Contracts.Inma;

/// <summary>
/// FEAT-J2: Kill-switch fallback client. Returns a canned "skipped" result so
/// InmaOptOutSyncJob parks the outbox row as 'skipped_noop' rather than
/// retrying endlessly. Drained later via /api/ops/outbox/retry-skipped once
/// Mode is flipped back to 'Http'.
/// </summary>
public sealed class NoOpInmaContactOptOutClient : IInmaContactOptOutClient
{
    public Task<InmaOptOutResult> PushOptOutAsync(InmaOptOutRequest request, CancellationToken ct = default)
        => Task.FromResult(CreateSkippedResult());

    public Task<InmaOptOutResult> PushOptInAsync(InmaOptOutRequest request, CancellationToken ct = default)
        => Task.FromResult(CreateSkippedResult());

    private static InmaOptOutResult CreateSkippedResult() => new()
    {
        Success = false,
        StatusCode = "SKIPPED-NOOP",
        Message = "NoOp mode active — push deferred",
        HttpStatusCode = 0,
        AlreadyOptedOut = false,
        AffectedChatCount = 0,
    };
}
