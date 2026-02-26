using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Invekto.Backend.Services;

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
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var systemPrompt = BuildSystemPrompt(existingFlows, currentFlowConfig);

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
        // Try ```flowconfig block first (primary format)
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

        return null;
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

    private static string BuildSystemPrompt(List<FlowSummaryContext>? existingFlows, string? currentFlowConfig = null)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(currentFlowConfig))
        {
            sb.AppendLine("Sen InvektoServices platformunda deneyimli bir chatbot akis tasarimcisisin. Su anda MEVCUT BIR AKISI DUZENLEME modundasin. Kullanici bu akisi AI yardimiyla gelistirmek istiyor.");
            sb.AppendLine();
            sb.AppendLine("## EDIT MODE KURALLARI");
            sb.AppendLine("- Mevcut akisin yapisini asagida goreceksin. Bu akisi TEMEL AL.");
            sb.AppendLine("- Kullanici degisiklik istediginde, mevcut node'lari KORU. Sadece istenen degisiklikleri yap.");
            sb.AppendLine("- Kullanici acikca 'sil', 'kaldir' veya 'cikar' demedikce mevcut node'lari SILME.");
            sb.AppendLine("- Degisiklik yaparken TUM flow_config JSON'unu uret (sadece diff degil, tam JSON).");
            sb.AppendLine("- Mevcut node ID'lerini ve edge'lerini koru, yeni eklemeler icin yeni ID'ler kullan.");
            sb.AppendLine("- Kullanici 'uygula', 'yap', 'degistir' diyene kadar once ne yapacagini ACIKLA, sonra flow_config JSON uret.");
            sb.AppendLine();
            sb.AppendLine("## Mevcut Akis Yapisi");
            sb.AppendLine("```json");
            sb.AppendLine(currentFlowConfig);
            sb.AppendLine("```");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Sen InvektoServices platformunda deneyimli bir chatbot akis tasarimcisisin. Gorevin, kullanicinin WhatsApp chatbot akisi tasarlamasina yardimci olmak.");
        }
        sb.AppendLine();

        // Response style
        sb.AppendLine("<response_style>");
        sb.AppendLine("Turkce konusursun. Kisa, sohbet tarzi yanitlar ver. Bullet listeler yerine akici cumleler kullan.");
        sb.AppendLine("Sadece ```flowconfig blogu icinde JSON uret, baska yerde JSON kullanma.");
        sb.AppendLine("Preamble ekleme, direkt konuya gir.");
        sb.AppendLine("Soru sorarken: Soruyu KISA tut (1-2 cumle). Detayli aciklama gerekiyorsa once aciklamayi yaz, sonda soruyu AYRI paragrafta sor. Soru ve aciklama FARKLI paragraflarda olmali. Uzun aciklamanin ICINE soru gomme.");
        sb.AppendLine("</response_style>");
        sb.AppendLine();

        // Rules
        sb.AppendLine("<rules>");
        sb.AppendLine("Once kullanicinin amacini anla. Belirsiz noktalar varsa soru sor.");
        sb.AppendLine("Flow'u en az node ile tasarla, cunku her ek node WhatsApp kullanici deneyimini yavaslatir ve bakim maliyetini artirir.");
        sb.AppendLine("Kullanici 'olustur' veya 'tamam' diyene kadar flowconfig JSON uretme, once tasarimi konusarak netles.");
        sb.AppendLine("Kullanici onayi alindiktan sonra once failure-path analizi yap: kosullarin false dali, intent algilanamama durumu, beklenmeyen girdi. Eksik path varsa kullaniciya sor.");
        sb.AppendLine("Tum path'ler kapandiktan sonra ```flowconfig blogu icinde gecerli FlowConfigV2 JSON uret.");
        sb.AppendLine("</rules>");
        sb.AppendLine();

        // Structured options (AskUserQuestion-like UX)
        sb.AppendLine("<options_format>");
        sb.AppendLine("Kullaniciya soru sorarken HER ZAMAN secenekler sun. Serbest metin cevap BEKLEME.");
        sb.AppendLine("Soruyu normal metin olarak yaz, ardindan ```options blogu icinde JSON dizisi olarak secenekleri belirt.");
        sb.AppendLine("Her secenek { \"label\": \"Kisa baslik\", \"description\": \"Aciklama\" } formatinda olmali.");
        sb.AppendLine("2-4 secenek sun. description opsiyonel ama tavsiye edilir.");
        sb.AppendLine("Ornek:");
        sb.AppendLine("Siparis numarasini nasil alayim?");
        sb.AppendLine("```options");
        sb.AppendLine("[{\"label\":\"Telefon numarasindan otomatik\",\"description\":\"Musteri telefon numarasiyla otomatik eslestirilir\"},{\"label\":\"Kullanici kendisi yazacak\",\"description\":\"Musteriden siparis numarasini girmesini isteriz\"}]");
        sb.AppendLine("```");
        sb.AppendLine("Kullanici bir secenek tikladiginda, o secenegin label'i mesaj olarak gelir. Ona gore devam et.");
        sb.AppendLine("ONEMLI: Soru sordugun HER yerde options blogu ZORUNLU. Seceneksiz soru SORMA.");
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
