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
/// Claude Haiku-powered translation service with DB cache.
/// Handles single + batch translate, language detection.
/// </summary>
public sealed class TranslationService
{
    private readonly HttpClient _httpClient;
    private readonly TranslationCacheRepository _cache;
    private readonly JsonLinesLogger _logger;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _timeoutSeconds;

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
        _apiKey = config["Claude:ApiKey"] ?? "";
        _model = config["Claude:TranslationModel"] ?? "claude-haiku-4-5-20251001";
        _timeoutSeconds = int.TryParse(config["Claude:TranslationTimeoutSeconds"], out var ts) ? ts : 30;
    }

    /// <summary>
    /// Translate a single message. Uses cache first, falls back to Claude API.
    /// </summary>
    public async Task<TranslateResponse> TranslateAsync(int tenantId, TranslateRequest request, CancellationToken ct = default)
    {
        var targetLang = LanguageDetector.Normalize(request.TargetLanguage)
            ?? throw new ArgumentException(ErrorCodes.BackendTranslationUnsupportedLang);

        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException(ErrorCodes.BackendTranslationInvalidText);

        if (!SupportedLanguageCodes.Contains(targetLang))
            throw new ArgumentException(ErrorCodes.BackendTranslationUnsupportedLang);

        var sourceHash = TranslationCacheRepository.ComputeHash(request.Text);

        // Check cache (degrade gracefully on DB failure)
        try
        {
            var cached = await _cache.GetAsync(tenantId, sourceHash, targetLang, ct);
            if (cached != null)
            {
                return new TranslateResponse
                {
                    TranslatedText = cached.TranslatedText,
                    SourceLanguage = cached.SourceLanguage ?? LanguageDetector.Detect(request.Text),
                    TargetLanguage = targetLang,
                    FromCache = true
                };
            }
        }
        catch (NpgsqlException ex)
        {
            _logger.StepError($"[{ErrorCodes.BackendTranslationCacheError}] Cache read error: {ex.Message}", "-");
        }

        // Call Claude API
        var sourceLang = LanguageDetector.Normalize(request.SourceLanguage) ?? LanguageDetector.Detect(request.Text);
        var translatedText = await CallClaudeTranslateAsync(request.Text, sourceLang, targetLang, ct);

        // Save to cache (fire-and-forget, degrade on DB failure)
        _ = Task.Run(async () =>
        {
            try { await _cache.SaveAsync(tenantId, sourceHash, request.Text, sourceLang, targetLang, translatedText); }
            catch (NpgsqlException ex) { _logger.StepError($"[{ErrorCodes.BackendTranslationCacheError}] Cache write error: {ex.Message}", "-"); }
        }, CancellationToken.None);

        return new TranslateResponse
        {
            TranslatedText = translatedText,
            SourceLanguage = sourceLang,
            TargetLanguage = targetLang,
            FromCache = false
        };
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
                var sourceLang = LanguageDetector.Normalize(item.SourceLanguage) ?? LanguageDetector.Detect(item.Text);
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

        // Quick heuristic first
        var heuristicLang = LanguageDetector.Detect(text);

        // For short texts or when heuristic is confident Turkish, skip Claude
        if (text.Length < 20)
        {
            var langInfo = SupportedLanguages.FirstOrDefault(l => l.Code == heuristicLang);
            return new DetectLanguageResponse
            {
                Language = heuristicLang,
                LanguageName = langInfo?.Name ?? heuristicLang,
                Confidence = "low"
            };
        }

        try
        {
            var prompt = $"What language is the following text written in? Reply with ONLY the ISO 639-1 two-letter code (e.g., 'tr', 'en', 'ar', 'ru'). Nothing else.\n\nText: {text}";
            var response = await CallClaudeRawAsync(prompt, 10, ct);
            var detectedLang = response.Trim().ToLowerInvariant();

            // Validate the response is a 2-letter code
            if (detectedLang.Length == 2 && detectedLang.All(char.IsLetter))
            {
                var langInfo = SupportedLanguages.FirstOrDefault(l => l.Code == detectedLang);
                return new DetectLanguageResponse
                {
                    Language = detectedLang,
                    LanguageName = langInfo?.Name ?? detectedLang,
                    Confidence = "high"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.StepError($"[{ErrorCodes.BackendTranslationDetectFailed}] Claude detect HTTP error, using heuristic: {ex.Message}", "-");
        }
        catch (InvalidOperationException ex)
        {
            _logger.StepError($"[{ErrorCodes.BackendTranslationDetectFailed}] Claude detect empty response, using heuristic: {ex.Message}", "-");
        }
        catch (OperationCanceledException)
        {
            _logger.StepError($"[{ErrorCodes.BackendTranslationDetectFailed}] Claude detect timed out, using heuristic", "-");
        }

        // Fallback to heuristic
        var fallbackInfo = SupportedLanguages.FirstOrDefault(l => l.Code == heuristicLang);
        return new DetectLanguageResponse
        {
            Language = heuristicLang,
            LanguageName = fallbackInfo?.Name ?? heuristicLang,
            Confidence = "medium"
        };
    }

    /// <summary>
    /// Call Claude to translate text.
    /// </summary>
    private async Task<string> CallClaudeTranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct)
    {
        var targetName = SupportedLanguages.FirstOrDefault(l => l.Code == targetLang)?.Name ?? targetLang;
        var sourceName = SupportedLanguages.FirstOrDefault(l => l.Code == sourceLang)?.Name ?? sourceLang;

        var prompt = $"Translate the following text from {sourceName} to {targetName}. Return ONLY the translated text, nothing else. Do not add explanations, quotes, or formatting.\n\nText: {text}";
        return await CallClaudeRawAsync(prompt, 2048, ct);
    }

    /// <summary>
    /// Low-level Claude API call. Returns the text content of the first content block.
    /// </summary>
    private async Task<string> CallClaudeRawAsync(string userMessage, int maxTokens, CancellationToken ct)
    {
        var requestBody = new
        {
            model = _model,
            max_tokens = maxTokens,
            messages = new[] { new { role = "user", content = userMessage } }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ClaudeApiUrl);
        httpRequest.Headers.Add("x-api-key", _apiKey);
        httpRequest.Headers.Add("anthropic-version", AnthropicVersion);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

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
