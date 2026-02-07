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
    private const string Model = "claude-3-5-haiku-20241022";
    private const int MaxTokens = 8192; // claude-3-5-haiku max output token limit
    private const int ParallelMaxTokens = 4096; // per-call limit for parallel mode (5-7 criteria per call)
    private const int TimeoutMs = 120000; // 120 second timeout

    /// <summary>
    /// Supported output languages. Unsupported languages fall back to "tr".
    /// </summary>
    public static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase) { "tr", "en" };

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
    /// <param name="messages">List of conversation messages</param>
    /// <param name="labelSearchText">Comma-separated available labels</param>
    /// <param name="lang">Output language: "tr" (default), "en". Unsupported values fall back to "tr".</param>
    /// <returns>Analysis result with language used in ActualLanguage property</returns>
    public async Task<ClaudeAnalysisResult> AnalyzeAsync(
        List<MessageItem> messages,
        string? labelSearchText,
        string lang = "tr")
    {
        try
        {
            // Normalize and validate language - fallback to Turkish for unsupported
            var requestedLang = string.IsNullOrWhiteSpace(lang) ? "tr" : lang.ToLowerInvariant();
            var language = SupportedLanguages.Contains(requestedLang) ? requestedLang : "tr";

            var conversationText = FormatConversation(messages, language);
            var availableLabels = ParseLabels(labelSearchText);
            var prompt = BuildPrompt(conversationText, availableLabels, language);

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

    /// <summary>
    /// Analyze chat messages using 3 parallel API calls for ~3x faster results.
    /// Splits 15 criteria into 3 groups and runs concurrently.
    /// Same output format as AnalyzeAsync.
    /// </summary>
    public async Task<ClaudeAnalysisResult> AnalyzeParallelAsync(
        List<MessageItem> messages,
        string? labelSearchText,
        string lang = "tr")
    {
        try
        {
            var requestedLang = string.IsNullOrWhiteSpace(lang) ? "tr" : lang.ToLowerInvariant();
            var language = SupportedLanguages.Contains(requestedLang) ? requestedLang : "tr";

            var conversationText = FormatConversation(messages, language);
            var availableLabels = ParseLabels(labelSearchText);

            // 3 parallel API calls, each with a subset of criteria
            var task1 = CallClaudeAsync(BuildParallelPromptLabelsCore(conversationText, availableLabels, language));
            var task2 = CallClaudeAsync(BuildParallelPromptSales(conversationText, language));
            var task3 = CallClaudeAsync(BuildParallelPromptStrategy(conversationText, language));

            await Task.WhenAll(task1, task2, task3);

            var r1 = task1.Result;
            var r2 = task2.Result;
            var r3 = task3.Result;

            if (r1.Error != null)
                return ClaudeAnalysisResult.Fail(ErrorCodes.ChatAnalysisClaudeError, $"Parallel call 1 (labels+core): {r1.Error}");
            if (r2.Error != null)
                return ClaudeAnalysisResult.Fail(ErrorCodes.ChatAnalysisClaudeError, $"Parallel call 2 (sales): {r2.Error}");
            if (r3.Error != null)
                return ClaudeAnalysisResult.Fail(ErrorCodes.ChatAnalysisClaudeError, $"Parallel call 3 (strategy): {r3.Error}");

            return MergeParallelResults(r1.Json!, r2.Json!, r3.Json!, availableLabels);
        }
        catch (TaskCanceledException)
        {
            return ClaudeAnalysisResult.Fail(
                ErrorCodes.ChatAnalysisClaudeTimeout,
                $"Claude parallel timeout after {TimeoutMs}ms");
        }
        catch (HttpRequestException ex)
        {
            return ClaudeAnalysisResult.Fail(
                ErrorCodes.ChatAnalysisClaudeError,
                $"Claude connection error: {ex.Message}");
        }
    }

    private async Task<(string? Json, string? Error)> CallClaudeAsync(string prompt)
    {
        try
        {
            var request = new ClaudeRequest
            {
                Model = Model,
                MaxTokens = ParallelMaxTokens,
                Messages = [new ClaudeMessage { Role = "user", Content = prompt }]
            };

            var response = await _httpClient.PostAsJsonAsync(ApiUrl, request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return (null, $"Claude returned {(int)response.StatusCode}: {errorBody}");
            }

            var claudeResponse = await response.Content.ReadFromJsonAsync<ClaudeResponse>(_jsonOptions);

            if (claudeResponse?.Content == null || claudeResponse.Content.Count == 0)
                return (null, "Claude returned empty response");

            var text = claudeResponse.Content[0].Text ?? "";
            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd <= jsonStart)
                return (null, $"No valid JSON found: {Truncate(text, 100)}");

            return (text.Substring(jsonStart, jsonEnd - jsonStart + 1), null);
        }
        catch (TaskCanceledException)
        {
            return (null, $"Timeout after {TimeoutMs}ms");
        }
        catch (HttpRequestException ex)
        {
            return (null, $"Connection error: {ex.Message}");
        }
    }

    #region Parallel Prompt Builders

    private static string BuildParallelPromptLabelsCore(string conversation, List<string> availableLabels, string language)
    {
        if (language == "en")
        {
            var labels = availableLabels.Count > 0
                ? $"Available labels: {string.Join(", ", availableLabels)}"
                : "No label list provided";

            return $@"Analyze this customer-agent conversation. Respond ONLY in JSON.

{labels}

Conversation:
{conversation}

---

For each criterion: ""Summary"" (1-2 words), ""Details"" (at least 2 sentences).
PurchaseProbability: also ""Percentage"" (0-100), ""Color"" (""red"" if 0-50, ""green"" if 51-100).

Criteria:
1. Content: Which service/product the customer is interested in
2. Attitude: Customer's attitude (positive, neutral, negative)
3. ApproachRecommendation: Approach suggestion for the agent
4. PurchaseProbability: Purchase probability with percentage
5. Needs: Customer's explicit/implicit needs

Also select relevant labels from the list (SelectedLabels) and suggest 2 new ones (SuggestedLabels).

{{
  ""SelectedLabels"": [""label1""],
  ""SuggestedLabels"": [""new1"", ""new2""],
  ""Content"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""Attitude"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""ApproachRecommendation"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""PurchaseProbability"": {{""Summary"": ""..."", ""Details"": ""..."", ""Percentage"": 0, ""Color"": ""red""}},
  ""Needs"": {{""Summary"": ""..."", ""Details"": ""...""}}
}}";
        }

        var labelsTr = availableLabels.Count > 0
            ? $"Mevcut etiketler: {string.Join(", ", availableLabels)}"
            : "Mevcut etiket listesi yok";

        return $@"Müşteri-temsilci konuşmasını analiz et. SADECE JSON formatında cevap ver.

{labelsTr}

Konuşma:
{conversation}

---

Her kriter için: ""Summary"" (1-2 kelime), ""Details"" (en az 2 cümle).
PurchaseProbability için ek: ""Percentage"" (0-100), ""Color"" (""red"" 0-50, ""green"" 51-100).

Kriterler:
1. Content: Müşterinin ilgilendiği hizmet/ürün
2. Attitude: Müşteri tutumu (olumlu, nötr, negatif)
3. ApproachRecommendation: Temsilciye yaklaşım önerisi
4. PurchaseProbability: Satın alma olasılığı (% ve renk)
5. Needs: Müşterinin açık/örtük ihtiyaçları

Ayrıca mevcut etiketlerden ilgili olanları seç (SelectedLabels) ve 2 yeni etiket öner (SuggestedLabels).

{{
  ""SelectedLabels"": [""etiket1""],
  ""SuggestedLabels"": [""yeni1"", ""yeni2""],
  ""Content"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""Attitude"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""ApproachRecommendation"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""PurchaseProbability"": {{""Summary"": ""..."", ""Details"": ""..."", ""Percentage"": 0, ""Color"": ""red""}},
  ""Needs"": {{""Summary"": ""..."", ""Details"": ""...""}}
}}";
    }

    private static string BuildParallelPromptSales(string conversation, string language)
    {
        if (language == "en")
        {
            return $@"Analyze this customer-agent conversation. Respond ONLY in JSON.

Conversation:
{conversation}

---

For each criterion: ""Summary"" (1-2 words), ""Details"" (at least 2 sentences).

Criteria:
1. DecisionProcess: Decision-making speed and comparison tendency
2. SalesBarriers: Factors preventing purchase
3. CommunicationStyle: Customer's tone and preferred communication style
4. CustomerProfile: Demographic/psychographic profile and service suggestion
5. SatisfactionAndFeedback: Satisfaction evaluation plan

{{
  ""DecisionProcess"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""SalesBarriers"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""CommunicationStyle"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""CustomerProfile"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""SatisfactionAndFeedback"": {{""Summary"": ""..."", ""Details"": ""...""}}
}}";
        }

        return $@"Müşteri-temsilci konuşmasını analiz et. SADECE JSON formatında cevap ver.

Konuşma:
{conversation}

---

Her kriter için: ""Summary"" (1-2 kelime), ""Details"" (en az 2 cümle).

Kriterler:
1. DecisionProcess: Karar alma hızı ve karşılaştırma eğilimi
2. SalesBarriers: Satın almayı engelleyen faktörler
3. CommunicationStyle: Müşterinin üslubu ve tercih ettiği iletişim şekli
4. CustomerProfile: Demografik/psikografik profil ve hizmet önerisi
5. SatisfactionAndFeedback: Memnuniyet değerlendirme planı

{{
  ""DecisionProcess"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""SalesBarriers"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""CommunicationStyle"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""CustomerProfile"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""SatisfactionAndFeedback"": {{""Summary"": ""..."", ""Details"": ""...""}}
}}";
    }

    private static string BuildParallelPromptStrategy(string conversation, string language)
    {
        if (language == "en")
        {
            return $@"Analyze this customer-agent conversation. Respond ONLY in JSON.

Conversation:
{conversation}

---

For each criterion: ""Summary"" (1-2 words), ""Details"" (at least 2 sentences).
RepresentativeResponseSuggestion: Ideal response for the agent (1 clear sentence).

Criteria:
1. OfferAndConversionRate: Offer response and conversion analysis
2. SupportStrategy: Long-term support strategy
3. CompetitorAnalysis: Suggestion for highlighting competitive advantages
4. BehaviorPatterns: Purchase behavior patterns
5. RepresentativeResponseSuggestion: Ideal response suggestion for agent

{{
  ""OfferAndConversionRate"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""SupportStrategy"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""CompetitorAnalysis"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""BehaviorPatterns"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""RepresentativeResponseSuggestion"": {{""Summary"": ""..."", ""Details"": ""...""}}
}}";
        }

        return $@"Müşteri-temsilci konuşmasını analiz et. SADECE JSON formatında cevap ver.

Konuşma:
{conversation}

---

Her kriter için: ""Summary"" (1-2 kelime), ""Details"" (en az 2 cümle).
RepresentativeResponseSuggestion: Temsilci için ideal cevap önerisi (1 cümle, açık, net).

Kriterler:
1. OfferAndConversionRate: Teklif tepkisi ve dönüşüm analizi
2. SupportStrategy: Uzun vadeli destek stratejisi
3. CompetitorAnalysis: Rekabet avantajlarını vurgulama önerisi
4. BehaviorPatterns: Satın alma davranış desenleri
5. RepresentativeResponseSuggestion: Temsilci için ideal cevap önerisi

{{
  ""OfferAndConversionRate"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""SupportStrategy"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""CompetitorAnalysis"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""BehaviorPatterns"": {{""Summary"": ""..."", ""Details"": ""...""}},
  ""RepresentativeResponseSuggestion"": {{""Summary"": ""..."", ""Details"": ""...""}}
}}";
    }

    #endregion

    private ClaudeAnalysisResult MergeParallelResults(
        string json1, string json2, string json3, List<string> availableLabels)
    {
        try
        {
            using var doc1 = JsonDocument.Parse(json1);
            using var doc2 = JsonDocument.Parse(json2);
            using var doc3 = JsonDocument.Parse(json3);

            var r1 = doc1.RootElement;
            var r2 = doc2.RootElement;
            var r3 = doc3.RootElement;

            var selectedLabels = availableLabels.Count == 0
                ? new List<string>()
                : ParseStringArray(r1, "SelectedLabels")
                    .Where(l => availableLabels.Contains(l, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            var suggestedLabels = ParseStringArray(r1, "SuggestedLabels").Take(2).ToList();

            var result = new FullAnalysisResult
            {
                SelectedLabels = selectedLabels,
                SuggestedLabels = suggestedLabels,
                // Call 1: Labels + Core
                Content = ParseCriterion(r1, "Content"),
                Attitude = ParseCriterion(r1, "Attitude"),
                ApproachRecommendation = ParseCriterion(r1, "ApproachRecommendation"),
                PurchaseProbability = ParsePurchaseProbability(r1),
                Needs = ParseCriterion(r1, "Needs"),
                // Call 2: Sales
                DecisionProcess = ParseCriterion(r2, "DecisionProcess"),
                SalesBarriers = ParseCriterion(r2, "SalesBarriers"),
                CommunicationStyle = ParseCriterion(r2, "CommunicationStyle"),
                CustomerProfile = ParseCriterion(r2, "CustomerProfile"),
                SatisfactionAndFeedback = ParseCriterion(r2, "SatisfactionAndFeedback"),
                // Call 3: Strategy
                OfferAndConversionRate = ParseCriterion(r3, "OfferAndConversionRate"),
                SupportStrategy = ParseCriterion(r3, "SupportStrategy"),
                CompetitorAnalysis = ParseCriterion(r3, "CompetitorAnalysis"),
                BehaviorPatterns = ParseCriterion(r3, "BehaviorPatterns"),
                RepresentativeResponseSuggestion = ParseCriterion(r3, "RepresentativeResponseSuggestion")
            };

            return ClaudeAnalysisResult.Success(result);
        }
        catch (JsonException ex)
        {
            return ClaudeAnalysisResult.Fail(
                ErrorCodes.ChatAnalysisClaudeError,
                $"JSON parse error in parallel merge: {ex.Message}");
        }
    }

    private static string FormatConversation(List<MessageItem> messages, string language)
    {
        var sb = new StringBuilder();
        var (agentLabel, customerLabel) = language == "en"
            ? ("Agent", "Customer")
            : ("Temsilci", "Müşteri");

        foreach (var msg in messages.Take(100)) // Limit to 100 messages
        {
            var sender = msg.Source.ToUpperInvariant() == "AGENT" ? agentLabel : customerLabel;
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

    private static string BuildPrompt(string conversation, List<string> availableLabels, string language)
    {
        return language == "en"
            ? BuildEnglishPrompt(conversation, availableLabels)
            : BuildTurkishPrompt(conversation, availableLabels);
    }

    private static string BuildTurkishPrompt(string conversation, List<string> availableLabels)
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

    private static string BuildEnglishPrompt(string conversation, List<string> availableLabels)
    {
        var labelsSection = availableLabels.Count > 0
            ? $"Available labels: {string.Join(", ", availableLabels)}"
            : "No label list provided";

        return $@"I'm giving you a customer-agent conversation and a label list. Analyze this conversation and respond in JSON format.

{labelsSection}

Conversation:
{conversation}

---

Analyze the following 15 criteria. For each criterion:
- ""Summary"": 1-2 word summary
- ""Details"": At least 2 sentences explanation

For PurchaseProbability additionally:
- ""Percentage"": Number between 0-100
- ""Color"": ""red"" (0-50) or ""green"" (51-100)

Also:
- ""SelectedLabels"": Select labels from available list that relate to the conversation (array)
- ""SuggestedLabels"": Suggest 2 new labels not in the list (array)

Criteria:
1. Content: Which service or product the customer is interested in
2. Attitude: Customer's attitude (positive, neutral, negative)
3. ApproachRecommendation: Approach suggestion for the agent
4. PurchaseProbability: Purchase probability (% and color)
5. Needs: Customer's explicit/implicit needs
6. DecisionProcess: Decision-making speed and comparison tendency
7. SalesBarriers: Factors preventing purchase
8. CommunicationStyle: Customer's tone and preferred communication style
9. CustomerProfile: Demographic/psychographic profile and service suggestion
10. SatisfactionAndFeedback: Satisfaction evaluation plan
11. OfferAndConversionRate: Offer response and conversion analysis
12. SupportStrategy: Long-term support strategy
13. CompetitorAnalysis: Suggestion for highlighting competitive advantages
14. BehaviorPatterns: Purchase behavior patterns
15. RepresentativeResponseSuggestion: Ideal response suggestion for agent (1 sentence, clear, concise, appropriate for customer profile)

Respond ONLY in JSON format, do not add any other text:
{{
  ""SelectedLabels"": [""label1"", ""label2""],
  ""SuggestedLabels"": [""new1"", ""new2""],
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
