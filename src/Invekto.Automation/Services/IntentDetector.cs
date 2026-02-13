using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Intent detection using Claude Haiku API.
/// Independent from ChatAnalysis -- Automation has its own Claude integration.
/// Supports dynamic custom intents (Phase 4b) or default 5 intents.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class IntentDetector
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly JsonLinesLogger _logger;

    private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-haiku-4-5-20251001";
    private const int MaxTokens = 256;
    private const int TimeoutMs = 10000;

    // Default intents (used when no custom intents provided)
    public static readonly string[] DefaultIntents =
    {
        "shipping_inquiry",    // kargo, teslimat, gonderi
        "price_inquiry",       // fiyat, ucret, maliyet
        "appointment",         // randevu, rezervasyon, saat
        "complaint",           // sikayet, sorun, problem
        "general_question"     // genel soru, bilgi
    };

    private static readonly string DefaultSystemPrompt = BuildSystemPrompt(DefaultIntents);

    public IntentDetector(HttpClient httpClient, string apiKey, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _logger = logger;
    }

    /// <summary>
    /// Detect intent from a user message using Claude Haiku.
    /// Returns intent result or null on failure (graceful degradation).
    /// </summary>
    /// <param name="userMessage">User's message text</param>
    /// <param name="customIntents">Custom intent labels. Null = use default 5 intents.</param>
    /// <param name="ct">Cancellation token</param>
    public async Task<IntentResult?> DetectAsync(string userMessage, string[]? customIntents = null, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutMs);

            var systemPrompt = customIntents != null && customIntents.Length > 0
                ? BuildSystemPrompt(customIntents)
                : DefaultSystemPrompt;

            var requestBody = new
            {
                model = Model,
                max_tokens = MaxTokens,
                system = systemPrompt,
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
                _logger.SystemWarn($"Claude API HTTP {(int)response.StatusCode}: {errorBody}");
                return null;
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonDocument>(cts.Token);
            if (responseJson == null)
            {
                _logger.SystemWarn("Claude API returned null JSON response body");
                return null;
            }

            // Extract text from Claude response
            var content = responseJson.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(content))
            {
                _logger.SystemWarn("Claude API response content text is empty");
                return null;
            }

            // Parse intent JSON from response
            return ParseIntentResponse(content);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // App shutting down
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn("Claude intent detection timeout");
            return null;
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"Intent detection failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Build Claude system prompt with the given intent labels.
    /// Dynamic intents are listed in the prompt so Claude knows what to detect.
    /// </summary>
    private static string BuildSystemPrompt(string[] intents)
    {
        var intentList = string.Join("\n", intents.Select(i => $"- {i}"));

        return $@"Sen bir musteri mesaji niyet (intent) algilama sistemisin.

Mesaji analiz et ve asagidaki intent'lerden birini sec:
{intentList}

JSON olarak cevap ver (baska metin yazma):
{{""intent"": ""<intent_name>"", ""confidence"": <0.0-1.0>, ""summary"": ""<1 cumle ozet>""}}";
    }

    private IntentResult? ParseIntentResponse(string responseText)
    {
        try
        {
            // Claude might wrap JSON in markdown code blocks
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

            var intent = root.GetProperty("intent").GetString();
            var confidence = root.GetProperty("confidence").GetDouble();
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() : null;

            if (string.IsNullOrEmpty(intent))
            {
                _logger.SystemWarn($"Parsed intent is empty from Claude response: {responseText}");
                return null;
            }

            return new IntentResult
            {
                Intent = intent,
                Confidence = Math.Clamp(confidence, 0.0, 1.0),
                Summary = summary ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"Failed to parse intent response: {ex.Message}, raw={responseText}");
            return null;
        }
    }
}

public sealed class IntentResult
{
    public required string Intent { get; init; }
    public double Confidence { get; init; }
    public required string Summary { get; init; }
}
