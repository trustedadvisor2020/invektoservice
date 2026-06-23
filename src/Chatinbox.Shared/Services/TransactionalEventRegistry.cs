namespace Chatinbox.Shared.Services;

/// <summary>
/// FEAT-J2: Central, exact-literal allow-list of event names that bypass the
/// marketing opt-out check on both sides of the integration:
///
/// - INSE: TriggerProcessor / ConsentManager skip the opt-out gate when the
///   event name is in this set (audit INV-OB-030).
/// - INMA: the bridge forwards MessageCategory="transactional" to
///   /api/chatoperation, which causes INMA's server-side opt-out check
///   to skip (wapcrm-marketing-api.md section 4).
///
/// Exact match is intentional — prefix globs let a tenant author
/// "offer_sent_spam" and slip past the gate. Extending this list requires
/// a code change, Codex review, and deploy; there is deliberately no
/// tenant-level / runtime override.
/// </summary>
public static class TransactionalEventRegistry
{
    private static readonly HashSet<string> Events = new(StringComparer.Ordinal)
    {
        "appointment_confirmed_tr", "appointment_confirmed_en",
        "appointment_reminder_tr",  "appointment_reminder_en",
        "meeting_link_sent_tr",     "meeting_link_sent_en",
        "offer_sent_consult_tr",    "offer_sent_consult_en",
        "payment_receipt_sent",
    };

    /// <summary>True when <paramref name="eventName"/> is a transactional allow-list entry.</summary>
    public static bool IsTransactional(string? eventName)
        => !string.IsNullOrEmpty(eventName) && Events.Contains(eventName);
}
