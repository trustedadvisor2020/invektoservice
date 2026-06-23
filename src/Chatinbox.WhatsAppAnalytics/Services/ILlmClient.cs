namespace Chatinbox.WhatsAppAnalytics.Services;

/// <summary>
/// Polymorphic LLM client interface for benchmark multi-model comparison.
/// Implemented by ClaudeClient and GeminiClient.
/// </summary>
public interface ILlmClient
{
    string ModelName { get; }
    bool IsAvailable { get; }
    Task<string?> ClassifyAsync(string systemPrompt, string userContent, CancellationToken ct);
}
