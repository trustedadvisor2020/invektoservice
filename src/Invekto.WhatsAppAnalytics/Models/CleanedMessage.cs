namespace Invekto.WhatsAppAnalytics.Models;

/// <summary>
/// A single cleaned WhatsApp message ready for DB insert.
/// Output of Stage 1 (Cleaner).
/// </summary>
public sealed class CleanedMessage
{
    public string ConversationId { get; set; } = "";
    public string BusinessPhone { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string MessageText { get; set; } = "";
    public string SenderType { get; set; } = ""; // ME or CUSTOMER
    public string AgentName { get; set; } = "";
    public string MessageHash { get; set; } = ""; // SHA256[:16] for dedup
}
