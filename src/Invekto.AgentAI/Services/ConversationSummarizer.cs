using System.Text;
using System.Text.Json;
using Invekto.Shared.DTOs.AgentAI;
using Invekto.Shared.Logging;

namespace Invekto.AgentAI.Services;

/// <summary>
/// GR-2.2: Summarizes long conversation histories to reduce token usage.
/// When history exceeds threshold, older messages are compressed into a summary
/// while recent messages are kept verbatim.
/// </summary>
public sealed class ConversationSummarizer
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly JsonLinesLogger _logger;

    private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
    private const int SummaryMaxTokens = 256;
    private const int SummaryTimeoutMs = 8000;

    /// <summary>
    /// Messages above this count trigger summarization.
    /// Recent messages (below threshold) are kept verbatim.
    /// </summary>
    public int SummaryThreshold { get; }

    /// <summary>
    /// Number of recent messages to keep verbatim (not summarized).
    /// </summary>
    public int RecentMessageCount { get; }

    public ConversationSummarizer(
        HttpClient httpClient, string apiKey, string model,
        int summaryThreshold, int recentMessageCount,
        JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        SummaryThreshold = summaryThreshold;
        RecentMessageCount = recentMessageCount;
        _logger = logger;
    }

    /// <summary>
    /// If conversation history is long enough, summarize older messages.
    /// Returns (summary, recentMessages) tuple.
    /// If summarization fails or history is short, returns (null, allMessages).
    /// </summary>
    public async Task<(string? Summary, List<ConversationMessage> RecentMessages)> SummarizeIfNeededAsync(
        List<ConversationMessage> history, CancellationToken ct = default)
    {
        if (history.Count <= SummaryThreshold)
            return (null, history);

        var olderMessages = history.Take(history.Count - RecentMessageCount).ToList();
        var recentMessages = history.TakeLast(RecentMessageCount).ToList();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(SummaryTimeoutMs);

            var conversationText = new StringBuilder();
            foreach (var msg in olderMessages)
            {
                var role = msg.Source == "CUSTOMER" ? "Musteri" : "Agent";
                conversationText.AppendLine($"{role}: {msg.Text}");
            }

            var requestBody = new
            {
                model = _model,
                max_tokens = SummaryMaxTokens,
                system = "Verilen sohbet gecmisini 2-3 cumle ile ozetle. Anahtar bilgileri (isimler, urunler, fiyatlar, talepler) koru. Sadece ozeti yaz, baska bir sey yazma.",
                messages = new[]
                {
                    new { role = "user", content = conversationText.ToString() }
                }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ClaudeApiUrl);
            // TryAddWithoutValidation: config-sourced api key may carry a stray char; Add() would
            // throw FormatException, which escapes the typed-catch graceful boundary to the caller.
            httpRequest.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
            httpRequest.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemWarn($"[ConversationSummarizer] Claude API HTTP {(int)response.StatusCode}");
                return (null, history);
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonDocument>(cts.Token);

            // Defensive navigation: missing/mis-shaped fields degrade to raw history instead
            // of throwing KeyNotFound/InvalidOperation/IndexOutOfRange (keeps the catch typed).
            if (responseJson is null
                || responseJson.RootElement.ValueKind != JsonValueKind.Object
                || !responseJson.RootElement.TryGetProperty("content", out var contentArray)
                || contentArray.ValueKind != JsonValueKind.Array
                || contentArray.GetArrayLength() == 0)
            {
                _logger.SystemWarn("[ConversationSummarizer] Claude response missing content array");
                return (null, history);
            }

            var firstBlock = contentArray[0];
            var summary = firstBlock.ValueKind == JsonValueKind.Object
                          && firstBlock.TryGetProperty("text", out var textProp)
                          && textProp.ValueKind == JsonValueKind.String
                ? textProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(summary))
            {
                _logger.SystemWarn("[ConversationSummarizer] Empty or malformed summary returned from Claude");
                return (null, history);
            }

            _logger.SystemInfo($"[ConversationSummarizer] Summarized {olderMessages.Count} messages into {summary.Length} chars");
            return (summary.Trim(), recentMessages);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // App shutting down
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn("[ConversationSummarizer] Summary generation timeout, using raw history");
            return (null, history);
        }
        catch (HttpRequestException ex)
        {
            return LogSummaryFailureAsRaw(ex, history);
        }
        catch (JsonException ex)
        {
            return LogSummaryFailureAsRaw(ex, history);
        }
        catch (NotSupportedException ex)
        {
            return LogSummaryFailureAsRaw(ex, history);
        }
    }

    // Typed transport/serialization failures degrade to raw history (graceful). Separate
    // catch blocks (not `catch (Exception) when (...)`) because Codex treats a when-filtered
    // Exception base as broad in this project (hot-lessons L36). Genuine-unexpected
    // exceptions propagate to the caller's degradation boundary (Program.cs /suggest).
    private (string?, List<ConversationMessage>) LogSummaryFailureAsRaw(
        Exception ex, List<ConversationMessage> history)
    {
        _logger.SystemWarn($"[ConversationSummarizer] Summary failed: {ex.Message}, using raw history");
        return (null, history);
    }
}
