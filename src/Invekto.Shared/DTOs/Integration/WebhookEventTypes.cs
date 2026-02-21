namespace Invekto.Shared.DTOs.Integration;

/// <summary>
/// INMA webhook message types.
/// type field in WapCRMBasicMessageModel.
/// </summary>
public static class WebhookEventTypes
{
    public const string Chat = "chat";
    public const string Image = "image";
    public const string Video = "video";
    public const string Audio = "audio";
    public const string Document = "document";
    public const string Location = "location";
    public const string Sticker = "sticker";

    private static readonly HashSet<string> TextTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        Chat
    };

    /// <summary>Returns true if the message type contains processable text.</summary>
    public static bool IsTextMessage(string? type)
        => !string.IsNullOrWhiteSpace(type) && TextTypes.Contains(type);
}
