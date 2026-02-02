using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.ChatAnalysis;

namespace Invekto.ChatAnalysis.Services;

/// <summary>
/// Claude API analyzer for sentiment and categorization
/// Uses Claude Haiku for fast, cheap analysis
/// </summary>
public sealed class ClaudeAnalyzer : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-3-haiku-20240307";
    private const int MaxTokens = 256;
    private const int TimeoutMs = 5000; // 5 second timeout for Claude

    public ClaudeAnalyzer(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey));

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(TimeoutMs)
        };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Analyze chat messages for sentiment and category
    /// </summary>
    public async Task<ClaudeResult<AnalysisResult>> AnalyzeAsync(List<WapCrmMessage> messages)
    {
        try
        {
            var conversationText = FormatConversation(messages);
            var prompt = BuildPrompt(conversationText);

            var request = new ClaudeRequest
            {
                Model = Model,
                MaxTokens = MaxTokens,
                Messages =
                [
                    new ClaudeMessage { Role = "user", Content = prompt }
                ]
            };

            var response = await _httpClient.PostAsJsonAsync(ApiUrl, request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return ClaudeResult<AnalysisResult>.Fail(
                    ErrorCodes.ChatAnalysisClaudeError,
                    $"Claude returned {(int)response.StatusCode}: {errorBody}");
            }

            var claudeResponse = await response.Content.ReadFromJsonAsync<ClaudeResponse>(_jsonOptions);

            if (claudeResponse?.Content == null || claudeResponse.Content.Count == 0)
            {
                return ClaudeResult<AnalysisResult>.Fail(
                    ErrorCodes.ChatAnalysisClaudeError,
                    "Claude returned empty response");
            }

            var analysisText = claudeResponse.Content[0].Text ?? "";
            var (analysis, parseFailed, parseError) = ParseAnalysis(analysisText);

            if (parseFailed)
            {
                // Log parse failure but still return result with low confidence
                // This is NOT silent - confidence=0 and summary indicates parse error
                return ClaudeResult<AnalysisResult>.SuccessWithWarning(
                    analysis,
                    $"Parse warning: {parseError}");
            }

            return ClaudeResult<AnalysisResult>.Success(analysis);
        }
        catch (TaskCanceledException)
        {
            return ClaudeResult<AnalysisResult>.Fail(
                ErrorCodes.ChatAnalysisClaudeTimeout,
                $"Claude timeout after {TimeoutMs}ms");
        }
        catch (HttpRequestException ex)
        {
            return ClaudeResult<AnalysisResult>.Fail(
                ErrorCodes.ChatAnalysisClaudeError,
                $"Claude connection error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return ClaudeResult<AnalysisResult>.Fail(
                ErrorCodes.ChatAnalysisClaudeError,
                $"Claude invalid JSON response: {ex.Message}");
        }
    }

    private static string FormatConversation(List<WapCrmMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages.Take(50)) // Limit to last 50 messages
        {
            var sender = msg.MessageSource == "ME" ? "Agent" : "Customer";
            sb.AppendLine($"[{msg.SentDate} {msg.SentTime}] {sender}: {msg.Message}");
        }
        return sb.ToString();
    }

    private static string BuildPrompt(string conversation)
    {
        return $@"Analyze the following customer service conversation and provide:
1. sentiment: The overall customer sentiment (must be exactly one of: positive, negative, neutral)
2. category: The primary topic (must be exactly one of: Destek, Satis, Sikayet, Bilgi)
3. confidence: Your confidence level (0.0 to 1.0)
4. summary: A brief summary in Turkish (max 200 characters)

Respond ONLY in this exact JSON format, no other text:
{{""sentiment"":""..."",""category"":""..."",""confidence"":0.0,""summary"":""...""}}

Conversation:
{conversation}";
    }

    private (AnalysisResult result, bool parseFailed, string? parseError) ParseAnalysis(string text)
    {
        // Extract JSON from response (Claude might add extra text)
        var jsonStart = text.IndexOf('{');
        var jsonEnd = text.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            try
            {
                var json = text.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AnalysisResult>(json, _jsonOptions);

                if (parsed != null)
                {
                    // Validate and normalize values
                    var result = new AnalysisResult
                    {
                        Sentiment = ValidateSentiment(parsed.Sentiment),
                        Category = ValidateCategory(parsed.Category),
                        Confidence = Math.Clamp(parsed.Confidence, 0, 1),
                        Summary = TruncateSummary(parsed.Summary)
                    };
                    return (result, false, null);
                }
            }
            catch (JsonException ex)
            {
                // JSON parsing failed - will return fallback with error info
                return (CreateFallbackResult(), true, $"JSON parse error: {ex.Message}");
            }
        }

        // No valid JSON found
        var preview = text.Length > 100 ? text[..100] + "..." : text;
        return (CreateFallbackResult(), true, $"No valid JSON found in response: {preview}");
    }

    private static AnalysisResult CreateFallbackResult() => new()
    {
        Sentiment = AnalysisCategories.SentimentNeutral,
        Category = AnalysisCategories.CategoryBilgi,
        Confidence = 0.0, // Low confidence = fallback indicator
        Summary = "Analiz yapılamadı (parse hatası)"
    };

    private static string ValidateSentiment(string sentiment)
    {
        var lower = sentiment?.ToLowerInvariant() ?? "";
        return lower switch
        {
            "positive" => AnalysisCategories.SentimentPositive,
            "negative" => AnalysisCategories.SentimentNegative,
            _ => AnalysisCategories.SentimentNeutral
        };
    }

    private static string ValidateCategory(string category)
    {
        // Case-insensitive match
        var normalized = category?.Trim() ?? "";
        foreach (var valid in AnalysisCategories.ValidCategories)
        {
            if (string.Equals(normalized, valid, StringComparison.OrdinalIgnoreCase))
                return valid;
        }
        return AnalysisCategories.CategoryBilgi;
    }

    private static string TruncateSummary(string summary)
    {
        if (string.IsNullOrEmpty(summary))
            return "Özet oluşturulamadı";

        return summary.Length > 200 ? summary[..197] + "..." : summary;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

// Claude API request/response models

internal sealed class ClaudeRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("max_tokens")]
    public required int MaxTokens { get; init; }

    [JsonPropertyName("messages")]
    public required List<ClaudeMessage> Messages { get; init; }
}

internal sealed class ClaudeMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

internal sealed class ClaudeResponse
{
    [JsonPropertyName("content")]
    public List<ClaudeContent>? Content { get; init; }
}

internal sealed class ClaudeContent
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

/// <summary>
/// Result wrapper for Claude operations
/// </summary>
public sealed class ClaudeResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Data { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }
    public string? Warning { get; private init; }

    private ClaudeResult() { }

    public static ClaudeResult<T> Success(T data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static ClaudeResult<T> SuccessWithWarning(T data, string warning) => new()
    {
        IsSuccess = true,
        Data = data,
        Warning = warning
    };

    public static ClaudeResult<T> Fail(string errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
