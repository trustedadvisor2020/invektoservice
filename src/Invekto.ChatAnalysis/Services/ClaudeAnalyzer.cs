using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.ChatAnalysis;

namespace Invekto.ChatAnalysis.Services;

/// <summary>
/// Claude API analyzer for comprehensive chat analysis (V2)
/// Uses Claude Haiku for fast analysis with 15 criteria
/// </summary>
public sealed class ClaudeAnalyzer : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-3-haiku-20240307";
    private const int MaxTokens = 4096; // Increased for 15 criteria
    private const int TimeoutMs = 30000; // 30 second timeout

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
    /// Analyze chat messages with 15 criteria and label selection
    /// </summary>
    public async Task<ClaudeAnalysisResult> AnalyzeAsync(
        List<MessageItem> messages,
        string? labelSearchText)
    {
        try
        {
            var conversationText = FormatConversation(messages);
            var availableLabels = ParseLabels(labelSearchText);
            var prompt = BuildPrompt(conversationText, availableLabels);

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
                return ClaudeAnalysisResult.Fail(
                    ErrorCodes.ChatAnalysisClaudeError,
                    $"Claude returned {(int)response.StatusCode}: {errorBody}");
            }

            var claudeResponse = await response.Content.ReadFromJsonAsync<ClaudeResponse>(_jsonOptions);

            if (claudeResponse?.Content == null || claudeResponse.Content.Count == 0)
            {
                return ClaudeAnalysisResult.Fail(
                    ErrorCodes.ChatAnalysisClaudeError,
                    "Claude returned empty response");
            }

            var analysisText = claudeResponse.Content[0].Text ?? "";
            return ParseAnalysis(analysisText, availableLabels);
        }
        catch (TaskCanceledException)
        {
            return ClaudeAnalysisResult.Fail(
                ErrorCodes.ChatAnalysisClaudeTimeout,
                $"Claude timeout after {TimeoutMs}ms");
        }
        catch (HttpRequestException ex)
        {
            return ClaudeAnalysisResult.Fail(
                ErrorCodes.ChatAnalysisClaudeError,
                $"Claude connection error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return ClaudeAnalysisResult.Fail(
                ErrorCodes.ChatAnalysisClaudeError,
                $"Claude invalid JSON response: {ex.Message}");
        }
    }

    private static string FormatConversation(List<MessageItem> messages)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages.Take(100)) // Limit to 100 messages
        {
            var sender = msg.Source.ToUpperInvariant() == "AGENT" ? "Temsilci" : "Müşteri";
            sb.AppendLine($"{sender}: {msg.Message}");
        }
        return sb.ToString();
    }

    private static List<string> ParseLabels(string? labelSearchText)
    {
        if (string.IsNullOrWhiteSpace(labelSearchText))
            return new List<string>();

        return labelSearchText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string BuildPrompt(string conversation, List<string> availableLabels)
    {
        var labelsSection = availableLabels.Count > 0
            ? $"Mevcut etiketler: {string.Join(", ", availableLabels)}"
            : "Mevcut etiket listesi yok";

        return $@"Sana bir müşteri-temsilci konuşması ve etiket listesi veriyorum. Bu konuşmayı analiz et ve JSON formatında cevap ver.

{labelsSection}

Konuşma:
{conversation}

---

Aşağıdaki 15 kriteri analiz et. Her kriter için:
- ""Summary"": 1-2 kelimelik özet
- ""Details"": En az 2 cümle açıklama

PurchaseProbability için ek olarak:
- ""Percentage"": 0-100 arası sayı
- ""Color"": ""red"" (0-50) veya ""green"" (51-100)

Ayrıca:
- ""SelectedLabels"": Mevcut etiketlerden sohbetle ilgili olanları seç (array)
- ""SuggestedLabels"": Listede olmayan 2 yeni etiket öner (array)

Kriterler:
1. Content: Müşterinin hangi hizmet veya ürünle ilgilendiği
2. Attitude: Müşterinin tutumu (olumlu, nötr, negatif)
3. ApproachRecommendation: Temsilciye yaklaşım önerisi
4. PurchaseProbability: Satın alma olasılığı (% ve renk)
5. Needs: Müşterinin açık/örtük ihtiyaçları
6. DecisionProcess: Karar alma hızı ve karşılaştırma eğilimi
7. SalesBarriers: Satın almayı engelleyen faktörler
8. CommunicationStyle: Müşterinin üslubu ve tercih ettiği iletişim şekli
9. CustomerProfile: Demografik/psikografik profil ve hizmet önerisi
10. SatisfactionAndFeedback: Memnuniyet değerlendirme planı
11. OfferAndConversionRate: Teklif tepkisi ve dönüşüm analizi
12. SupportStrategy: Uzun vadeli destek stratejisi
13. CompetitorAnalysis: Rekabet avantajlarını vurgulama önerisi
14. BehaviorPatterns: Satın alma davranış desenleri
15. RepresentativeResponseSuggestion: Temsilci için ideal cevap önerisi (1 cümle, açık, net, müşteri profiline uygun)

SADECE JSON formatında cevap ver, başka metin ekleme:
{{
  ""SelectedLabels"": [""etiket1"", ""etiket2""],
  ""SuggestedLabels"": [""yeni1"", ""yeni2""],
  ""Content"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""Attitude"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""ApproachRecommendation"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""PurchaseProbability"": {{""Summary"": ""..."", ""Details"": ""..."", ""Percentage"": 0, ""Color"": ""red""}},
  ""Needs"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""DecisionProcess"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""SalesBarriers"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""CommunicationStyle"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""CustomerProfile"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""SatisfactionAndFeedback"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""OfferAndConversionRate"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""SupportStrategy"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""CompetitorAnalysis"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""BehaviorPatterns"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""RepresentativeResponseSuggestion"": {{""Summary"": ""..."", ""Details"": ""...""}}
}}";
    }

    private ClaudeAnalysisResult ParseAnalysis(string text, List<string> availableLabels)
    {
        // Extract JSON from response
        var jsonStart = text.IndexOf('{');
        var jsonEnd = text.LastIndexOf('}');

        if (jsonStart < 0 || jsonEnd <= jsonStart)
        {
            return ClaudeAnalysisResult.Fail(
                ErrorCodes.ChatAnalysisClaudeError,
                $"No valid JSON found in response: {Truncate(text, 100)}");
        }

        try
        {
            var json = text.Substring(jsonStart, jsonEnd - jsonStart + 1);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Parse labels - if no available labels provided, SelectedLabels stays empty (only suggestions)
            var selectedLabels = availableLabels.Count == 0
                ? new List<string>()
                : ParseStringArray(root, "SelectedLabels")
                    .Where(l => availableLabels.Contains(l, StringComparer.OrdinalIgnoreCase))
                    .ToList();

            var suggestedLabels = ParseStringArray(root, "SuggestedLabels")
                .Take(2)
                .ToList();

            // Parse criteria
            var result = new FullAnalysisResult
            {
                SelectedLabels = selectedLabels,
                SuggestedLabels = suggestedLabels,
                Content = ParseCriterion(root, "Content"),
                Attitude = ParseCriterion(root, "Attitude"),
                ApproachRecommendation = ParseCriterion(root, "ApproachRecommendation"),
                PurchaseProbability = ParsePurchaseProbability(root),
                Needs = ParseCriterion(root, "Needs"),
                DecisionProcess = ParseCriterion(root, "DecisionProcess"),
                SalesBarriers = ParseCriterion(root, "SalesBarriers"),
                CommunicationStyle = ParseCriterion(root, "CommunicationStyle"),
                CustomerProfile = ParseCriterion(root, "CustomerProfile"),
                SatisfactionAndFeedback = ParseCriterion(root, "SatisfactionAndFeedback"),
                OfferAndConversionRate = ParseCriterion(root, "OfferAndConversionRate"),
                SupportStrategy = ParseCriterion(root, "SupportStrategy"),
                CompetitorAnalysis = ParseCriterion(root, "CompetitorAnalysis"),
                BehaviorPatterns = ParseCriterion(root, "BehaviorPatterns"),
                RepresentativeResponseSuggestion = ParseCriterion(root, "RepresentativeResponseSuggestion")
            };

            return ClaudeAnalysisResult.Success(result);
        }
        catch (JsonException ex)
        {
            return ClaudeAnalysisResult.Fail(
                ErrorCodes.ChatAnalysisClaudeError,
                $"JSON parse error: {ex.Message}");
        }
    }

    private static List<string> ParseStringArray(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            return prop.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
        return new List<string>();
    }

    private static AnalysisCriterion ParseCriterion(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Object)
        {
            var summary = prop.TryGetProperty("Summary", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() ?? "-"
                : "-";
            var details = prop.TryGetProperty("Details", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString() ?? "Analiz yapılamadı."
                : "Analiz yapılamadı.";

            return new AnalysisCriterion { Summary = summary, Details = details };
        }
        return AnalysisCriterion.Empty;
    }

    private static PurchaseProbabilityCriterion ParsePurchaseProbability(JsonElement root)
    {
        if (root.TryGetProperty("PurchaseProbability", out var prop) && prop.ValueKind == JsonValueKind.Object)
        {
            var summary = prop.TryGetProperty("Summary", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() ?? "-"
                : "-";
            var details = prop.TryGetProperty("Details", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString() ?? "Analiz yapılamadı."
                : "Analiz yapılamadı.";
            var percentage = prop.TryGetProperty("Percentage", out var p) && p.ValueKind == JsonValueKind.Number
                ? Math.Clamp(p.GetInt32(), 0, 100)
                : 0;
            var color = prop.TryGetProperty("Color", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()?.ToLowerInvariant() ?? "red"
                : "red";

            // Validate color based on percentage
            color = percentage > 50 ? "green" : "red";

            return new PurchaseProbabilityCriterion
            {
                Summary = summary,
                Details = details,
                Percentage = percentage,
                Color = color
            };
        }
        return PurchaseProbabilityCriterion.Empty;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// Full analysis result from Claude
/// </summary>
public sealed class FullAnalysisResult
{
    public required List<string> SelectedLabels { get; init; }
    public required List<string> SuggestedLabels { get; init; }
    public required AnalysisCriterion Content { get; init; }
    public required AnalysisCriterion Attitude { get; init; }
    public required AnalysisCriterion ApproachRecommendation { get; init; }
    public required PurchaseProbabilityCriterion PurchaseProbability { get; init; }
    public required AnalysisCriterion Needs { get; init; }
    public required AnalysisCriterion DecisionProcess { get; init; }
    public required AnalysisCriterion SalesBarriers { get; init; }
    public required AnalysisCriterion CommunicationStyle { get; init; }
    public required AnalysisCriterion CustomerProfile { get; init; }
    public required AnalysisCriterion SatisfactionAndFeedback { get; init; }
    public required AnalysisCriterion OfferAndConversionRate { get; init; }
    public required AnalysisCriterion SupportStrategy { get; init; }
    public required AnalysisCriterion CompetitorAnalysis { get; init; }
    public required AnalysisCriterion BehaviorPatterns { get; init; }
    public required AnalysisCriterion RepresentativeResponseSuggestion { get; init; }
}

/// <summary>
/// Result wrapper for Claude analysis operations
/// </summary>
public sealed class ClaudeAnalysisResult
{
    public bool IsSuccess { get; private init; }
    public FullAnalysisResult? Data { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    private ClaudeAnalysisResult() { }

    public static ClaudeAnalysisResult Success(FullAnalysisResult data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static ClaudeAnalysisResult Fail(string errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
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
