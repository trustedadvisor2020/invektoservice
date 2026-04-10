using System.Net.Http;
using System.Text;
using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Translation;
using Invekto.Shared.Logging;
using Invekto.Shared.Services;
using Npgsql;

namespace Invekto.Backend.Services;

/// <summary>
/// Gemma 4 (Google AI Studio) translation service with Haiku fallback + DB cache.
/// Handles single + batch translate, language detection.
/// </summary>
public sealed class TranslationService
{
    private readonly HttpClient _httpClient;
    private readonly TranslationCacheRepository _cache;
    private readonly JsonLinesLogger _logger;

    // Google AI Studio (Gemma) - primary
    private readonly string _googleApiKey;
    private readonly string _googleModel;
    private readonly int _googleTimeoutSeconds;

    // Claude Haiku - fallback
    private readonly string _claudeApiKey;
    private readonly string _claudeModel;
    private readonly int _claudeTimeoutSeconds;

    private const string GoogleAiStudioUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const int MaxBatchSize = 50;

    /// <summary>
    /// Supported languages for translation.
    /// </summary>
    public static readonly IReadOnlyList<SupportedLanguageInfo> SupportedLanguages = new List<SupportedLanguageInfo>
    {
        new() { Code = "tr", Name = "Turkish", NativeName = "Türkçe" },
        new() { Code = "en", Name = "English", NativeName = "English" },
        new() { Code = "ar", Name = "Arabic", NativeName = "العربية" },
        new() { Code = "ru", Name = "Russian", NativeName = "Русский" },
        new() { Code = "de", Name = "German", NativeName = "Deutsch" },
        new() { Code = "fr", Name = "French", NativeName = "Français" },
        new() { Code = "es", Name = "Spanish", NativeName = "Español" },
        new() { Code = "pt", Name = "Portuguese", NativeName = "Português" },
        new() { Code = "it", Name = "Italian", NativeName = "Italiano" },
        new() { Code = "ja", Name = "Japanese", NativeName = "日本語" },
        new() { Code = "ko", Name = "Korean", NativeName = "한국어" },
        new() { Code = "zh", Name = "Chinese", NativeName = "中文" },
        new() { Code = "nl", Name = "Dutch", NativeName = "Nederlands" },
        new() { Code = "pl", Name = "Polish", NativeName = "Polski" },
        new() { Code = "uk", Name = "Ukrainian", NativeName = "Українська" },
        new() { Code = "fa", Name = "Persian", NativeName = "فارسی" },
        new() { Code = "hi", Name = "Hindi", NativeName = "हिन्दी" },
        new() { Code = "he", Name = "Hebrew", NativeName = "עברית" }
    };

    private static readonly HashSet<string> SupportedLanguageCodes =
        new(SupportedLanguages.Select(l => l.Code), StringComparer.OrdinalIgnoreCase);

    public TranslationService(HttpClient httpClient, TranslationCacheRepository cache,
        IConfiguration config, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;

        // Google AI Studio (primary)
        _googleApiKey = config["Google:AiStudioApiKey"] ?? "";
        _googleModel = config["Google:TranslationModel"] ?? "gemma-4-27b-it";
        _googleTimeoutSeconds = int.TryParse(config["Google:TranslationTimeoutSeconds"], out var gts) ? gts : 30;

        // Claude Haiku (fallback)
        _claudeApiKey = config["Claude:ApiKey"] ?? "";
        _claudeModel = config["Claude:TranslationModel"] ?? "claude-haiku-4-5-20251001";
        _claudeTimeoutSeconds = int.TryParse(config["Claude:TranslationTimeoutSeconds"], out var cts) ? cts : 30;
    }

