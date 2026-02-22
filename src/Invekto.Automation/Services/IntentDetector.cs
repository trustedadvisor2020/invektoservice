using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

    // Common fake name patterns
    private static readonly string[] FakeNamePatterns =
        { "asdf", "qwer", "zxcv", "test", "xxx", "aaa", "bbb", "abc", "123", "admin", "null", "undefined" };

    // Greeting prefixes to strip when extracting name
    private static readonly string[] GreetingPrefixes =
        { "merhaba", "selam", "hey", "iyi gunler", "iyi günler", "iyi aksamlar", "iyi akşamlar" };

    // Name extraction patterns (Turkish)
    private static readonly Regex[] NamePatterns =
    {
        new(@"(?:benim\s+)?ad[ıi]m\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(?:benim\s+)?ismim\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^ben\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };

    // Confirmation keywords
    private static readonly string[] PositiveConfirmations =
        { "evet", "yes", "doğru", "dogru", "aynen", "tamam", "kesinlikle", "tabii", "tabi", "yep", "onu" };
    private static readonly string[] NegativeConfirmations =
        { "hayır", "hayir", "no", "değil", "degil", "yanlış", "yanlis", "başka", "baska", "öyle değil", "oyle degil" };

    public IntentDetector(HttpClient httpClient, string apiKey, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _logger = logger;
    }

    // ============================================================
    // Name helpers (static, no API call)
    // ============================================================

    /// <summary>
    /// Extract a name from user input, stripping greetings and Turkish patterns.
    /// E.g. "Merhaba, ben Ali" → "Ali", "adım Zeynep" → "Zeynep"
    /// </summary>
    public static string ExtractName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        var text = input.Trim();

        // Try Turkish patterns: "ben X", "adım X", "ismim X"
        foreach (var pattern in NamePatterns)
        {
            var match = pattern.Match(text);
            if (match.Success)
            {
                var extracted = match.Groups[1].Value.Trim().TrimEnd('.', ',', '!', '?');
                if (!string.IsNullOrWhiteSpace(extracted))
                    return extracted;
            }
        }

        // Strip greeting prefixes: "merhaba Ali" → "Ali"
        var lower = text.ToLowerInvariant();
        foreach (var g in GreetingPrefixes)
        {
            if (lower.StartsWith(g, StringComparison.Ordinal))
            {
                var rest = text[g.Length..].Trim().TrimStart(',', ' ');
                if (!string.IsNullOrWhiteSpace(rest))
                {
                    // Only use rest if it looks like a name (not a full sentence)
                    if (!rest.Contains(' ') || rest.Split(' ').Length <= 3)
                        return rest.TrimEnd('.', ',', '!', '?');
                }
            }
        }

        // Use whole input as name
        return text.TrimEnd('.', ',', '!', '?');
    }

    /// <summary>
    /// Validate a name with heuristic rules. No API call.
    /// Returns (isValid, rejectionMessage).
    /// </summary>
    public static (bool IsValid, string? Message) ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
            return (false, "Adınızı duyamadım, tekrar yazar mısınız?");

        if (name.Any(char.IsDigit))
            return (false, "İsimde rakam olmaz, gerçek isminizi yazabilir misiniz?");

        if (name.ToLowerInvariant().Distinct().Count() <= 1)
            return (false, "Gerçek isminizi yazabilir misiniz?");

        var lower = name.ToLowerInvariant();
        if (FakeNamePatterns.Any(lower.Contains))
            return (false, "Bu gerçek bir isim gibi görünmüyor. Gerçek isminizi yazabilir misiniz?");

        if (name.Length > 50)
            return (false, "Sadece isminizi yazmanız yeterli.");

        // Allow letters (any script), spaces, hyphens, apostrophes
        if (!name.All(c => char.IsLetter(c) || c == ' ' || c == '-' || c == '\''))
            return (false, "Lütfen sadece isminizi yazın.");

        return (true, null);
    }

    /// <summary>
    /// Parse a yes/no confirmation from user input. Returns true=yes, false=no, null=unclear.
    /// </summary>
    public static bool? ParseConfirmation(string input)
    {
        var lower = input.Trim().ToLowerInvariant();

        if (PositiveConfirmations.Any(p => lower.Contains(p)))
            return true;

        if (NegativeConfirmations.Any(p => lower.Contains(p)))
            return false;

        return null;
    }

    // ============================================================
    // Simple intent detection (existing, backward-compatible)
    // ============================================================

    /// <summary>
    /// Detect intent from a user message using Claude Haiku.
    /// Returns intent result or null on failure (graceful degradation).
    /// </summary>
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

            var content = responseJson.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(content))
            {
                _logger.SystemWarn("Claude API response content text is empty");
                return null;
            }

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
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"Intent detection HTTP error: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"Intent detection JSON parse error: {ex.Message}");
            return null;
        }
    }

    // ============================================================
    // Conversational intent detection (new - multi-turn with history)
    // ============================================================

    /// <summary>
    /// Detect intent with conversation history context. Returns intent OR a clarifying question.
    /// Used by the multi-phase AiIntentHandler for "don't give up" behavior.
    /// </summary>
    public async Task<ConversationalIntentResult?> DetectConversationalAsync(
        string userMessage,
        string customerName,
        string[]? customIntents,
        List<ConversationTurn>? history,
        CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutMs);

            var intents = customIntents is { Length: > 0 } ? customIntents : DefaultIntents;
            var systemPrompt = BuildConversationalPrompt(intents, customerName);

            // Build messages array with conversation history
            var messages = new List<object>();
            if (history != null)
            {
                foreach (var turn in history)
                    messages.Add(new { role = turn.Role, content = turn.Content });
            }
            messages.Add(new { role = "user", content = userMessage });

            var requestBody = new
            {
                model = Model,
                max_tokens = MaxTokens,
                system = systemPrompt,
                messages
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, ClaudeApiUrl);
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = JsonContent.Create(requestBody);

            using var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.SystemWarn($"Claude conversational API HTTP {(int)response.StatusCode}: {errorBody}");
                return null;
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonDocument>(cts.Token);
            if (responseJson == null) return null;

            var content = responseJson.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(content)) return null;

            return ParseConversationalResponse(content);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn("Claude conversational intent detection timeout");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"Conversational intent detection HTTP error: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"Conversational intent detection JSON parse error: {ex.Message}");
            return null;
        }
    }

    // ============================================================
    // Prompt builders
    // ============================================================

    private static string BuildSystemPrompt(string[] intents)
    {
        var intentList = string.Join("\n", intents.Select(i => $"- {i}"));

        return $@"Sen bir musteri mesaji niyet (intent) algilama sistemisin.

Mesaji analiz et ve asagidaki intent'lerden birini sec:
{intentList}

JSON olarak cevap ver (baska metin yazma):
{{""intent"": ""<intent_name>"", ""confidence"": <0.0-1.0>, ""summary"": ""<1 cumle ozet>""}}";
    }

    private static string BuildConversationalPrompt(string[] intents, string customerName)
    {
        var intentList = string.Join("\n", intents.Select(i => $"- {i}"));

        return $@"Sen bir musteri hizmetleri asistanisin. Musterinin adi: {customerName}.

Gorevin musterinin mesajini analiz edip asagidaki intent'lerden birini belirlemek:
{intentList}

Kurallar:
- Musterinin ne istedigini anlayabildiysen intent ve confidence dondur.
- Emin degilsen ama bir tahminin varsa, intent'i belirt ve {customerName}'a onay sorusu oner.
- Hic emin degilsen, {customerName}'a yonlendirici ve nazik bir soru sor.
- ASLA ""anlamadim"" veya ""tekrar eder misiniz"" deme. Her zaman ipuclarindan yola cikarak akilli bir soru sor.
- {customerName} ismiyle hitap et. Kisa ve samimi ol.

JSON cevap ver (baska metin yazma):
{{""intent"": ""<intent veya null>"", ""confidence"": <0.0-1.0>, ""summary"": ""<ozet>"", ""clarify"": ""<soru veya null>""}}";
    }

    // ============================================================
    // Response parsers
    // ============================================================

    private IntentResult? ParseIntentResponse(string responseText)
    {
        try
        {
            var json = StripMarkdownCodeBlock(responseText);

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
        catch (JsonException ex)
        {
            _logger.SystemWarn($"Failed to parse intent response: {ex.Message}, raw={responseText}");
            return null;
        }
    }

    private ConversationalIntentResult? ParseConversationalResponse(string responseText)
    {
        try
        {
            var json = StripMarkdownCodeBlock(responseText);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var intent = root.TryGetProperty("intent", out var ip) && ip.ValueKind != JsonValueKind.Null
                ? ip.GetString() : null;
            var confidence = root.TryGetProperty("confidence", out var cp) ? cp.GetDouble() : 0;
            var summary = root.TryGetProperty("summary", out var sp) ? sp.GetString() : "";
            var clarify = root.TryGetProperty("clarify", out var clp) && clp.ValueKind != JsonValueKind.Null
                ? clp.GetString() : null;

            return new ConversationalIntentResult
            {
                Intent = string.IsNullOrEmpty(intent) ? null : intent,
                Confidence = Math.Clamp(confidence, 0.0, 1.0),
                Summary = summary ?? "",
                ClarifyQuestion = string.IsNullOrEmpty(clarify) ? null : clarify
            };
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"Failed to parse conversational intent response: {ex.Message}, raw={responseText}");
            return null;
        }
    }

    private static string StripMarkdownCodeBlock(string text)
    {
        var json = text.Trim();
        if (json.StartsWith("```"))
        {
            var startIdx = json.IndexOf('{');
            var endIdx = json.LastIndexOf('}');
            if (startIdx >= 0 && endIdx > startIdx)
                json = json[startIdx..(endIdx + 1)];
        }
        return json;
    }
}

// ============================================================
// Result types
// ============================================================

public sealed class IntentResult
{
    public required string Intent { get; init; }
    public double Confidence { get; init; }
    public required string Summary { get; init; }
}

public sealed class ConversationalIntentResult
{
    public string? Intent { get; init; }
    public double Confidence { get; init; }
    public string Summary { get; init; } = "";
    public string? ClarifyQuestion { get; init; }
}

public sealed class ConversationTurn
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }
}
