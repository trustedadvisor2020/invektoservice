using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chatinbox.Backend.Services;

/// <summary>
/// AI Flow Wizard service: calls Claude API with streaming to help users
/// design chatbot flows via natural conversation.
/// </summary>
public sealed class ClaudeWizardService
{
    private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _fallbackModel;
    private readonly int _maxTokens;
    private readonly int _timeoutSeconds;
    private readonly ILogger<ClaudeWizardService> _logger;

    public ClaudeWizardService(HttpClient httpClient, IConfiguration config, ILogger<ClaudeWizardService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Claude:ApiKey"] ?? "";
        _model = config["Claude:WizardModel"] ?? "claude-sonnet-4-6";
        _fallbackModel = config["Claude:WizardFallbackModel"] ?? "claude-haiku-4-5-20251001";
        _maxTokens = int.TryParse(config["Claude:WizardMaxTokens"], out var mt) ? mt : 4096;
        _timeoutSeconds = int.TryParse(config["Claude:WizardTimeoutSeconds"], out var ts) ? ts : 60;
        _logger = logger;
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    /// <summary>
    /// Stream a chat response from Claude. Yields text chunks as they arrive.
    /// The final yielded item may contain a flow_config JSON block.
    /// </summary>
    public async IAsyncEnumerable<WizardStreamChunk> StreamChatAsync(
        string userMessage,
        List<WizardMessage> history,
        List<FlowSummaryContext>? existingFlows,
        string? currentFlowConfig = null,
        string? executionContext = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var systemPrompt = BuildSystemPrompt(existingFlows, currentFlowConfig, executionContext);

        var messages = new List<object>();
        foreach (var msg in history)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }
        messages.Add(new { role = "user", content = userMessage });

        // Try primary model, fall back on overload (529)
        var models = string.IsNullOrEmpty(_fallbackModel) || _fallbackModel == _model
            ? new[] { _model } : new[] { _model, _fallbackModel };

        for (int modelIdx = 0; modelIdx < models.Length; modelIdx++)
        {
        var activeModel = models[modelIdx];
        var isLastModel = modelIdx == models.Length - 1;

        var requestBody = new
        {
            model = activeModel,
            max_tokens = _maxTokens,
            stream = true,
            system = systemPrompt,
            messages
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ClaudeApiUrl);
        httpRequest.Headers.Add("x-api-key", _apiKey);
        httpRequest.Headers.Add("anthropic-version", AnthropicVersion);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        // C# does not allow yield inside try-catch, so use flag variables
        HttpResponseMessage? response = null;
        string? sendError = null;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Claude wizard API timeout after {Timeout}s", _timeoutSeconds);
            sendError = "AI yanit suresi doldu. Lutfen tekrar deneyin.";
        }

        if (sendError != null)
        {
            yield return new WizardStreamChunk { Type = "error", Content = sendError };
            yield break;
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            var statusCode = response != null ? (int)response.StatusCode : 0;
            var errorBody = response != null ? await response.Content.ReadAsStringAsync(cts.Token) : "null response";
            _logger.LogWarning("Claude wizard API error {Status}: {Body}", statusCode, errorBody);

            // Overloaded — try fallback model before giving up
            if (statusCode == 529 && !isLastModel)
            {
                _logger.LogInformation("Wizard: {Model} overloaded (HTTP 529), falling back to {Fallback}", activeModel, models[modelIdx + 1]);
                response?.Dispose();
                continue;
            }

            var userMsg = "AI servisi gecici olarak kullanilamiyor.";
            if (statusCode == 429) userMsg = "AI istek limiti asildi. Birkaç saniye bekleyip tekrar deneyin.";
            else if (statusCode == 529) userMsg = "AI servisi su anda yogun. Lutfen biraz bekleyin.";
            else if (statusCode >= 500) userMsg = $"AI servisi hatasi (HTTP {statusCode}). Tekrar deneyin.";

            yield return new WizardStreamChunk { Type = "error", Content = userMsg };
            yield break;
        }

        var fullText = new StringBuilder();
        using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);
        string? streamError = null;
        var streamOverloaded = false;

        while (!reader.EndOfStream && !cts.Token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cts.Token);
            if (line == null) break;

            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") break;

            WizardStreamChunk? chunk = null;
            bool shouldStop = false;

            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                var type = root.GetProperty("type").GetString();

                if (type == "content_block_delta")
                {
                    var delta = root.GetProperty("delta");
                    if (delta.TryGetProperty("text", out var textProp))
                    {
                        var text = textProp.GetString() ?? "";
                        fullText.Append(text);
                        chunk = new WizardStreamChunk { Type = "text", Content = text };
                    }
                }
                else if (type == "message_stop")
                {
                    shouldStop = true;
                }
                else if (type == "error")
                {
                    var errorMsg = root.TryGetProperty("error", out var errProp)
                        ? errProp.GetProperty("message").GetString() ?? "Unknown error"
                        : "Unknown error";
                    _logger.LogWarning("Claude stream error: {Error}", errorMsg);
                    streamOverloaded = errorMsg.Contains("overload", StringComparison.OrdinalIgnoreCase);
                    streamError = $"AI hatasi: {errorMsg}";
                    shouldStop = true;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug("Malformed SSE line skipped: {Data} — {Error}", data, ex.Message);
            }

