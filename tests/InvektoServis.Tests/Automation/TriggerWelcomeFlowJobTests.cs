using FluentAssertions;
using Invekto.Automation.Services;
using Invekto.Automation.Services.Jobs;
using Xunit;

namespace InvektoServis.Tests.Automation;

/// <summary>
/// Paket B-META AC2: welcome-sent lifecycle dispatch invariant tests.
/// Pins the semantic that <see cref="TriggerWelcomeFlowJob.ShouldDispatchWelcomeSent"/>
/// returns true iff the welcome flow emitted ≥1 outbound message (Q's
/// "mesaj atildi" literal over IsTerminal / ErrorCode-based alternatives).
///
/// Why a helper-level test instead of a full mock graph: <c>AutomationRepository</c>
/// and <c>FlowEngineV2</c> are both <c>sealed</c> (NSubstitute can't mock them
/// without a refactor that splits interfaces). Extracting the decision into a
/// pure static helper gives us deterministic coverage of the critical
/// invariant without pulling the rest of the flow engine into test scope.
///
/// Full end-to-end path (welcome flow → messages emitted → lifecycle hop fired
/// → ZohoLifecycleDispatcher → CRM transition) is covered by Pilot Stage 1
/// smoke AC1b/AC2b, which exercises the live integration once the tenant's
/// Meta / HSM / Blueprint config comes online.
/// </summary>
public class TriggerWelcomeFlowJobTests
{
    [Fact]
    public void ShouldDispatchWelcomeSent_SuccessScenario_MessagesEmitted_ReturnsTrue()
    {
        // AC2(a) — messages.Count=2 + IsTerminal=true + NeedsHandoff=false.
        var result = new EngineStepResult
        {
            Messages = new List<string> { "Hello!", "Welcome to Dent Adavista." },
            State = new SessionStateV2 { Status = "completed" },
            IsTerminal = true,
            NeedsHandoff = false
        };

        TriggerWelcomeFlowJob.ShouldDispatchWelcomeSent(result).Should().BeTrue(
            "the welcome flow actually emitted outbound messages so the lifecycle hop must fire");
    }

    [Fact]
    public void ShouldDispatchWelcomeSent_ErrorScenario_NoMessages_ReturnsFalse()
    {
        // AC2(b) — messages.Count=0 + IsTerminal=true + ErrorCode set.
        var result = new EngineStepResult
        {
            Messages = new List<string>(),
            State = new SessionStateV2 { Status = "error" },
            IsTerminal = true,
            NeedsHandoff = false,
            ErrorCode = "INV-AT-071"
        };

        TriggerWelcomeFlowJob.ShouldDispatchWelcomeSent(result).Should().BeFalse(
            "no outbound message reached the user, so dirtying CRM state with a welcome_sent transition would be a false positive");
    }

    [Fact]
    public void ShouldDispatchWelcomeSent_NullResult_ReturnsFalse()
    {
        // Defensive null-guard — serialization gap or caller bug must not crash
        // the welcome job's cleanup path. (Comment on the helper captures the
        // same rationale; test keeps the contract visible to reviewers.)
        TriggerWelcomeFlowJob.ShouldDispatchWelcomeSent(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void ShouldDispatchWelcomeSent_AnyPositiveCount_ReturnsTrue(int messageCount)
    {
        // Pins the boundary: count > 0 dispatches regardless of terminal / handoff /
        // error state. Matches Q's G2 decision — "literal mesaj atildi" semantic.
        var result = new EngineStepResult
        {
            Messages = Enumerable.Range(0, messageCount).Select(i => $"msg-{i}").ToList(),
            State = new SessionStateV2 { Status = "active" },
            IsTerminal = false
        };

        TriggerWelcomeFlowJob.ShouldDispatchWelcomeSent(result).Should().BeTrue();
    }
}
