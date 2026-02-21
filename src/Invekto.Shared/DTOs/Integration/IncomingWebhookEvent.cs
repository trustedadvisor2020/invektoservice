using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Integration;

/// <summary>
/// Webhook payload that INMA (Main App) sends to InvektoServis.
/// Format matches WapCRMBasicMessageWebHookModel from INMA.
/// tenant_id comes from JWT or IP whitelist (?companyId=), NOT from payload.
/// </summary>
public sealed class IncomingWebhookEvent
{
    [JsonPropertyName("messages")]
    public List<WebhookMessage>? Messages { get; init; }

    [JsonPropertyName("InstanceID")]
    public string? InstanceId { get; init; }
}

/// <summary>
/// Single message within the webhook payload.
/// Maps to WapCRMBasicMessageModel from INMA.
/// </summary>
public sealed class WebhookMessage
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("body")]
    public string? Body { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("senderName")]
    public string? SenderName { get; init; }

    [JsonPropertyName("fromMe")]
    public bool FromMe { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("chatId")]
    public string? ChatId { get; init; }

    [JsonPropertyName("caption")]
    public string? Caption { get; init; }

    [JsonPropertyName("isforwarded")]
    public bool IsForwarded { get; init; }

    [JsonPropertyName("hasQuotedMsg")]
    public bool HasQuotedMsg { get; init; }

    [JsonPropertyName("QuotedMsgID")]
    public string? QuotedMsgId { get; init; }

    [JsonPropertyName("IsGroupMessage")]
    public bool IsGroupMessage { get; init; }

    [JsonPropertyName("productLink")]
    public string? ProductLink { get; init; }

    [JsonPropertyName("productContent")]
    public string? ProductContent { get; init; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; init; }

    [JsonPropertyName("fromContact")]
    public string? FromContact { get; init; }
}
