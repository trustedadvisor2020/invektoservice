namespace Invekto.Shared.DTOs.Integration;

/// <summary>
/// Webhook event types that Main App sends to InvektoServis.
/// GR-1.9: Defines the contract for Main App -> InvektoServis communication.
/// </summary>
public static class WebhookEventTypes
{
    /// <summary>New message received from customer</summary>
    public const string NewMessage = "new_message";

    /// <summary>Conversation closed by agent or system</summary>
    public const string ConversationClosed = "conversation_closed";

    /// <summary>Tag/label changed on a conversation</summary>
    public const string TagChanged = "tag_changed";

    /// <summary>New conversation started</summary>
    public const string ConversationStarted = "conversation_started";

    /// <summary>Agent assigned to conversation</summary>
    public const string AgentAssigned = "agent_assigned";

    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        NewMessage,
        ConversationClosed,
        TagChanged,
        ConversationStarted,
        AgentAssigned
    };

    public static bool IsValid(string? eventType)
        => !string.IsNullOrWhiteSpace(eventType) && ValidTypes.Contains(eventType);
}
