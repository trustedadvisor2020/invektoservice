using System.Net.Http.Json;
using System.Text.Json;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Automation.Services;

/// <summary>
/// Sentiment analysis using Claude Haiku API.
/// Follows IntentDetector pattern — own HttpClient, own prompt, graceful degradation.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class SentimentAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly JsonLinesLogger _logger;

    private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-haiku-4-5-20251001";
    private const int MaxTokens = 256;
    private const int TimeoutMs = 10000;

    private const string SystemPrompt =
        @"Sen bir musteri mesaji duygu (sentiment) analiz sistemisin.

Mesaji analiz et ve duygu durumunu belirle.

JSON olarak cevap ver (baska metin yazma):
{""sentiment"": ""positive|negative|neutral"", ""score"": <0.0-1.0>, ""summary"": ""<1 cumle ozet>""}

score: 0.0 = cok negatif, 0.5 = notr, 1.0 = cok pozitif";

    public SentimentAnalyzer(HttpClient httpClient, string apiKey, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _logger = logger;
    }

    /// <summary>
    /// Analyze sentiment of a user message using Claude Haiku.
    /// Returns result or null on failure (graceful degradation).
    /// </summary>
    public async Task<SentimentResult?> AnalyzeAsync(string userMessage, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutMs);

            var requestBody = new
            {
                model = Model,
                max_tokens = MaxTokens,
                system = SystemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userMessage }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, ClaudeApiUrl);
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = JsonContent.Create(requestBody);

            using var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.SystemWarn($"Claude Sentiment API HTTP {(int)response.StatusCode}: {errorBody}");
                return null;
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonDocument>(cts.Token);
            if (responseJson == null)
            {
                _logger.SystemWarn("Claude Sentiment API returned null JSON response body");
                return null;
            }

            var content = responseJson.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(content))
            {
                _logger.SystemWarn("Claude Sentiment API response content text is empty");
                return null;
            }

            return ParseSentimentResponse(content);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // App shutting down
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn("Claude sentiment analysis timeout");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"Sentiment analysis HTTP error: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"Sentiment analysis JSON parse error: {ex.Message}");
            return null;
        }
        catch (KeyNotFoundException ex)
        {
            _logger.SystemWarn($"Sentiment analysis missing JSON property: {ex.Message}");
            return null;
        }
    }

    private SentimentResult? ParseSentimentResponse(string responseText)
    {
        try
        {
            var json = responseText.Trim();
            if (json.StartsWith("```"))
            {
                var startIdx = json.IndexOf('{');
                var endIdx = json.LastIndexOf('}');
                if (startIdx >= 0 && endIdx > startIdx)
                    json = json[startIdx..(endIdx + 1)];
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var sentiment = root.GetProperty("sentiment").GetString();
            var score = root.GetProperty("score").GetDouble();
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() : null;

            if (string.IsNullOrEmpty(sentiment))
            {
                _logger.SystemWarn($"Parsed sentiment is empty from Claude response: {responseText}");
                return null;
            }

            return new SentimentResult
            {
                Sentiment = sentiment,
                Score = Math.Clamp(score, 0.0, 1.0),
                Summary = summary ?? ""
            };
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"Failed to parse sentiment JSON: {ex.Message}, raw={responseText}");
            return null;
        }
        catch (KeyNotFoundException ex)
        {
            _logger.SystemWarn($"Missing property in sentiment response: {ex.Message}, raw={responseText}");
            return null;
        }
        catch (FormatException ex)
        {
            _logger.SystemWarn($"Invalid number format in sentiment response: {ex.Message}, raw={responseText}");
            return null;
        }
    }
}

public sealed class SentimentResult
{
    public required string Sentiment { get; init; } // "positive", "negative", "neutral"
    public double Score { get; init; } // 0.0 = very negative, 1.0 = very positive
    public required string Summary { get; init; }
}