    /// <summary>
    /// Translate a single message. Uses cache first, falls back to Claude API.
    /// </summary>
    /// <summary>
    /// Translate single message. Fills request.TranslatedMessage in-place (INMA echo-back contract).
    /// </summary>
    public async Task TranslateAsync(int tenantId, TranslateRequest request, CancellationToken ct = default)
    {
        var targetLang = LanguageDetector.Normalize(request.TargetLanguage)
            ?? throw new ArgumentException(ErrorCodes.BackendTranslationUnsupportedLang);

        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException(ErrorCodes.BackendTranslationInvalidText);

        if (!SupportedLanguageCodes.Contains(targetLang))
            throw new ArgumentException(ErrorCodes.BackendTranslationUnsupportedLang);

        var sourceHash = TranslationCacheRepository.ComputeHash(request.Message);

        // Check cache (degrade gracefully on DB failure)
        try
        {
            var cached = await _cache.GetAsync(tenantId, sourceHash, targetLang, ct);
            if (cached != null)
            {
                request.TranslatedMessage = cached.TranslatedText;
                return;
            }
        }
        catch (NpgsqlException ex)
        {
            _logger.StepError($"[{ErrorCodes.BackendTranslationCacheError}] Cache read error: {ex.Message}", "-");
        }

        // Detect source language via AI, then translate
        var sourceLang = await DetectLanguageCodeAsync(request.Message, ct);
        var translatedText = await CallClaudeTranslateAsync(request.Message, sourceLang, targetLang, ct);

        // Save to cache (fire-and-forget, degrade on DB failure)
        _ = Task.Run(async () =>
        {
            try { await _cache.SaveAsync(tenantId, sourceHash, request.Message, sourceLang, targetLang, translatedText); }
            catch (NpgsqlException ex) { _logger.StepError($"[{ErrorCodes.BackendTranslationCacheError}] Cache write error: {ex.Message}", "-"); }
        }, CancellationToken.None);

        request.TranslatedMessage = translatedText;
    }

