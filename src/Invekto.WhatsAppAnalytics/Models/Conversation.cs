namespace Invekto.WhatsAppAnalytics.Models;

/// <summary>
/// A threaded conversation with outcome detection.
/// Output of Stage 2 (Threader).
/// </summary>
public sealed class Conversation
{
    public string ConversationId { get; set; } = "";
    public string BusinessPhone { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public int MessageCount { get; set; }
    public int CustomerMessageCount { get; set; }
    public int AgentMessageCount { get; set; }
    public string PrimaryAgent { get; set; } = "";
    public double FirstResponseMinutes { get; set; }
    public string Outcome { get; set; } = "no_sale"; // sale, offered, no_sale, abandoned, no_response, return, complaint
    public string ProductCodes { get; set; } = ""; // pipe-separated
    public string FirstCustomerMsg { get; set; } = "";
    public string LastAgentMsg { get; set; } = "";
}
