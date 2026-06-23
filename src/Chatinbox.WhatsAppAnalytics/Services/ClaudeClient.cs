using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Chatinbox.Shared.Logging;

namespace Chatinbox.WhatsAppAnalytics.Services;

/// <summary>
/// Claude Haiku API client for NLP stages 4 (intent) and 6 (sentiment).
/// Keyword-first hybrid: API key missing → keyword-only mode (no API calls).
/// Per-batch error handling: failure → fallback to "unknown"/"skipped", pipeline continues.
/// </summary>
public sealed class ClaudeClient : ILlmClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly int _maxTokens;
    private readonly int _timeoutSeconds;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string ModelName => _model;
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public ClaudeClient(string? apiKey, string model, int maxTokens, int timeoutSeconds, JsonLinesLogger logger)
    {
        _apiKey = apiKey;
        _model = model;
        _maxTokens = maxTokens;
        _timeoutSeconds = timeoutSeconds;
        _logger = logger;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.anthropic.com"),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    /// <summary>
    /// Send a batch of text items to Claude for classification/analysis.
    /// Returns the raw response text (expected JSON array), or null on any failure.
    /// All errors are handled internally — caller receives null and falls back to keyword-only.
    /// </summary>
    public async Task<string?> SendBatchAsync(string systemPrompt, string userContent, CancellationToken ct)
    {
        if (!IsAvailable)
        {
            _logger.SystemWarn("[ClaudeClient] API key not configured, skipping batch");
            return null;
        }

        try
        {
            var requestBody = new
            {
                model = _model,
                max_tokens = _maxTokens,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userContent }
                }
            };

            var json = JsonSerializer.Serialize(requestBody, JsonOpts);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", _apiKey);

            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.SystemWarn($"[ClaudeClient] API error {(int)response.StatusCode}: {errorBody[..Math.Min(errorBody.Length, 200)]}");
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("content", out var contentArr) &&
                contentArr.GetArrayLength() > 0 &&
                contentArr[0].TryGetProperty("text", out var textEl))
            {
                return textEl.GetString();
            }

            _logger.SystemWarn("[ClaudeClient] Unexpected response structure: missing content[0].text");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[ClaudeClient] Connection error: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.SystemWarn($"[ClaudeClient] Request timed out after {_timeoutSeconds}s");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[ClaudeClient] Response parse error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parse a JSON array from Claude's response text.
    /// Handles cases where Claude wraps JSON in markdown code blocks.
    /// Returns empty list on parse failure.
    /// </summary>
    public static List<T> ParseJsonArray<T>(string? responseText, JsonLinesLogger logger)
    {
        if (string.IsNullOrEmpty(responseText)) return new List<T>();

        var text = responseText.Trim();

        // Strip markdown code block if present
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline > 0)
                text = text[(firstNewline + 1)..];
            if (text.EndsWith("```"))
                text = text[..^3];
            text = text.Trim();
        }

        // Find the JSON array bounds
        var startIdx = text.IndexOf('[');
        var endIdx = text.LastIndexOf(']');
        if (startIdx < 0 || endIdx <= startIdx)
        {
            logger.SystemWarn($"[ClaudeClient] No JSON array found in response: {text[..Math.Min(text.Length, 100)]}");
            return new List<T>();
        }

        var jsonStr = text[startIdx..(endIdx + 1)];

        try
        {
            return JsonSerializer.Deserialize<List<T>>(jsonStr, JsonOpts) ?? new List<T>();
        }
        catch (JsonException ex)
        {
            logger.SystemWarn($"[ClaudeClient] JSON parse error: {ex.Message}, text: {jsonStr[..Math.Min(jsonStr.Length, 100)]}");
            return new List<T>();
        }
    }

    /// <summary>
    /// ILlmClient implementation — delegates to SendBatchAsync.
    /// </summary>
    public Task<string?> ClassifyAsync(string systemPrompt, string userContent, CancellationToken ct)
        => SendBatchAsync(systemPrompt, userContent, ct);

    public void Dispose() => _httpClient.Dispose();
}
