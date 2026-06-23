using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Chatinbox.Marketing.Data;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Marketing.Services;

/// <summary>
/// Generates multilingual responses for medical tourism inquiries using Claude Haiku.
/// GR-3.25: Treatment catalog context injection, multilingual response + Turkish translation.
/// Thread-safe, register as singleton via AddHttpClient typed client.
/// </summary>
public sealed class TourismResponseGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxTokens;
    private readonly int _timeoutMs;
    private readonly JsonLinesLogger _logger;

    private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";

    public TourismResponseGenerator(
        HttpClient httpClient, string apiKey, string model,
        int maxTokens, int timeoutSecs, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _maxTokens = maxTokens;
        _timeoutMs = timeoutSecs * 1000;
        _logger = logger;
    }

    /// <summary>
    /// Generate a multilingual response for a patient inquiry.
    /// Returns null on failure (graceful degradation — caller returns 503 INV-MK-023).
    /// </summary>
    public async Task<TourismResponseResult?> GenerateResponseAsync(
        int tenantId,
        string patientLang,
        string patientMessage,
        string? patientCountry,
        string? treatmentInterest,
        List<TreatmentDto> catalog,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeoutMs);

            var systemPrompt = BuildSystemPrompt(patientLang, patientCountry, catalog);
            var userPrompt = BuildUserPrompt(patientMessage, treatmentInterest);

            var requestBody = new
            {
                model = _model,
                max_tokens = _maxTokens,
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
                _logger.SystemWarn($"[TourismResponseGenerator] Claude API HTTP {(int)response.StatusCode}: {errorBody}");
                return null;
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonDocument>(cts.Token);
            if (responseJson == null)
            {
                _logger.SystemWarn("[TourismResponseGenerator] Claude returned null JSON body");
                return null;
            }

            var content = responseJson.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(content))
            {
                _logger.SystemWarn("[TourismResponseGenerator] Claude returned empty content text");
                return null;
            }

            var result = ParseResponse(content, patientLang);
            if (result != null)
                result.ProcessingTimeMs = sw.ElapsedMilliseconds;

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // App shutting down — propagate
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _logger.SystemWarn($"[TourismResponseGenerator] Claude timeout after {sw.ElapsedMilliseconds}ms (tenant={tenantId})");
            return null;
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.SystemWarn($"[TourismResponseGenerator] HTTP error after {sw.ElapsedMilliseconds}ms (tenant={tenantId}): {(int?)ex.StatusCode} {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            sw.Stop();
            _logger.SystemWarn($"[TourismResponseGenerator] JSON parse error after {sw.ElapsedMilliseconds}ms (tenant={tenantId}): {ex.Message}");
            return null;
        }
    }

    private static string BuildSystemPrompt(
        string patientLang, string? patientCountry, List<TreatmentDto> catalog)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a professional medical tourism assistant for a clinic in Turkey.");
        sb.AppendLine($"Respond in {patientLang} language. Be warm, professional, and culturally appropriate.");
        sb.AppendLine();

        // Treatment catalog context
        if (catalog.Count > 0)
        {
            sb.AppendLine("Available treatments:");
            foreach (var t in catalog)
            {
                var name = !string.IsNullOrEmpty(t.TreatmentNameEn) ? t.TreatmentNameEn : t.TreatmentName;
                sb.Append($"- {name}");

                if (t.PriceMin.HasValue && t.PriceMax.HasValue)
                    sb.Append($" | {t.PriceMin:F0}-{t.PriceMax:F0} {t.PriceCurrency}");
                else if (t.PriceMin.HasValue)
                    sb.Append($" | from {t.PriceMin:F0} {t.PriceCurrency}");

                if (t.DurationDays.HasValue)
                    sb.Append($" | {t.DurationDays}d treatment");

                if (t.RecoveryDays.HasValue)
                    sb.Append($" + {t.RecoveryDays}d recovery");

                if (!string.IsNullOrEmpty(t.PackageIncludes))
                    sb.Append($" | Includes: {t.PackageIncludes}");

                sb.AppendLine();
            }
            sb.AppendLine();
        }

        // Country-aware greeting hint
        if (!string.IsNullOrEmpty(patientCountry))
            sb.AppendLine($"Patient is from country code: {patientCountry}. Adapt cultural references accordingly.");

        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Give price RANGES with \"estimated\" disclaimer");
        sb.AppendLine("- Mention package inclusions if relevant");
        sb.AppendLine("- For medical specifics, say \"will be discussed during your personal consultation\"");
        sb.AppendLine("- End with a clear next step (video consultation, photo request, or booking)");
        sb.AppendLine("- Never give guarantees about outcomes");
        sb.AppendLine("- ONLY mention treatments listed above. Do NOT invent treatments or prices not in the catalog.");
        sb.AppendLine("- Do NOT include internal reasoning, thinking, or meta-commentary in your response.");
        sb.AppendLine();
        sb.AppendLine("OUTPUT: Return ONLY a JSON object (no other text):");
        sb.Append("{\"response\": \"<your response in patient language>\", \"tr_translation\": \"<Turkish translation for clinic staff>\", \"detected_intent\": \"<one of: treatment_inquiry, price_query, package_query, availability, photo_consultation, general>\"}");

        return sb.ToString();
    }

    private static string BuildUserPrompt(string patientMessage, string? treatmentInterest)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Patient message: {patientMessage}");

        if (!string.IsNullOrEmpty(treatmentInterest))
            sb.AppendLine($"Known treatment interest: {treatmentInterest}");

        sb.Append("Generate a helpful response.");
        return sb.ToString();
    }

    private TourismResponseResult? ParseResponse(string responseText, string expectedLang)
    {
        try
        {
            var json = responseText.Trim();

            // Strip markdown code fences if Claude wraps output
            if (json.StartsWith("```"))
            {
                var startIdx = json.IndexOf('{');
                var endIdx = json.LastIndexOf('}');
                if (startIdx >= 0 && endIdx > startIdx)
                    json = json[startIdx..(endIdx + 1)];
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var response = root.TryGetProperty("response", out var r) ? r.GetString() : null;
            var trTranslation = root.TryGetProperty("tr_translation", out var tr) ? tr.GetString() : null;
            var detectedIntent = root.TryGetProperty("detected_intent", out var di) ? di.GetString() : null;

            if (string.IsNullOrEmpty(response))
            {
                _logger.SystemWarn("[TourismResponseGenerator] Parsed JSON has empty response field");
                return null;
            }

            // Normalize "null" string to actual null
            if (trTranslation is "null" or "")
                trTranslation = null;
            if (detectedIntent is "null" or "")
                detectedIntent = null;

            return new TourismResponseResult
            {
                Response = response,
                ResponseLang = expectedLang,
                TrTranslation = trTranslation,
                DetectedIntent = detectedIntent
            };
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[TourismResponseGenerator] JSON parse error: {ex.Message}, raw={responseText}");
            return null;
        }
    }
}

/// <summary>
/// Result of Claude multilingual response generation.
/// </summary>
public sealed class TourismResponseResult
{
    public string Response { get; set; } = "";
    public string ResponseLang { get; set; } = "";
    public string? TrTranslation { get; set; }
    public string? DetectedIntent { get; set; }
    public long ProcessingTimeMs { get; set; }
}
