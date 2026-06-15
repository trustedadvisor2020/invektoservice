using FluentAssertions;
using Invekto.Automation.Services.NodeHandlers;
using Invekto.Shared.Contracts.Inma.Webhooks;
using Xunit;

namespace InvektoServis.Tests.Automation;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C4: pins the two pure pieces of the 'Set Customer Status' action handler —
/// (1) FULL-LIST feature-id parsing (empty = clear; dedup; drop garbage) and (2) the STABLE, GUID-free
/// ClientRequestID (deterministic per (tenant, group, node, target); carries the shared 'invekto-' loop
/// prefix; clamped to the vendor's 128-char limit).
///
/// Why helper-level: <c>BackendCustomerStatusClient</c> (the HTTP hop) and <c>FlowEngineV2</c> are sealed
/// and the handler's real branches (simulation short-circuit, identity guard, success/error routing) need
/// an ExecutionContext + logger host. The pure statics carry the Codex-relevant invariants (idempotent id,
/// full-list semantics) and are unit-coverable without that host — mirrors
/// <see cref="Invekto.Automation.Services.Jobs.TriggerWelcomeFlowJob.ShouldDispatchWelcomeSent"/>. The
/// end-to-end write path is exercised by deploy-time smoke (5050).
/// </summary>
public class ActionSetCustomerStatusHandlerTests
{
    // --- ParseFeatureIds: FULL-LIST semantics ---

    [Fact]
    public void ParseFeatureIds_Empty_ReturnsEmpty_ClearsGroup()
    {
        ActionSetCustomerStatusHandler.ParseFeatureIds("").Should().BeEmpty(
            "an empty selection is the intentional 'clear the group' case");
        ActionSetCustomerStatusHandler.ParseFeatureIds("   ").Should().BeEmpty();
    }

    [Fact]
    public void ParseFeatureIds_Single_ParsesOne()
    {
        ActionSetCustomerStatusHandler.ParseFeatureIds("19").Should().Equal(19);
    }

    [Fact]
    public void ParseFeatureIds_MultiWithSpaces_ParsesAllInOrder()
    {
        ActionSetCustomerStatusHandler.ParseFeatureIds("30, 31 ,32").Should().Equal(30, 31, 32);
    }

    [Fact]
    public void ParseFeatureIds_DropsDuplicatesAndGarbage()
    {
        // 19 repeated -> deduped; "abc"/"0"/"-5"/"" -> dropped (non-positive / non-numeric / empty).
        ActionSetCustomerStatusHandler.ParseFeatureIds("19,abc,19,0,-5,,20").Should().Equal(19, 20);
    }

    // --- BuildClientRequestId: stable, prefixed, clamped ---

    [Fact]
    public void BuildClientRequestId_PrefersCustomerId_AndIsDeterministic()
    {
        var a = ActionSetCustomerStatusHandler.BuildClientRequestId(5050, 1, "node-7", 134162, "905551112233");
        var b = ActionSetCustomerStatusHandler.BuildClientRequestId(5050, 1, "node-7", 134162, "905551112233");

        a.Should().Be(b, "the same (tenant, group, node, target) must yield one id so a retry is not a fresh write");
        a.Should().StartWith(CustomerStatusFlowSuppression.OriginRequestIdPrefix,
            "the loop-suppress prefix must match the C3a guard so the echo is recognized");
        a.Should().Contain("c134162", "customerId is the preferred identity token");
        a.Should().NotContain("905551112233", "phone is not used when a customerId is present");
    }

    [Fact]
    public void BuildClientRequestId_FallsBackToPhone_WhenNoCustomerId()
    {
        var id = ActionSetCustomerStatusHandler.BuildClientRequestId(5050, 1, "node-7", null, "905551112233");
        id.Should().StartWith(CustomerStatusFlowSuppression.OriginRequestIdPrefix);
        id.Should().Contain("p905551112233", "phone is the fallback identity token");
    }

    [Fact]
    public void BuildClientRequestId_DiffersByTargetAndGroupAndNode()
    {
        var baseId = ActionSetCustomerStatusHandler.BuildClientRequestId(5050, 1, "node-7", 100, null);
        ActionSetCustomerStatusHandler.BuildClientRequestId(5050, 1, "node-7", 200, null).Should().NotBe(baseId);
        ActionSetCustomerStatusHandler.BuildClientRequestId(5050, 2, "node-7", 100, null).Should().NotBe(baseId);
        ActionSetCustomerStatusHandler.BuildClientRequestId(5050, 1, "node-8", 100, null).Should().NotBe(baseId);
        ActionSetCustomerStatusHandler.BuildClientRequestId(9999, 1, "node-7", 100, null).Should().NotBe(baseId);
    }

    [Fact]
    public void BuildClientRequestId_ClampedToVendorMaxLength()
    {
        var longNode = new string('n', 500);
        var id = ActionSetCustomerStatusHandler.BuildClientRequestId(5050, 1, longNode, 100, null);
        id.Length.Should().BeLessThanOrEqualTo(128, "the vendor ClientRequestID field caps at 128 chars");
        id.Should().StartWith(CustomerStatusFlowSuppression.OriginRequestIdPrefix);
    }
}
