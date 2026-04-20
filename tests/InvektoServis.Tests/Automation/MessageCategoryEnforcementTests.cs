using FluentAssertions;
using Invekto.Shared.Services;
using Xunit;

namespace InvektoServis.Tests.Automation;

/// <summary>
/// FEAT-J2 AC10 Scenario 10 (enforce_message_category) + AC6 transactional
/// allow-list — unit tests against the pure registry used by
/// AutomationOrchestrator.SendCallbackAsync to derive MessageCategory.
///
/// Orchestrator-level end-to-end (enforce=true + null event ⇒ callback rejected)
/// is covered by the enforcement branch in SendCallbackAsync returning false;
/// these tests pin the registry's contract independently so the broader surface
/// is exercised at the boundary it actually consumes.
/// </summary>
public class MessageCategoryEnforcementTests
{
    [Fact]
    public void TransactionalAllowList_ExactWhitelistedEvent_ReturnsTrue()
    {
        TransactionalEventRegistry.IsTransactional("appointment_confirmed_en").Should().BeTrue();
        TransactionalEventRegistry.IsTransactional("appointment_reminder_tr").Should().BeTrue();
        TransactionalEventRegistry.IsTransactional("meeting_link_sent_en").Should().BeTrue();
        TransactionalEventRegistry.IsTransactional("offer_sent_consult_tr").Should().BeTrue();
        TransactionalEventRegistry.IsTransactional("payment_receipt_sent").Should().BeTrue();
    }

    [Theory]
    [InlineData("offer_sent_spam")]                // Q7 spoof attempt
    [InlineData("appointment_confirmed_spam")]     // suffix append
    [InlineData("offer_sent_consult_fr")]          // unlisted locale
    [InlineData("appointmentconfirmed_en")]        // underscore missing
    [InlineData("broadcast_promo")]                // obvious marketing
    [InlineData("")]                               // empty
    public void TransactionalAllowList_UnlistedEvent_ReturnsFalse(string eventName)
    {
        TransactionalEventRegistry.IsTransactional(eventName).Should().BeFalse();
    }

    [Fact]
    public void TransactionalAllowList_NullEvent_ReturnsFalse()
    {
        TransactionalEventRegistry.IsTransactional(null).Should().BeFalse();
    }

    [Fact]
    public void TransactionalAllowList_CaseSensitive()
    {
        // Ordinal comparison — APPOINTMENT_CONFIRMED_EN must NOT match
        // appointment_confirmed_en. This is intentional: tenant-authored
        // templates that differ only in case should be treated as distinct.
        TransactionalEventRegistry.IsTransactional("APPOINTMENT_CONFIRMED_EN").Should().BeFalse();
        TransactionalEventRegistry.IsTransactional("Appointment_Confirmed_En").Should().BeFalse();
    }

    /// <summary>
    /// Enforcement-category derivation mirror (Q3 decision):
    /// - event in allow-list → "transactional"
    /// - event NOT in allow-list → "marketing"
    /// </summary>
    [Theory]
    [InlineData("appointment_confirmed_en", "transactional")]
    [InlineData("payment_receipt_sent", "transactional")]
    [InlineData("broadcast_promo", "marketing")]
    [InlineData("weekly_offer", "marketing")]
    public void EnforcementCategoryDerivation_EventProvided_MapsCorrectly(string eventName, string expected)
    {
        var actual = TransactionalEventRegistry.IsTransactional(eventName) ? "transactional" : "marketing";
        actual.Should().Be(expected);
    }
}