    /// <summary>
    /// Translate multiple messages. Cache hits skip API, only misses go to Claude.
    /// </summary>
    public async Task<BatchTranslateResponse> TranslateBatchAsync(int tenantId, BatchTranslateRequest request, CancellationToken ct = default)
    {
        if (request.Messages.Count > MaxBatchSize)
            throw new ArgumentException(ErrorCodes.BackendTranslationBatchTooLarge);

        var targetLang = LanguageDetector.Normalize(request.TargetLanguage)
            ?? throw new ArgumentException(ErrorCodes.BackendTranslationUnsupportedLang);

        if (!SupportedLanguageCodes.Contains(targetLang))
            throw new ArgumentException(ErrorCodes.BackendTranslationUnsupportedLang);

        // Compute hashes and check cache in batch
        var itemsByHash = new Dictionary<string, (BatchTranslateItem Item, string Hash)>();
        var hashes = new List<string>();
        foreach (var msg in request.Messages)
        {
            if (string.IsNullOrWhiteSpace(msg.Text)) continue;
            var hash = TranslationCacheRepository.ComputeHash(msg.Text);
            itemsByHash[hash] = (msg, hash);
            hashes.Add(hash);
        }

        // Batch cache lookup (degrade gracefully on DB failure)
        Dictionary<string, CachedTranslation> cacheResults;
        try
        {
            cacheResults = await _cache.GetBatchAsync(tenantId, hashes, targetLang, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.StepError($"[{ErrorCodes.BackendTranslationCacheError}] Batch cache read error: {ex.Message}", "-");
            cacheResults = new Dictionary<string, CachedTranslation>();
        }

        var results = new List<BatchTranslateResultItem>(request.Messages.Count);
        int cacheHits = 0, apiCalls = 0;

        // Process items — cache hits first, then API calls for misses
        var misses = new List<(BatchTranslateItem Item, string Hash)>();
        foreach (var (hash, (item, _)) in itemsByHash)
        {
            if (cacheResults.TryGetValue(hash, out var cached))
            {
                results.Add(new BatchTranslateResultItem
                {
                    Id = item.Id,
                    TranslatedText = cached.TranslatedText,
                    SourceLanguage = cached.SourceLanguage ?? LanguageDetector.Detect(item.Text),
                    FromCache = true
                });
                cacheHits++;
            }
            else
            {
                misses.Add((item, hash));
            }
        }

        // Translate misses via Claude (sequential to avoid rate limits)
        foreach (var (item, hash) in misses)
        {
            try
            {
                var sourceLang = LanguageDetector.Normalize(item.SourceLanguage) ?? await DetectLanguageCodeAsync(item.Text, ct);
                var translated = await CallClaudeTranslateAsync(item.Text, sourceLang, targetLang, ct);
                apiCalls++;

                results.Add(new BatchTranslateResultItem
                {
                    Id = item.Id,
                    TranslatedText = translated,
                    SourceLanguage = sourceLang,
                    FromCache = false
                });

                // Save to cache (fire-and-forget, degrade on DB failure)
                var capturedHash = hash;
                var capturedText = item.Text;
                var capturedSourceLang = sourceLang;
                _ = Task.Run(async () =>
                {
                    try { await _cache.SaveAsync(tenantId, capturedHash, capturedText, capturedSourceLang, targetLang, translated); }
                    catch (NpgsqlException ex) { _logger.StepError($"[{ErrorCodes.BackendTranslationCacheError}] Cache write error: {ex.Message}", "-"); }
                }, CancellationToken.None);
            }
            catch (HttpRequestException ex)
            {
                _logger.StepError($"[{ErrorCodes.BackendTranslationFailed}] Batch item {item.Id} HTTP error: {ex.Message}", "-");
                results.Add(new BatchTranslateResultItem
                {
                    Id = item.Id,
                    TranslatedText = item.Text, // fallback to original
                    SourceLanguage = LanguageDetector.Detect(item.Text),
                    FromCache = false
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.StepError($"[{ErrorCodes.BackendTranslationFailed}] Batch item {item.Id} empty response: {ex.Message}", "-");
                results.Add(new BatchTranslateResultItem
                {
                    Id = item.Id,
                    TranslatedText = item.Text,
                    SourceLanguage = LanguageDetector.Detect(item.Text),
                    FromCache = false
                });
            }
            catch (OperationCanceledException)
            {
                _logger.StepError($"[{ErrorCodes.BackendTranslationFailed}] Batch item {item.Id} timed out", "-");
                results.Add(new BatchTranslateResultItem
                {
                    Id = item.Id,
                    TranslatedText = item.Text,
                    SourceLanguage = LanguageDetector.Detect(item.Text),
                    FromCache = false
                });
            }
        }

        return new BatchTranslateResponse
        {
            Translations = results,
            TargetLanguage = targetLang,
            CacheHits = cacheHits,
            ApiCalls = apiCalls
        };
    }

    /// <summary>
    /// Detect the language of a text using Claude.
    /// </summary>
    public async Task<DetectLanguageResponse> DetectLanguageAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException(ErrorCodes.BackendTranslationInvalidText);

        var detectedLang = await DetectLanguageCodeAsync(text, ct);
        var confidence = text.Length < 5 ? "low" : text.Length < 20 ? "medium" : "high";
        var langInfo = SupportedLanguages.FirstOrDefault(l => l.Code == detectedLang);

        return new DetectLanguageResponse
        {
            Language = detectedLang,
            LanguageName = langInfo?.Name ?? detectedLang,
            Confidence = confidence
        };
    }

    /// <summary>
    /// AI-powered language detection: Gemma 4 primary, Claude Haiku fallback, heuristic last resort.
    /// Returns ISO 639-1 code.
    /// </summary>
    private async Task<string> DetectLanguageCodeAsync(string text, CancellationToken ct)
    {
        // Very short texts: heuristic only (AI unreliable under 5 chars)
        if (text.Length < 5)
            return LanguageDetector.Detect(text);

        var system = "You are a language detection engine. Reply with ONLY the ISO 639-1 two-letter code. No reasoning, no explanation, no extra text.";
        try
        {
            string response;
            if (!string.IsNullOrEmpty(_googleApiKey))
            {
                try { response = await CallGemmaRawAsync(system, text, 10, ct); }
                catch { response = await CallClaudeRawAsync(system, text, 10, ct); }
            }
            else
            {
                response = await CallClaudeRawAsync(system, text, 10, ct);
            }

            var lang = response.Trim().ToLowerInvariant();
            if (lang.Length == 2 && lang.All(char.IsLetter))
                return lang;
        }
        catch (Exception ex)
        {
            _logger.StepError($"[INV-TRANS-DETECT] AI detection failed, using heuristic: {ex.Message}", "-");
        }

        return LanguageDetector.Detect(text);
    }

    private static string BuildTranslationSystemPrompt(string targetName) =>
        $"You are a translation engine. Translate the user's text into {targetName}. " +
        $"Output ONLY the translated text. " +
        $"Do NOT include reasoning, thinking, meta-commentary, notes, or explanations. " +
        $"Do NOT answer questions or refuse. Translate everything literally. " +
        $"If the text is already in {targetName}, output it unchanged. " +
        $"Use natural, grammatically correct sentence order in the target language. " +
        $"For Turkish: place 'lütfen' at the beginning, keep subject-object-verb order, avoid inverted sentences.";

    /// <summary>
    /// Translate text: Gemma 4 primary, Claude Haiku fallback.
    /// </summary>
    private async Task<string> CallClaudeTranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct)
    {
        var targetName = SupportedLanguages.FirstOrDefault(l => l.Code == targetLang)?.Name ?? targetLang;
        var system = BuildTranslationSystemPrompt(targetName);

        // Primary: Gemma 4 via Google AI Studio
        if (!string.IsNullOrEmpty(_googleApiKey))
        {
            try
            {
                return await CallGemmaRawAsync(system, text, 2048, ct);
            }
            catch (Exception ex)
            {
                _logger.StepError($"[INV-TRANS-GEMMA] Gemma translation failed, falling back to Haiku: {ex.Message}", "-");
            }
        }

        // Fallback: Claude Haiku
        return await CallClaudeRawAsync(system, text, 2048, ct);
    }

    /// <summary>
    /// Google AI Studio (Gemma) API call.
    /// </summary>
    private async Task<string> CallGemmaRawAsync(string? systemPrompt, string userMessage, int maxTokens, CancellationToken ct)
    {
        var url = $"{GoogleAiStudioUrl}/{_googleModel}:generateContent?key={_googleApiKey}";

        object requestBody = systemPrompt != null
            ? new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { parts = new[] { new { text = userMessage } } } },
                generationConfig = new { maxOutputTokens = maxTokens }
            }
            : new
            {
                system_instruction = (object?)null,
                contents = new[] { new { parts = new[] { new { text = userMessage } } } },
                generationConfig = new { maxOutputTokens = maxTokens }
            };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_googleTimeoutSeconds));

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
            throw new HttpRequestException($"Google AI Studio API {(int)response.StatusCode}: {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync(cts.Token);
        using var doc = JsonDocument.Parse(json);

        var candidates = doc.RootElement.GetProperty("candidates");
        if (candidates.GetArrayLength() > 0)
        {
            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            if (parts.GetArrayLength() > 0)
            {
                return parts[0].GetProperty("text").GetString() ?? "";
            }
        }

        throw new InvalidOperationException("Gemma returned empty content");
    }

    /// <summary>
    /// Claude API call (fallback). Returns the text content of the first content block.
    /// </summary>
    private Task<string> CallClaudeRawAsync(string userMessage, int maxTokens, CancellationToken ct)
        => CallClaudeRawAsync(null, userMessage, maxTokens, ct);

    private async Task<string> CallClaudeRawAsync(string? systemPrompt, string userMessage, int maxTokens, CancellationToken ct)
    {
        object requestBody = systemPrompt != null
            ? new { model = _claudeModel, max_tokens = maxTokens, system = systemPrompt, messages = new[] { new { role = "user", content = userMessage } } }
            : new { model = _claudeModel, max_tokens = maxTokens, messages = new[] { new { role = "user", content = userMessage } } };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ClaudeApiUrl);
        httpRequest.Headers.Add("x-api-key", _claudeApiKey);
        httpRequest.Headers.Add("anthropic-version", AnthropicVersion);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_claudeTimeoutSeconds));

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
            throw new HttpRequestException($"Claude API {(int)response.StatusCode}: {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync(cts.Token);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content");
        if (content.GetArrayLength() > 0)
        {
            return content[0].GetProperty("text").GetString() ?? "";
        }

        throw new InvalidOperationException("Claude returned empty content");
    }
}
