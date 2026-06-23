using FluentAssertions;
using Chatinbox.Shared.Contracts.Inma.Webhooks;

namespace Chatinbox.Tests.Shared.Inma;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C3a: the security-relevant piece of the flow-trigger dispatch is the
/// fail-closed loop guard. Unit-covered here so the C2→C3→C4→C2 loop protection is verified without a
/// Backend integration host. Rule: fire ONLY on an explicit human 'user' actor; suppress everything else
/// (api / zoho-webhook / system / null / unknown) and ALWAYS suppress an 'invekto-' originRequestId echo.
/// </summary>
public class CustomerStatusFlowSuppressionTests
{
    [Theory]
    [InlineData("user")]
    [InlineData("User")]      // case-insensitive
    [InlineData(" user ")]    // trimmed
    [InlineData("USER")]
    public void Evaluate_HumanUserActor_NoOriginEcho_Fires(string actorType)
    {
        CustomerStatusFlowSuppression.Evaluate(actorType, originRequestId: null)
            .Should().Be(FlowSuppressionDecision.Fire);
    }

    [Theory]
    [InlineData("api")]
    [InlineData("zoho-webhook")]
    [InlineData("system")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("partner")]   // any non-user actor
    [InlineData(null)]        // fail closed on missing actor
    public void Evaluate_NonUserActor_NoOriginEcho_SuppressesAsActor(string? actorType)
    {
        CustomerStatusFlowSuppression.Evaluate(actorType, originRequestId: null)
            .Should().Be(FlowSuppressionDecision.SuppressActor);
    }

    [Theory]
    [InlineData("user")]      // loop kill beats the human-user fire path
    [InlineData("api")]
    [InlineData("system")]
    [InlineData(null)]
    public void Evaluate_InvektoOriginEcho_AlwaysSuppressesAsLoop(string? actorType)
    {
        CustomerStatusFlowSuppression.Evaluate(actorType, originRequestId: "invekto-flowrun-7c1d2e3f")
            .Should().Be(FlowSuppressionDecision.SuppressLoop);
    }

    [Fact]
    public void Evaluate_NonInvektoOriginEcho_DoesNotTriggerLoopSuppress()
    {
        // A third-party API client's request id must NOT be treated as our loop echo; it falls through
        // to the actor rule (api => SuppressActor here, not SuppressLoop).
        CustomerStatusFlowSuppression.Evaluate("api", originRequestId: "partner-xyz-123")
            .Should().Be(FlowSuppressionDecision.SuppressActor);
    }

    [Fact]
    public void Evaluate_NonInvektoOrigin_UserActor_StillFires()
    {
        CustomerStatusFlowSuppression.Evaluate("user", originRequestId: "partner-xyz-123")
            .Should().Be(FlowSuppressionDecision.Fire);
    }

    [Fact]
    public void OriginRequestIdPrefix_IsTheSharedInvektoConstant()
    {
        // C4 sends ClientRequestID = "invekto-{flowRunUuid}" using this same constant — keep it pinned.
        CustomerStatusFlowSuppression.OriginRequestIdPrefix.Should().Be("invekto-");
    }
}
