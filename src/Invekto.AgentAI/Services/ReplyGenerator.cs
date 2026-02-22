using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Invekto.Shared.DTOs.AgentAI;
using Invekto.Shared.Logging;

namespace Invekto.AgentAI.Services;

/// <summary>
/// Generates AI reply suggestions using Claude API.
/// GR-2.2: Knowledge context injection, tone presets, follow-up questions, conversation summary.
/// GR-2.3: Inline language detection, multi-lang prompt templates.
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
    private const int MaxTokens = 768; // Increased for follow-up + sources

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
    /// Generate a reply suggestion with intent detection, knowledge context, follow-up, and language detection.
    /// Returns null on failure (graceful degradation).
    /// </summary>
    public async Task<ReplyResult?> GenerateAsync(
        SuggestReplyRequest request,
        string? agentProfile,
        string? templateSuggestion,
        string? knowledgeContext,
        string? tone,
        string? conversationSummary,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeoutMs);

            var systemPrompt = BuildSystemPrompt(
                request.Language, agentProfile, templateSuggestion,
                knowledgeContext, tone, conversationSummary);
            var userPrompt = BuildUserPrompt(request, conversationSummary);

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
                _logger.SystemWarn($"[ReplyGenerator] Claude API HTTP {(int)response.StatusCode}: {errorBody}");
                return null;
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonDocument>(cts.Token);
            if (responseJson == null)
            {
                _logger.SystemWarn("[ReplyGenerator] Claude returned null JSON body");
                return null;
            }

            var content = responseJson.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(content))
            {
                _logger.SystemWarn("[ReplyGenerator] Claude returned empty content text");
                return null;
            }

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
            _logger.SystemWarn($"[ReplyGenerator] Claude reply generation timeout after {sw.ElapsedMilliseconds}ms");
            return new ReplyResult { ErrorCode = "timeout", ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.SystemWarn($"[ReplyGenerator] Reply generation failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
            return null;
        }
    }

    private static string BuildSystemPrompt(
        string language, string? agentProfile, string? templateSuggestion,
        string? knowledgeContext, string? tone, string? conversationSummary)
    {
        var sb = new StringBuilder();

        // Base instruction - language-aware with XML tags
        if (language == "en")
        {
            sb.AppendLine("<role>Customer service assistant generating reply suggestions for agents.</role>");
            sb.AppendLine("<rules>Be polite and professional. Give short, clear answers (1-3 sentences). Use customer's name if known. Never give medical or legal advice.</rules>");
        }
        else
        {
            sb.AppendLine("<role>Musteri hizmetleri asistani. Agent'a WhatsApp uzerinden cevap onerisi uretiyorsun.</role>");
            sb.AppendLine("<rules>Kibbar ve profesyonel ol. Kisa ve net cevap ver (1-3 cumle), cunku WhatsApp'ta uzun mesajlar okunmuyor. Musteri adini biliyorsan kullan. Tibbi veya hukuki tavsiye verme.</rules>");
        }

        // GR-2.3: Language detection
        sb.AppendLine("<language_detection>Musterinin mesaj dilini algilayip ayni dilde yanit ver. Algilanan dili detected_language alanina ISO 639-1 koduyla yaz.</language_detection>");

        // GR-2.2: Tone preset
        if (!string.IsNullOrEmpty(tone))
        {
            var toneInstruction = tone.ToLowerInvariant() switch
            {
                "formal" => "Resmi ve profesyonel dil kullan. 'Siz' hitabi, kurumsal ifadeler.",
                "casual" => "Samimi ve sicak dil kullan. Dogal, sohbet havasi. Emoji kullanabilirsin.",
                "concise" => "Cok kisa ve oze odakli cevap ver. Maksimum 1-2 cumle.",
                _ => tone
            };
            sb.AppendLine($"<tone>{toneInstruction}</tone>");
        }

        // GR-2.2: Knowledge context
        if (!string.IsNullOrEmpty(knowledgeContext))
        {
            sb.AppendLine("<knowledge_base>");
            sb.AppendLine(knowledgeContext);
            sb.AppendLine("Bu bilgileri dogal bir sekilde cevaba entegre et, dogrudan kopyalama.");
            sb.AppendLine("</knowledge_base>");
        }

        // Agent profile (feedback learning)
        if (!string.IsNullOrEmpty(agentProfile))
            sb.AppendLine($"<agent_profile>{agentProfile}</agent_profile>");

        // Template suggestion
        if (!string.IsNullOrEmpty(templateSuggestion))
            sb.AppendLine($"<template>Referans sablon: \"{templateSuggestion}\". Gerekiyorsa duzelt veya genislet.</template>");

        // GR-2.2: Conversation summary context
        if (!string.IsNullOrEmpty(conversationSummary))
            sb.AppendLine($"<conversation_summary>{conversationSummary} — Bu ozeti dikkate al ama tekrarlama, son mesajlara odaklan.</conversation_summary>");

        // Output format
        sb.AppendLine("<output_format>JSON olarak cevap ver, baska metin yazma: {\"suggested_reply\": \"...\", \"intent\": \"...\", \"confidence\": 0.0-1.0, \"suggested_followup\": \"...veya null\", \"detected_language\": \"iso_639_1\"}</output_format>");

        return sb.ToString();
    }

    private static string BuildUserPrompt(SuggestReplyRequest request, string? conversationSummary)
    {
        var sb = new StringBuilder();

        // Conversation history (already trimmed or summarized by caller)
        if (request.ConversationHistory is { Count: > 0 })
        {
            if (!string.IsNullOrEmpty(conversationSummary))
                sb.AppendLine("Son mesajlar:");
            else
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
            var suggestedFollowup = root.TryGetProperty("suggested_followup", out var sf) ? sf.GetString() : null;
            var detectedLanguage = root.TryGetProperty("detected_language", out var dl) ? dl.GetString() : null;

            // GR-2.3: Fallback if Claude omits detected_language
            if (string.IsNullOrEmpty(detectedLanguage) || detectedLanguage == "null")
            {
                _logger.SystemWarn("[ReplyGenerator] Claude omitted detected_language, defaulting to 'tr'");
                detectedLanguage = "tr";
            }

            if (string.IsNullOrEmpty(suggestedReply))
            {
                _logger.SystemWarn("[ReplyGenerator] Parsed JSON has empty suggested_reply");
                return null;
            }

            // Normalize "null" string to actual null
            if (suggestedFollowup is "null" or "")
                suggestedFollowup = null;

            return new ReplyResult
            {
                SuggestedReply = suggestedReply,
                Intent = intent,
                Confidence = Math.Clamp(confidence, 0.0, 1.0),
                SuggestedFollowup = suggestedFollowup,
                DetectedLanguage = detectedLanguage
            };
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[ReplyGenerator] JSON parse error: {ex.Message}, raw={responseText}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"[ReplyGenerator] Failed to parse reply response: {ex.Message}, raw={responseText}");
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
    public string? SuggestedFollowup { get; set; }
    public string? DetectedLanguage { get; set; }
    public bool IsSuccess => ErrorCode == null && !string.IsNullOrEmpty(SuggestedReply);
}