            if (chunk != null) yield return chunk;
            if (shouldStop) break;
        }

        if (streamError != null)
        {
            // Overloaded mid-stream before any content — try fallback model
            if (streamOverloaded && !isLastModel && fullText.Length == 0)
            {
                _logger.LogInformation("Wizard: {Model} overloaded (stream), falling back to {Fallback}", activeModel, models[modelIdx + 1]);
                response?.Dispose();
                continue;
            }
            yield return new WizardStreamChunk { Type = "error", Content = streamError };
            yield break;
        }

        // Check if the full response contains a flow config and/or options
        var fullResponse = fullText.ToString();
        var flowConfig = ExtractFlowConfig(fullResponse);
        var prerequisites = flowConfig != null ? ExtractPrerequisites(flowConfig) : null;
        var options = ExtractOptions(fullResponse);

        _logger.LogInformation("Wizard response: length={Length}, hasFlowConfig={HasConfig}, hasOptions={HasOptions}",
            fullResponse.Length, flowConfig != null, options != null);
        if (flowConfig == null && fullResponse.Contains("flowconfig"))
            _logger.LogWarning("Wizard: flowconfig block found in text but ExtractFlowConfig returned null. Response tail: {Tail}",
                fullResponse.Length > 500 ? fullResponse[^500..] : fullResponse);

        // Always strip code blocks from content so user sees clean text
        var cleanContent = StripCodeBlocks(fullResponse);

        // Validate API URLs in generated flow config before offering to user
        if (flowConfig != null)
        {
            yield return new WizardStreamChunk { Type = "text", Content = "\n\n\ud83d\udd0d API adresleri dogrulanıyor..." };

            var urlFailures = await ValidateApiUrlsAsync(flowConfig, ct);
            if (urlFailures.Count > 0)
            {
                var warning = new StringBuilder("\n\n\u26a0\ufe0f API Dogrulama Sonucu:\n");
                foreach (var f in urlFailures)
                    warning.AppendLine($"\u2022 \u274c {f.Label}: {f.Host} sunucudan erisilemedi");
                warning.AppendLine("\nBu haliyle akis calismaz. \"Duzelt\" diyerek calisan alternatif API'ler onerebilirim.");

                cleanContent += warning.ToString();
                flowConfig = null; // Don't offer broken config for apply
            }
        }

        yield return new WizardStreamChunk
        {
            Type = "done",
            Content = cleanContent,
            FlowConfig = flowConfig,
            Prerequisites = prerequisites,
            Options = options
        };
        yield break; // Success — don't try next model
        } // end for (modelIdx)
    }

    /// <summary>
    /// Extract FlowConfigV2 JSON from ```flowconfig or ```json code blocks in the response.
    /// Tries ```flowconfig first, then falls back to ```json blocks containing valid FlowConfigV2.
    /// </summary>
    public string? ExtractFlowConfig(string response)
    {
        // Try ```flowconfig block first (primary format) — with closing ```
        var match = Regex.Match(response, @"```flowconfig\s*([\s\S]*?)```", RegexOptions.Multiline);
        if (match.Success)
        {
            var result = ValidateFlowConfigJson(match.Groups[1].Value.Trim());
            if (result != null) return result;
        }

        // Fallback: try ```json blocks that contain valid FlowConfigV2
        var jsonMatches = Regex.Matches(response, @"```json\s*([\s\S]*?)```", RegexOptions.Multiline);
        foreach (Match jm in jsonMatches)
        {
            var result = ValidateFlowConfigJson(jm.Groups[1].Value.Trim());
            if (result != null) return result;
        }

        // Fallback 2: flowconfig block without closing ``` (truncated by max_tokens/timeout)
        var unclosedMatch = Regex.Match(response, @"```flowconfig\s*([\s\S]+)", RegexOptions.Multiline);
        if (unclosedMatch.Success)
        {
            var raw = unclosedMatch.Groups[1].Value.Trim();
            // Try to repair: strip trailing ``` if partially present, then balance braces
            raw = Regex.Replace(raw, @"`+$", "").Trim();
            var repaired = RepairTruncatedJson(raw);
            if (repaired != null)
            {
                var result = ValidateFlowConfigJson(repaired);
                if (result != null)
                {
                    _logger.LogInformation("Wizard: extracted flowconfig from unclosed block (repaired truncated JSON)");
                    return result;
                }
            }
        }

        // Fallback 3: unclosed ```json block with FlowConfigV2
        var unclosedJsonMatch = Regex.Match(response, @"```json\s*([\s\S]+)", RegexOptions.Multiline);
        if (unclosedJsonMatch.Success)
        {
            var raw = Regex.Replace(unclosedJsonMatch.Groups[1].Value.Trim(), @"`+$", "").Trim();
            var repaired = RepairTruncatedJson(raw);
            if (repaired != null)
            {
                var result = ValidateFlowConfigJson(repaired);
                if (result != null)
                {
                    _logger.LogInformation("Wizard: extracted flowconfig from unclosed json block (repaired truncated JSON)");
                    return result;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Attempt to repair truncated JSON by closing unclosed braces/brackets.
    /// Returns null if the JSON is too broken to repair.
    /// </summary>
    private static string? RepairTruncatedJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        // Quick check: if it already parses, return as-is
        try { using var _ = JsonDocument.Parse(json); return json; } catch { }

        // Strip trailing comma, incomplete key/value
        var trimmed = Regex.Replace(json, @",\s*$", "");
        // Remove incomplete trailing string/key (e.g., `"some_key": "partial val`)
        trimmed = Regex.Replace(trimmed, @",?\s*""[^""]*$", "");
        // Remove trailing colon with incomplete value
        trimmed = Regex.Replace(trimmed, @",?\s*""[^""]*""\s*:\s*""?[^""}\]]*$", "");

        // Count open vs close braces/brackets
        int braces = 0, brackets = 0;
        bool inString = false;
        char prev = '\0';
        foreach (var c in trimmed)
        {
            if (c == '"' && prev != '\\') inString = !inString;
            if (!inString)
            {
                if (c == '{') braces++;
                else if (c == '}') braces--;
                else if (c == '[') brackets++;
                else if (c == ']') brackets--;
            }
            prev = c;
        }

        // Close unclosed brackets/braces
        var sb = new StringBuilder(trimmed);
        for (int i = 0; i < brackets; i++) sb.Append(']');
        for (int i = 0; i < braces; i++) sb.Append('}');

        var repaired = sb.ToString();
        try { using var _ = JsonDocument.Parse(repaired); return repaired; } catch { return null; }
    }

    private string? ValidateFlowConfigJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("version", out var ver) && ver.GetInt32() == 2
                && root.TryGetProperty("nodes", out _)
                && root.TryGetProperty("edges", out _))
            {
                return json;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug("ValidateFlowConfigJson: invalid JSON: {Error}", ex.Message);
        }
        return null;
    }

    /// <summary>
    /// Analyze a flow config and return prerequisites the user needs to complete.
    /// </summary>
    public List<FlowPrerequisite>? ExtractPrerequisites(string flowConfigJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(flowConfigJson);
            if (!doc.RootElement.TryGetProperty("nodes", out var nodes))
                return null;
            var prerequisites = new List<FlowPrerequisite>();
            var nodeTypes = new HashSet<string>();

            foreach (var node in nodes.EnumerateArray())
            {
                var nodeType = node.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";
                nodeTypes.Add(nodeType);
            }

            if (nodeTypes.Contains("ai_faq"))
            {
                prerequisites.Add(new FlowPrerequisite
                {
                    Type = "action_required",
                    Title = "FAQ Verileri Yukleyin",
                    Description = "Bu akis FAQ dugumu kullaniyor. Calisabilmesi icin FAQ sorulari ve cevaplari eklemeniz gerekiyor.",
                    Action = "faq_upload"
                });
            }

            if (nodeTypes.Contains("ai_intent"))
            {
                prerequisites.Add(new FlowPrerequisite
                {
                    Type = "action_required",
                    Title = "Intent Tanimlarini Kontrol Edin",
                    Description = "Bu akis intent algilama kullaniyor. Sektorunuze uygun intent tanimlari otomatik yuklenir, ancak kontrol etmeniz onerilir.",
                    Action = "intent_review"
                });
            }

            if (nodeTypes.Contains("action_api_call"))
            {
                prerequisites.Add(new FlowPrerequisite
                {
                    Type = "configuration",
                    Title = "API Endpoint URL'lerini Yapilandirin",
                    Description = "Bu akis harici API cagrisi yapiyor. API URL'lerini ve gerekli header'lari duzenleyicide yapilandirin.",
                    Action = "api_configure"
                });
            }

            if (nodeTypes.Contains("webhook_trigger"))
            {
                prerequisites.Add(new FlowPrerequisite
                {
                    Type = "integration",
                    Title = "Webhook URL'sini Dis Sisteminize Ekleyin",
                    Description = "Bu akis harici webhook ile tetikleniyor. Webhook URL'sini ilgili servise ekleyin.",
                    Action = "webhook_setup"
                });
            }

            if (nodeTypes.Contains("schedule_trigger"))
            {
                prerequisites.Add(new FlowPrerequisite
                {
                    Type = "configuration",
                    Title = "Zamanlama Ayarlarini Kontrol Edin",
                    Description = "Bu akis zamanlanmis tetikleyici kullaniyor. Cron ifadesinin dogru ayarlandigindan emin olun.",
                    Action = "schedule_review"
                });
            }

            return prerequisites.Count > 0 ? prerequisites : null;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug("ExtractPrerequisites: invalid flowConfigJson: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Extract structured options from ```options JSON blocks in the response.
    /// Returns null if no options block is found.
    /// </summary>
    public List<WizardOption>? ExtractOptions(string response)
    {
        var match = Regex.Match(response, @"```options\s*([\s\S]*?)```", RegexOptions.Multiline);
        if (!match.Success) return null;

        var json = match.Groups[1].Value.Trim();
        try
        {
            var options = JsonSerializer.Deserialize<List<WizardOption>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return options is { Count: > 0 } ? options : null;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug("ExtractOptions: invalid JSON in options block: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Strip ```options, ```flowconfig, and FlowConfigV2-containing ```json blocks
    /// from the text so the user sees clean prose.
    /// </summary>
    private string StripCodeBlocks(string text)
    {
        var result = Regex.Replace(text, @"```options\s*[\s\S]*?```", "", RegexOptions.Multiline);
        result = Regex.Replace(result, @"```flowconfig\s*[\s\S]*?```", "", RegexOptions.Multiline);
        // Strip ```json blocks that contain FlowConfigV2 (version: 2 + nodes + edges)
        result = Regex.Replace(result, @"```json\s*([\s\S]*?)```", m =>
        {
            return ValidateFlowConfigJson(m.Groups[1].Value.Trim()) != null ? "" : m.Value;
        }, RegexOptions.Multiline);
        return result.TrimEnd();
    }

    /// <summary>
    /// Validate API URLs in a generated flow config by making test HTTP requests from the server.
    /// Returns list of unreachable URLs. Empty list = all reachable.
    /// Replaces {{variable}} placeholders with "test" before probing.
    /// </summary>
    private async Task<List<ApiUrlFailure>> ValidateApiUrlsAsync(string flowConfigJson, CancellationToken ct)
    {
        var failures = new List<ApiUrlFailure>();

        List<(string Label, string RawUrl)> apiUrls;
        try
        {
            using var doc = JsonDocument.Parse(flowConfigJson);
            if (!doc.RootElement.TryGetProperty("nodes", out var nodes))
                return failures;

            apiUrls = new();
            foreach (var node in nodes.EnumerateArray())
            {
                var type = node.TryGetProperty("type", out var t) ? t.GetString() : "";
                if (type != "action_api_call") continue;
                if (!node.TryGetProperty("data", out var data)) continue;

                var url = data.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(url)) continue;

                var label = data.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
                apiUrls.Add((label, url));
            }
        }
        catch (JsonException)
        {
            return failures;
        }

        if (apiUrls.Count == 0) return failures;

        // Validate all URLs in parallel (5s timeout each)
        var tasks = apiUrls.Select(async entry =>
        {
            var (label, rawUrl) = entry;
            var testUrl = Regex.Replace(rawUrl, @"\{\{[^}]+\}\}", "test");

            if (!Uri.TryCreate(testUrl, UriKind.Absolute, out var uri))
                return new ApiUrlFailure(label, "gecersiz-url");

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                using var response = await _httpClient.GetAsync(testUrl, cts.Token);
                return null; // Any HTTP response = server reachable
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // App shutdown
            }
            catch
            {
                return new ApiUrlFailure(label, uri.Host);
            }
        });

        try
        {
            var results = await Task.WhenAll(tasks);
            failures.AddRange(results.Where(r => r != null)!);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("API URL validation error: {Error}", ex.Message);
        }

        return failures;
    }

    private sealed record ApiUrlFailure(string Label, string Host);

    private static string BuildSystemPrompt(List<FlowSummaryContext>? existingFlows, string? currentFlowConfig = null, string? executionContext = null)
    {
        var sb = new StringBuilder();

        // MONITOR MODE — when execution context is provided
        if (!string.IsNullOrEmpty(executionContext))
        {
            sb.AppendLine("Sen, kucuk isletme sahibinin WhatsApp asistanini birlikte gozden gecirmesine yardim eden samimi, cozum odakli bir yardimcisin. Su anda asistanin nasil calistigini inceliyorsunuz.");
            sb.AppendLine();
            sb.AppendLine("## DEGERLENDIRME MODU KURALLARI");
            sb.AppendLine("- Asagida asistanin yapilandirmasi ve son musteri konusmalarinin detaylari var.");
            sb.AppendLine("- Her adimi sade bir dille incele: kullanicidan ne istenmis, asistan ne cevap vermis, ne kadar surmus.");
            sb.AppendLine("- Bir hata veya tuhaf bir durum varsa neden oldugunu sade dilde acikla — teknik jargon kullanma.");
            sb.AppendLine("- Yavas calisan adimlari (3 saniyeden uzun) 'musterinizi bekletiyor olabilir' diye belirt.");
            sb.AppendLine("- Kullanici degisiklik isterse: once kisaca ne yapacagini sade dilde anlat, onay al, sonra ```flowconfig blogu ile tam JSON uret.");
            sb.AppendLine("- Proaktif yardim et: 'Su noktada musterileriniz takiliyor olabilir, sunu deneyelim mi?' gibi onerilerde bulun.");
            sb.AppendLine("- Yanit duzeni: 1) Kisa ozet (her sey iyi mi yoksa bir sey mi var), 2) Varsa dikkat etmek gereken durumlar, 3) Iyilestirme onerin.");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(currentFlowConfig))
            {
                sb.AppendLine("## Akis Konfigurasyonu");
                sb.AppendLine("```json");
                sb.AppendLine(currentFlowConfig);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            sb.AppendLine("## Yurutme Detaylari");
            sb.AppendLine(executionContext);
            sb.AppendLine();
        }
        else if (!string.IsNullOrEmpty(currentFlowConfig))
        {
            sb.AppendLine("Sen, kucuk isletme sahibinin WhatsApp asistanini birlikte iyilestirmesine yardim eden samimi, cozum odakli bir yardimcisin. Su anda kullanicinin mevcut asistanini onunla beraber sekillendiriyorsunuz.");
            sb.AppendLine();
            sb.AppendLine("## IYILESTIRME MODU KURALLARI");
            sb.AppendLine("- Mevcut akisin yapisini asagida goreceksin. Bu akisi TEMEL AL.");
            sb.AppendLine("- Kullanici degisiklik istediginde, mevcut adimlari KORU. Sadece istenen degisiklikleri yap.");
            sb.AppendLine("- Kullanici acikca 'sil', 'kaldir' veya 'cikar' demedikce mevcut adimlari SILME.");
            sb.AppendLine("- Degisiklik yaparken TUM flow_config JSON'unu uret (sadece diff degil, tam JSON).");
            sb.AppendLine("- Mevcut node ID'lerini ve edge'lerini koru, yeni eklemeler icin yeni ID'ler kullan.");
            sb.AppendLine("- Once ne yapacagini KISA ve sade dilde anlat (teknik jargon yok). Kullanici onaylarsa (evet, tamam, devam et, uygula vb.) AYNI YANIT ICINDE failure-path analizi yap VE ```flowconfig blogu ile tam JSON uret. Onay sonrasi SADECE metin gonderip bekleme, mutlaka flowconfig uret!");
            sb.AppendLine();
            sb.AppendLine("## Mevcut Akis Yapisi");
            sb.AppendLine("```json");
            sb.AppendLine(currentFlowConfig);
            sb.AppendLine("```");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Sen, kucuk isletme sahibinin WhatsApp asistanini sifirdan kurmasina yardim eden samimi, cozum odakli bir yardimcisin. Gorevin, kullanicinin isine ozel bir WhatsApp asistani tasarlamasina elini tutarak yardimci olmak.");
        }
        sb.AppendLine();

        // Audience + tone (Q feedback 2026-05-16: dis hekimi, restoran sahibi, kuafor gibi teknik olmayan is insanlari)
        sb.AppendLine("<audience>");
        sb.AppendLine("Kullanicilarin teknik degil, gunluk isiyle ugrasan kucuk isletme sahipleri: dis hekimi, restoran sahibi, kuafor, klinik yoneticisi, esnaf vb.");
        sb.AppendLine("AI ile konusma deneyimleri YOK denecek kadar az. Karmasik sorular, secenekli onay isteyen sorular, teknik aciklamalar onlari kacirir.");
        sb.AppendLine("Senin tonun: hep yardimsever, ekstra mil gitmeye hevesli, isi BITIRMEYE ve onlari memnun etmeye odakli, sicak.");
        sb.AppendLine("</audience>");
        sb.AppendLine();

        // Response style — sade, jargonsuz, nokta atisi
        sb.AppendLine("<response_style>");
        sb.AppendLine("Turkce konusursun. Sade, sicak, kisa cumleler. 'Tabii ki', 'Hemen yapalim', 'Su sekilde dusunuyorum' gibi yardimsever ifadeler kullan.");
        sb.AppendLine("YASAK kelimeler — kullanicinin bilmek zorunda olmadigi teknik jargon: 'node', 'dugum', 'flow', 'akis konfigurasyonu', 'trigger', 'tetikleyici', 'webhook', 'handle', 'edge', 'baglanti noktasi', 'payload', 'JSON', 'API', 'config', 'wait_for_input', 'condition', 'flowconfig', 'flow_config_snapshot'. Bunlarin yerine 'adim', 'soru', 'mesaj', 'cevap', 'menu', 'secim', 'kosul' gibi gunluk kelimeler kullan.");
        sb.AppendLine("YASAK ifadeler — kafa karistirici: 'sablonundan olusturuldu', 'isletmeme gore ozellestir', 'sektorum ve marka tonumu sor', 'akis tasarlayalim', 'flow gelistirelim', 'optimize edelim'. Yerine: 'Sizin isinize gore birkac sey ayarlayalim', 'Sorularima cevap verirsen, bunu sizin icin hazirlarim'.");
        sb.AppendLine("Bir seferde TEK basit soru sor. Asla zincirli soru sorma. Ornek: 'Firmaniz hangi sektorde?' EVET. 'Firmaniz hangi sektorde ve marka toplulugunuz ne?' HAYIR.");
        sb.AppendLine("Soru sorarken: cumleyi 1-2 satirda bitir. Aciklama gerekiyorsa once 1 cumlelik aciklama, sonra AYRI paragrafta tek soru.");
        sb.AppendLine("Preamble ekleme, direkt konuya gir AMA sicak bir karsilamayla. Ornek: 'Tabii ki, hemen yardimci olayim. Once sunu sorayim: ...'");
        sb.AppendLine("Sadece ```flowconfig blogu icinde JSON uret, baska yerde JSON kullanma. Kullaniciya JSON, kod, teknik isim ASLA gosterme.");
        sb.AppendLine("</response_style>");
        sb.AppendLine();

        // CRITICAL: zero-tolerance JSON / technical leakage guard.
        // Q feedback 2026-05-17 (dis hekimi screenshot): AI raw flowconfig JSON'unu ham olarak yazip
        // gondermisti -> kullanici uygulamayi terk eder. Asagidaki kurallar TARTISMASIZ.
        sb.AppendLine("<no_raw_json>");
        sb.AppendLine("KESIN YASAK: Kullaniciya gonderecegin metin govdesinde ASLA su karakterler/yapilar olmasin:");
        sb.AppendLine("- Satir basinda { veya [ karakteri (object/array baslangici)");
        sb.AppendLine("- \"version\": 2, \"metadata\":, \"nodes\":, \"edges\":, \"settings\":, \"data\":, \"position\": gibi JSON key'leri");
        sb.AppendLine("- Ardisik 2+ satir \"keyword\": value formatinda (JSON property dump)");
        sb.AppendLine("- Tip isimleri: trigger_start, ai_intent, message_text, action_handoff, logic_condition, flow_config, flowconfig, FlowConfigV2");
        sb.AppendLine("- Coordinate/pozisyon: x: 300, y: 50 gibi pixel sayilar");
        sb.AppendLine("Eger flow_config uretmen gerekiyorsa MUTLAKA ```flowconfig fence blogunun ICINDE yap — bu blok kullaniciya hic gosterilmez, FE filtreler. Fence DISINDA tek satir bile JSON yazma.");
        sb.AppendLine("Eger bir akisi sade dille anlatmak istiyorsan: dugum isimlerini KULLANICI DILINDE soyle ('basla', 'mesaj gonder', 'menu', 'AI'a sor', 'temsilciye baglan') — type isimleri (trigger_start vs.) ASLA gecmesin.");
        sb.AppendLine("YANLIS ornek (asla yapma):");
        sb.AppendLine("  Akisi guncelliyorum:");
        sb.AppendLine("  {");
        sb.AppendLine("    \"version\": 2,");
        sb.AppendLine("    \"nodes\": [{ \"id\": \"trigger_start_1\", \"type\": \"trigger_start\" }]");
        sb.AppendLine("  }");
        sb.AppendLine("DOGRU ornek:");
        sb.AppendLine("  Akisi guncelledim. Bekleme sorgusundan sonra musteriyi temsilciye baglayan adim ekledim.");
        sb.AppendLine("  ```flowconfig");
        sb.AppendLine("  {\"version\":2,\"nodes\":[...],\"edges\":[...]}");
        sb.AppendLine("  ```");
        sb.AppendLine("FE tarafinda ek bir guvenlik agi seninle birlikte calisiyor — yine de bu yasagi sen de uygulamak ZORUNDASIN, cunku FE'ye guvenip leaking yapmak Q'nun guvenini kaybettirir.");
        sb.AppendLine("</no_raw_json>");
        sb.AppendLine();

        // Template seed mode — when user starts from a template, AI greets first
        sb.AppendLine("<template_seed_mode>");
        sb.AppendLine("Eger kullanici mesajinda 'TEMPLATE_SEED: <sablon_adi>' formatinda gizli bir baslangic sinyali gorursen, bu kullanicinin bir hazir sablondan baslattigi anlamina gelir. Bu durumda:");
        sb.AppendLine("- Sablonun var oldugundan BAHSETME ('X sablonundan baslattin' deme — bunu zaten kullanici biliyor, panelin ustunde gosteriliyor).");
        sb.AppendLine("- Sicak bir karsilama yap, sonra ilk basit soruyu sor.");
        sb.AppendLine("- IDEAL ILK YANIT ORNEGI: 'Tabii ki, hemen yardimci olayim. Bu akisi sizin isinize uygun hale getirmek icin birkac sorum olacak.\\n\\nFirmaniz hangi sektorde calisiyor?'");
        sb.AppendLine("- Bu ilk yanitta options blogu KOY: { \"label\": \"Saglik / Klinik\" }, { \"label\": \"Restoran / Yemek\" }, { \"label\": \"Kuafor / Guzellik\" }, { \"label\": \"Diger (yaziyla soyle)\" } gibi 3-4 yaygin sektor secenegi sun.");
        sb.AppendLine("</template_seed_mode>");
        sb.AppendLine();

        // Rules — sade
        sb.AppendLine("<rules>");
        sb.AppendLine("Once kullanicinin ne istedigini anla. Belirsiz seyleri tek tek, sade dilde sor.");
        sb.AppendLine("Asistani EN AZ adimla tasarla — her ek adim musteriyi bekletir.");
        sb.AppendLine("Kullanici 'olustur', 'tamam', 'devam', 'uygula' diyene kadar flowconfig JSON uretme; once tasarimi sade dille konusarak netlestirin.");
        sb.AppendLine("Kullanici onayi alindiktan sonra (evet, tamam, devam, uygula vb.) AYNI YANIT ICINDE: 1) eksik durumlari (musteri yanit vermezse, yanlis sey yazarsa) kisa kontrol et, 2) eksik durum yoksa ```flowconfig blogu ile FlowConfigV2 JSON uret. Onay sonrasi SADECE metin gonderip bekleme — mutlaka flowconfig uret!");
        sb.AppendLine("Sadece gercek bir bilgi eksigi varsa soru sor; aksi halde direkt yap ve uygula.");
        sb.AppendLine("Eger kullanici sikilmis veya kafasi karismis gorunuyorsa: 'Bunu sizin icin ben halledeyim, siz sadece sunu soyleyin: ...' diyerek yuku ustlen.");
        sb.AppendLine("</rules>");
        sb.AppendLine();

        // Reporting / listing — kullanici 'kontrol et', 'rapor', 'incele', 'sorunlari listele', 'durum', 'analiz' gibi
        // bilgi-isteyen sorgular yaparsa: bilgiyi GIZLEME, ASLA 'iste sunlar' deyip detayi sonraya birakma.
        sb.AppendLine("<reporting>");
        sb.AppendLine("Kullanici 'kontrol et', 'rapor', 'incele', 'sorunlari listele', 'durum', 'analiz', 'goz at' gibi bilgi-isteyen bir talep yaparsa: bulduklarini AYNI mesajda madde madde sun. ASLA 'iste sunlar:', 'su sorunlari buldum:' deyip detayi ayri bir adima erteleme — kullanici bilgiyi simdi gormek istiyor.");
        sb.AppendLine("Liste formati: her bulgu icin tek satirlik markdown bullet `- ` ile yaz. Karakter sayisini sinirla, sade tut. Ornek:");
        sb.AppendLine("Akisi inceledim. Bulduklarim:");
        sb.AppendLine("- Menude tek secenek var, ikinci secenek hic baglanmamis.");
        sb.AppendLine("- 'Bekleme' adiminin sonrasi bos — musteri burada takilir.");
        sb.AppendLine("- Fiyat sorusu icin hata yolu yok.");
        sb.AppendLine();
        sb.AppendLine("Bunlari sirayla duzeltelim mi?");
        sb.AppendLine("```options");
        sb.AppendLine("[{\"label\":\"Evet, hepsini duzelt\"},{\"label\":\"Sadece menuyu duzelt\"},{\"label\":\"Sadece eksik baglantilari kur\"}]");
        sb.AppendLine("```");
        sb.AppendLine("ONEMLI: 'Detaylari sonra anlatirim' veya 'isterseniz detaylari paylasayim' DEME — kullanici zaten istedi, hemen ver.");
        sb.AppendLine("</reporting>");
        sb.AppendLine();

        // Structured options (AskUserQuestion-like UX) — sade dilli secenekler
        sb.AppendLine("<options_format>");
        sb.AppendLine("Kullaniciya soru sorarken HER ZAMAN 2-4 hazir secenek sun. Serbest metin cevap bekleme — kullanicilar AI ile konusmaya alisik degil, tiklayarak ilerlemek isterler.");
        sb.AppendLine("Soruyu normal metin olarak yaz, ardindan ```options blogu icinde JSON dizisi olarak secenekleri belirt.");
        sb.AppendLine("Her secenek { \"label\": \"Kisa baslik\", \"description\": \"Aciklama\" } formatinda. label cok kisa (1-4 kelime), description opsiyonel (description sadece gercekten farki acikliyorsa kullan, magna magna aciklama yazma).");
        sb.AppendLine("Secenek labellari da JARGONSUZ olmali — kullanici dili konus: 'Evet, randevu defterimden gozetelim' EVET. 'Yes, Calendar API sync' HAYIR.");
        sb.AppendLine("Ornek:");
        sb.AppendLine("Musterinizin siparis numarasini nasil bulayim?");
        sb.AppendLine("```options");
        sb.AppendLine("[{\"label\":\"Telefonundan otomatik bulayim\",\"description\":\"Aradigi numara ile musteriyi otomatik eslestirelim\"},{\"label\":\"Numarayi musteri yazsin\",\"description\":\"Musteriye soralim, siparis numarasini yazsin\"}]");
        sb.AppendLine("```");
        sb.AppendLine("Kullanici bir secenek tikladiginda, o secenegin label'i mesaj olarak gelir. Ona gore devam et.");
        sb.AppendLine("ONEMLI: Soru sordugun HER yerde options blogu ZORUNLU. Seceneksiz soru SORMA.");
        sb.AppendLine("Kullanici 'bilemiyorum', 'sen karar ver', 'fark etmez' gibi seyler derse: Q'nin yerine SEN karar ver, en yaygin/guvenli secenegi onun adina sec, gerekce sade bir cumleyle anlat ve devam et. Kullaniciyi tekrar dusunmeye zorlama.");
        sb.AppendLine("</options_format>");
        sb.AppendLine();

        // Node registry with REQUIRED data fields
        sb.AppendLine("<node_registry>");
        sb.AppendLine("Her node'un data objesi asagidaki ZORUNLU alanlari icermeli. label her zaman zorunlu.");
        sb.AppendLine();
        sb.AppendLine("trigger_start: data: { label }. Her akista en az 1 tane olmali.");
        sb.AppendLine("webhook_trigger: data: { label, secret_key, payload_variable }.");
        sb.AppendLine("outbound_trigger: data: { label, campaign_variable }.");
        sb.AppendLine("schedule_trigger: data: { label, cron_expression, timezone }.");
        sb.AppendLine();
        sb.AppendLine("message_text: data: { label, text, wait_for_input }. text ZORUNLU — gonderilecek mesaj metni. {{degisken}} destekli. ASLA bos birakma! wait_for_input: true ise mesaj gonderildikten sonra akis durur ve kullanicinin yanitini bekler. Kullanicidan bilgi isteyen mesajlar (ad, telefon, adres, numara vb.) icin wait_for_input: true KULLAN! Bilgilendirme mesajlari icin false veya belirtme.");
        sb.AppendLine("message_menu: data: { label, text, options }. text = baslik metni. options = [{key:\"1\",label:\"Secenek 1\",handle_id:\"opt_1\"}, ...]. Her secenek icin ayri edge cikisi.");
        sb.AppendLine();
        sb.AppendLine("logic_condition: data: { label, variable, operator, value }. operator: equals|contains|starts_with|greater_than|less_than|is_empty|regex. Cikis: sourceHandle=\"true_handle\" veya \"false_handle\".");
        sb.AppendLine("logic_switch: data: { label, variable, cases, default_handle_id }. cases = [{value:\"x\",handle_id:\"case_1\"}, ...]. Her case icin ayri edge.");
        sb.AppendLine();
        sb.AppendLine("ai_intent: data: { label, confidence_threshold, ask_name, greeting_message }. confidence_threshold: 0.0-1.0. Cikis: \"high_confidence\"/\"low_confidence\".");
        sb.AppendLine("ai_faq: data: { label, min_confidence }. Cikis: \"matched\"/\"no_match\".");
        sb.AppendLine("ai_sentiment: data: { label, threshold }. Cikis: \"positive\"/\"negative\".");
        sb.AppendLine();
        sb.AppendLine("action_handoff: data: { label, summary_template }. Terminal node — baska node'a baglanmaz.");
        sb.AppendLine("action_api_call: data: { label, method, url, headers, body_template, response_variable, timeout_ms }. method: GET|POST|PUT|DELETE. Cikis: \"success\"/\"error\".");
        sb.AppendLine("action_ecommerce: data: { label, provider, operation, filter_phone, filter_email, filter_search, filter_status, order_id, product_id, response_variable }. provider: \"ikas\" (veya diger e-ticaret platformu). operation: list_orders|get_order|list_products|get_product|list_customers|fulfill_order|update_order_status|refund_order_line. Cikis: \"success\"/\"error\". Sonuc response_variable'a (default: ecom_result) JSON olarak yazilir. ONEMLI: E-ticaret islemleri icin action_api_call DEGIL action_ecommerce kullan! Endpoint bilgisi otomatik — kullanicidan URL isteme.");
        sb.AppendLine("action_delay: data: { label, seconds }. seconds: 1-300.");
        sb.AppendLine();
        sb.AppendLine("utility_set_variable: data: { label, variable_name, value_expression }. {{degisken}} destekli.");
        sb.AppendLine("utility_note: data: { label, text }. Sadece tasarimci notu, calistirilmaz.");
        sb.AppendLine();
        sb.AppendLine("KRITIK: message_text icin text alani ASLA bos olamaz! Kullaniciyla konusarak belirlenen mesaj metnini data.text'e yaz.");
        sb.AppendLine("KRITIK: Kullanicidan bilgi toplayan message_text node'larinda (isim, telefon, adres, numara vb. soran) wait_for_input: true ZORUNLU! Aksi halde akis durmadan tum mesajlari arka arkaya gonderir.");
        sb.AppendLine("</node_registry>");
        sb.AppendLine();

        // FlowConfigV2 schema (compact)
        sb.AppendLine("<output_schema>");
        sb.AppendLine("FlowConfigV2: { version: 2, metadata: { name }, nodes: [{ id, type, position: {x,y}, data: { label, ...config } }], edges: [{ id, source, target, sourceHandle }], settings: { off_hours_message, unknown_input_message, handoff_confidence_threshold, session_timeout_minutes, max_loop_count } }");
        sb.AppendLine("Node ID format: {type}_{sayi}. Coklu cikisli dugumler (logic_condition, ai_intent, ai_faq, ai_sentiment, action_api_call) icin sourceHandle zorunlu, tek cikisli icin null.");
        sb.AppendLine("</output_schema>");
        sb.AppendLine();

        // API guidelines — known working/broken APIs + validation notice
        sb.AppendLine("<api_guidelines>");
        sb.AppendLine("Harici API onerirken asagidaki kurallara uy:");
        sb.AppendLine("1. API key gerektirmeyen ucretsiz servisleri TERCIH ET.");
        sb.AppendLine("2. Dogrulanmis calisan API'ler:");
        sb.AppendLine("   - Hava durumu: open-meteo.com (geocoding: geocoding-api.open-meteo.com/v1/search, forecast: api.open-meteo.com/v1/forecast)");
        sb.AppendLine("   - Doviz kuru: cdn.jsdelivr.net/npm/@fawazahmed0/currency-api (ucretsiz, API key yok)");
        sb.AppendLine("3. ONERME (sunucudan erisilemez veya guvenilmez):");
        sb.AppendLine("   - wttr.in (sunucudan zaman asimi, erisilemez)");
        sb.AppendLine("4. API key gerektiren servisler icin kullaniciyi acikca bilgilendir ve prerequisite olarak belirt.");
        sb.AppendLine("5. Bilinmeyen bir API onereceksen, erisim riski hakkinda uyar.");
        sb.AppendLine("6. E-TICARET ISLEMLERI (siparis, urun, kategori, musteri) icin action_api_call KULLANMA! Bunun yerine action_ecommerce node'u kullan. Bu node otomatik olarak tenant'in baglandigi e-ticaret platformuyla (ikas vb.) iletisim kurar. Endpoint URL, auth bilgileri OTOMATIK — kullanicidan API endpoint veya URL SORMA.");
        sb.AppendLine("NOT: Akis uretildikten sonra API URL'leri sunucudan OTOMATIK dogrulanir. Erisilemez API'ler tespit edilirse akis reddedilir.");
        sb.AppendLine("</api_guidelines>");
        sb.AppendLine();

        // Existing flows context
        if (existingFlows != null && existingFlows.Count > 0)
        {
            sb.AppendLine("## Tenant'in Mevcut Akislari");
            sb.AppendLine("Asagidaki akislar zaten mevcut. Yeni akis olusturulurken bunlarla cakisma veya entegrasyon firsatlari kontrol edilmelidir:");
            sb.AppendLine();
            foreach (var flow in existingFlows)
            {
                sb.Append($"- **{flow.FlowName}** (ID: {flow.FlowId}");
                if (flow.IsActive) sb.Append(", AKTIF");
                sb.Append($", {flow.NodeCount} dugum");
                if (flow.NodeTypes.Count > 0) sb.Append($", tipler: {string.Join(", ", flow.NodeTypes)}");
                sb.AppendLine(")");
            }
            sb.AppendLine();
            sb.AppendLine("ONEMLI: Eger yeni akis mevcut bir akisla benzer amaca hizmet ediyorsa, kullaniciya bunu bildir ve entegrasyon/farklilastirma oner.");
        }

        return sb.ToString();
    }
}

// ============================================================
// DTOs
// ============================================================

public sealed class WizardMessage
{
    [System.Text.Json.Serialization.JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("flow_config_snapshot")]
    public string? FlowConfigSnapshot { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("options")]
    public List<WizardOption>? Options { get; set; }
}

public sealed class WizardStreamChunk
{
    public string Type { get; set; } = "text"; // text, done, error, flow_config
    public string Content { get; set; } = "";
    public string? FlowConfig { get; set; }
    public List<FlowPrerequisite>? Prerequisites { get; set; }
    public List<WizardOption>? Options { get; set; }
}

public sealed class WizardOption
{
    [System.Text.Json.Serialization.JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class FlowSummaryContext
{
    public int FlowId { get; set; }
    public string FlowName { get; set; } = "";
    public bool IsActive { get; set; }
    public int NodeCount { get; set; }
    public List<string> NodeTypes { get; set; } = new();
}

public sealed class FlowPrerequisite
{
    public string Type { get; set; } = ""; // action_required, configuration, integration
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Action { get; set; } = ""; // faq_upload, intent_review, api_configure, webhook_setup, schedule_review
}
