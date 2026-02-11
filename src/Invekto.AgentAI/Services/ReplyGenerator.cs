using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Invekto.Shared.DTOs.AgentAI;
using Invekto.Shared.Logging;

namespace Invekto.AgentAI.Services;

/// <summary>
/// Generates AI reply suggestions using Claude API.
/// Includes agent profile context for personalized suggestions.
/// Thread-safe, register as singleton via AddHttpClient.
/// </summary>
public sealed class ReplyGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _timeoutMs;
    private readonly JsonLinesLogger _logger;

    private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
    private const int MaxTokens = 512;

    public ReplyGenerator(HttpClient httpClient, string apiKey, string model, int timeoutMs, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _timeoutMs = timeoutMs;
        _logger = logger;
    }

    public string ModelName => _model;

    /// <summary>
    /// Generate a reply suggestion with intent detection.
    /// Returns null on failure (graceful degradation).
    /// </summary>
    public async Task<ReplyResult?> GenerateAsync(
        SuggestReplyRequest request,
        string? agentProfile,
        string? templateSuggestion,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeoutMs);

            var systemPrompt = BuildSystemPrompt(request.Language, agentProfile, templateSuggestion);
            var userPrompt = BuildUserPrompt(request);

            var requestBody = new
            {
                model = _model,
                max_tokens = MaxTokens,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userPrompt }
                }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ClaudeApiUrl);
            httpRequest.Headers.Add("x-api-key", _apiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");
            httpRequest.Content = JsonContent.Create(requestBody);

            using var response = await _httpClient.SendAsync(httpRequest, cts.Token);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.SystemWarn($"Claude API HTTP {(int)response.StatusCode}: {errorBody}");
                return null;
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonDocument>(cts.Token);
            if (responseJson == null)
                return null;

            var content = responseJson.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(content))
                return null;

            var result = ParseResponse(content);
            if (result != null)
                result.ProcessingTimeMs = sw.ElapsedMilliseconds;

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // App shutting down
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _logger.SystemWarn($"Claude reply generation timeout after {sw.ElapsedMilliseconds}ms");
            return new ReplyResult { ErrorCode = "timeout", ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.SystemWarn($"Reply generation failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
            return null;
        }
    }

    private static string BuildSystemPrompt(string language, string? agentProfile, string? templateSuggestion)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Sen bir musteri hizmetleri asistanisin. Agent'a cevap onerisi uretiyorsun.");
        sb.AppendLine();
        sb.AppendLine("KURALLAR:");
        sb.AppendLine("- Kibbar, profesyonel ve yardimci ol");
        sb.AppendLine("- Kisa ve net cevap ver (1-3 cumle)");
        sb.AppendLine("- Musteri adini biliyorsan kullan");
        sb.AppendLine("- Tibbi tavsiye verme, hukuki tavsiye verme");
        sb.AppendLine($"- Dil: {(language == "tr" ? "Turkce" : language == "en" ? "English" : language)}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(agentProfile))
        {
            sb.AppendLine(agentProfile);
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(templateSuggestion))
        {
            sb.AppendLine($"Mevcut sablon onerisi: \"{templateSuggestion}\"");
            sb.AppendLine("Bu sablonu referans al ama gerekiyorsa duzelt veya genislet.");
            sb.AppendLine();
        }

        sb.AppendLine("JSON olarak cevap ver (baska metin yazma):");
        sb.Append("{\"suggested_reply\": \"<oneri>\", \"intent\": \"<intent>\", \"confidence\": <0.0-1.0>}");

        return sb.ToString();
    }

    private static string BuildUserPrompt(SuggestReplyRequest request)
    {
        var sb = new StringBuilder();

        // Conversation history
        if (request.ConversationHistory is { Count: > 0 })
        {
            sb.AppendLine("Sohbet gecmisi:");
            foreach (var msg in request.ConversationHistory)
            {
                var role = msg.Source == "CUSTOMER" ? "Musteri" : "Agent";
                sb.AppendLine($"  {role}: {msg.Text}");
            }
            sb.AppendLine();
        }

        // Current message
        sb.AppendLine($"Musteri mesaji: {request.MessageText}");

        if (!string.IsNullOrEmpty(request.CustomerName))
            sb.AppendLine($"Musteri adi: {request.CustomerName}");

        if (!string.IsNullOrEmpty(request.Channel))
            sb.AppendLine($"Kanal: {request.Channel}");

        sb.AppendLine();
        sb.Append("Bu mesaja cevap onerisi uret.");

        return sb.ToString();
    }

    private ReplyResult? ParseResponse(string responseText)
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

            var suggestedReply = root.TryGetProperty("suggested_reply", out var sr) ? sr.GetString() : null;
            var intent = root.TryGetProperty("intent", out var i) ? i.GetString() : null;
            var confidence = root.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.0;

            if (string.IsNullOrEmpty(suggestedReply))
                return null;

            return new ReplyResult
            {
                SuggestedReply = suggestedReply,
                Intent = intent,
                Confidence = Math.Clamp(confidence, 0.0, 1.0)
            };
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"Failed to parse reply response: {ex.Message}, raw={responseText}");
            return null;
        }
    }
}

public sealed class ReplyResult
{
    public string SuggestedReply { get; set; } = "";
    public string? Intent { get; set; }
    public double Confidence { get; set; }
    public long ProcessingTimeMs { get; set; }
    public string? ErrorCode { get; set; }
    public bool IsSuccess => ErrorCode == null && !string.IsNullOrEmpty(SuggestedReply);
}
