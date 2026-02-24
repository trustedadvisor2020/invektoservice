using System.Text;
using System.Text.Json;
using Invekto.Shared.Logging;

namespace Invekto.WhatsAppAnalytics.Services;

/// <summary>
/// Google Gemini API client for LLM benchmark.
/// Raw HttpClient — no SDK, mirrors ClaudeClient pattern.
/// Two instances registered: Flash (gemini-2.0-flash) and Pro (gemini-2.5-pro).
/// </summary>
public sealed class GeminiClient : ILlmClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly int _maxTokens;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string ModelName => _model;
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public GeminiClient(string? apiKey, string model, int maxTokens, int timeoutSeconds, JsonLinesLogger logger)
    {
        _apiKey = apiKey;
        _model = model;
        _maxTokens = maxTokens;
        _logger = logger;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com"),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
    }

    public async Task<string?> ClassifyAsync(string systemPrompt, string userContent, CancellationToken ct)
    {
        if (!IsAvailable)
        {
            _logger.SystemWarn($"[GeminiClient:{_model}] API key not configured, skipping");
            return null;
        }

        try
        {
            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new { text = systemPrompt }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userContent } }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = _maxTokens,
                    temperature = 0.1
                }
            };

            var json = JsonSerializer.Serialize(requestBody, JsonOpts);
            var endpoint = $"/v1beta/models/{_model}:generateContent?key={_apiKey}";

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.SystemWarn($"[GeminiClient:{_model}] API error {(int)response.StatusCode}: {errorBody[..Math.Min(errorBody.Length, 200)]}");
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            // Parse: candidates[0].content.parts[0].text
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var textEl))
            {
                return textEl.GetString();
            }

            _logger.SystemWarn($"[GeminiClient:{_model}] Unexpected response structure");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[GeminiClient:{_model}] Connection error: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.SystemWarn($"[GeminiClient:{_model}] Request timed out");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[GeminiClient:{_model}] Response parse error: {ex.Message}");
            return null;
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
