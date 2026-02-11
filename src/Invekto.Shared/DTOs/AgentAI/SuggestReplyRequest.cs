using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.AgentAI;

public sealed class SuggestReplyRequest
{
    [JsonPropertyName("chat_id")]
    public int ChatId { get; set; }

    [JsonPropertyName("message_text")]
    public string? MessageText { get; set; }

    [JsonPropertyName("customer_name")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    [JsonPropertyName("conversation_history")]
    public List<ConversationMessage>? ConversationHistory { get; set; }

    [JsonPropertyName("templates")]
    public List<ReplyTemplate>? Templates { get; set; }

    [JsonPropertyName("template_variables")]
    public Dictionary<string, string>? TemplateVariables { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = "tr";
}

public sealed class ConversationMessage
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}

public sealed class ReplyTemplate
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
