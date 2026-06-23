using FluentAssertions;
using Chatinbox.Outbound.Services;
using Chatinbox.Shared.Contracts.Inma.Dtos;
using Chatinbox.Tests._Shared.Infrastructure;

namespace Chatinbox.Tests.Outbound;

/// <summary>
/// FEAT-PROJELER / cxapi send engine — PR-3a. Pure, deterministic tests for the idempotency
/// doctrine: <see cref="CxapiOutcomeMapper"/> (the single source of the outcome -> state decision)
/// and the (tenant, instance) <see cref="RateLimiter"/> + cooldown. No DB, no HTTP, no real sleeps
/// (the limiter clock is injected).
/// </summary>
public sealed class CxapiSendStateMachineTests
{
    private const int MaxSendAttempts = 5;

    // ── CxapiOutcomeMapper: only RateLimited (provably not sent) is ever retried ──────────

    [Fact]
    public void Map_Submitted_IsSentTerminal()
    {
        var d = CxapiOutcomeMapper.Map(WapCrmSendOutcome.Submitted, attemptCount: 1, MaxSendAttempts);
        d.Status.Should().Be("sent");
        d.CounterColumn.Should().Be("sent");
        d.Requeue.Should().BeFalse();
        d.ApplyCooldown.Should().BeFalse();
    }

    [Fact]
    public void Map_ProviderFailed_IsFailedTerminal()
    {
        var d = CxapiOutcomeMapper.Map(WapCrmSendOutcome.ProviderFailed, attemptCount: 1, MaxSendAttempts);
        d.Status.Should().Be("failed");
        d.CounterColumn.Should().Be("failed");
        d.Requeue.Should().BeFalse();
        d.ApplyCooldown.Should().BeFalse();
    }

    [Fact]
    public void Map_RateLimited_UnderCap_RequeuesWithCooldown()
    {
        var d = CxapiOutcomeMapper.Map(WapCrmSendOutcome.RateLimited, attemptCount: 1, MaxSendAttempts);
        d.Status.Should().Be("queued");
        d.CounterColumn.Should().BeNull("a requeue must not touch any terminal counter");
        d.Requeue.Should().BeTrue();
        d.ApplyCooldown.Should().BeTrue();
    }

    [Fact]
    public void Map_RateLimited_AtCap_BecomesFailed()
    {
        var d = CxapiOutcomeMapper.Map(WapCrmSendOutcome.RateLimited, attemptCount: MaxSendAttempts, MaxSendAttempts);
        d.Status.Should().Be("failed");
        d.CounterColumn.Should().Be("failed");
        d.Requeue.Should().BeFalse();
        d.ApplyCooldown.Should().BeFalse();
    }

    [Fact]
    public void Map_Ambiguous_IsAmbiguousTerminal_NeverRetried()
    {
        var d = CxapiOutcomeMapper.Map(WapCrmSendOutcome.Ambiguous, attemptCount: 1, MaxSendAttempts);
        d.Status.Should().Be("ambiguous");
        d.CounterColumn.Should().Be("ambiguous");
        d.Requeue.Should().BeFalse("a timeout may have reached the server — never auto-retry");
        d.ApplyCooldown.Should().BeFalse();
    }

    [Fact]
    public void Map_TransportError_IsAmbiguousTerminal_NeverRetried()
    {
        var d = CxapiOutcomeMapper.Map(WapCrmSendOutcome.TransportError, attemptCount: 1, MaxSendAttempts);
        d.Status.Should().Be("ambiguous");
        d.CounterColumn.Should().Be("ambiguous");
        d.Requeue.Should().BeFalse("transport failure is not assumed retry-safe (can fail in the response phase)");
    }

    // ── RateLimiter: (tenant, instance) keying + cooldown, bridge untouched ───────────────

    private static RateLimiter NewLimiter(int perMinute, Func<DateTime> clock) =>
        new(perMinute, FakeLoggerFactory.Create("CxapiRateLimiter.Test"), clock);

    [Fact]
    public void TryAcquire_PerInstance_WindowsAreIndependent()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var limiter = NewLimiter(perMinute: 1, () => now);

        limiter.TryAcquire(tenantId: 7, instanceId: 100).Should().BeTrue();
        limiter.TryAcquire(tenantId: 7, instanceId: 100).Should().BeFalse("the (7,100) minute window is full");
        limiter.TryAcquire(tenantId: 7, instanceId: 200).Should().BeTrue("(7,200) is a separate window");
    }

    [Fact]
    public void BridgeTryAcquire_MapsToInstanceZero_AndIsUnaffectedByInstanceCooldown()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var limiter = NewLimiter(perMinute: 100, () => now);

        // A cooldown on a real cxapi instance must NOT block the bridge (instance 0) window.
        limiter.ApplyCooldown(tenantId: 7, instanceId: 100, TimeSpan.FromMinutes(10));
        limiter.TryAcquire(tenantId: 7).Should().BeTrue("the bridge uses (tenant, instance=0), not the cooled-down instance");
        limiter.TryAcquire(tenantId: 7, instanceId: 0).Should().BeTrue("TryAcquire(tenant) == TryAcquire(tenant, 0)");
        limiter.TryAcquire(tenantId: 7, instanceId: 100).Should().BeFalse("the cooled-down instance is blocked");
    }

    [Fact]
    public void ApplyCooldown_BlocksUntilElapsed_ThenReleases()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var limiter = NewLimiter(perMinute: 100, () => now);

        limiter.ApplyCooldown(tenantId: 7, instanceId: 100, TimeSpan.FromSeconds(60));
        limiter.TryAcquire(tenantId: 7, instanceId: 100).Should().BeFalse("inside the cooldown");

        now = now.AddSeconds(61);
        limiter.TryAcquire(tenantId: 7, instanceId: 100).Should().BeTrue("the cooldown has elapsed");
    }

    [Fact]
    public void ApplyCooldown_NonPositive_IsNoOp()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var limiter = NewLimiter(perMinute: 100, () => now);

        limiter.ApplyCooldown(tenantId: 7, instanceId: 100, TimeSpan.Zero);
        limiter.TryAcquire(tenantId: 7, instanceId: 100).Should().BeTrue("a zero cooldown must not block");
    }
}
