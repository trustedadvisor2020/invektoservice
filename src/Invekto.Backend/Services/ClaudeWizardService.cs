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
    private readonly int _maxTokens;
    private readonly int _timeoutSeconds;
    private readonly ILogger<ClaudeWizardService> _logger;

    public ClaudeWizardService(HttpClient httpClient, IConfiguration config, ILogger<ClaudeWizardService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Claude:ApiKey"] ?? "";
        _model = config["Claude:WizardModel"] ?? "claude-sonnet-4-6";
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

        var requestBody = new
        {
            model = _model,
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
            var errorBody = response != null ? await response.Content.ReadAsStringAsync(cts.Token) : "null response";
            _logger.LogWarning("Claude wizard API error {Status}: {Body}", (int)response.StatusCode, errorBody);
            yield return new WizardStreamChunk { Type = "error", Content = "AI servisi gecici olarak kullanilamiyor." };
            yield break;
        }

        var fullText = new StringBuilder();
        using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);
        string? streamError = null;

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
                    streamError = "AI hatasi olustu.";
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
            yield return new WizardStreamChunk { Type = "error", Content = streamError };
            yield break;
        }

        // Check if the full response contains a flow config and/or options
        var fullResponse = fullText.ToString();
        var flowConfig = ExtractFlowConfig(fullResponse);
        var prerequisites = flowConfig != null ? ExtractPrerequisites(flowConfig) : null;
        var options = ExtractOptions(fullResponse);

        // Strip options block from content so user sees clean text
        var cleanContent = options != null ? StripOptionsBlock(fullResponse) : fullResponse;

        yield return new WizardStreamChunk
        {
            Type = "done",
            Content = cleanContent,
            FlowConfig = flowConfig,
            Prerequisites = prerequisites,
            Options = options
        };
    }

    /// <summary>
    /// Extract FlowConfigV2 JSON from ```flowconfig code blocks in the response.
    /// </summary>
    public string? ExtractFlowConfig(string response)
    {
        var match = Regex.Match(response, @"```flowconfig\s*\n([\s\S]*?)```", RegexOptions.Multiline);
        if (!match.Success) return null;

        var json = match.Groups[1].Value.Trim();
        try
        {
            using var doc = JsonDocument.Parse(json);
            // Validate it looks like a FlowConfigV2
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
            _logger.LogDebug("ExtractFlowConfig: invalid JSON in flowconfig block: {Error}", ex.Message);
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
        var match = Regex.Match(response, @"```options\s*\n([\s\S]*?)```", RegexOptions.Multiline);
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
    /// Strip ```options blocks from the text so the user sees clean prose.
    /// </summary>
    private static string StripOptionsBlock(string text)
    {
        return Regex.Replace(text, @"```options\s*\n[\s\S]*?```", "", RegexOptions.Multiline).TrimEnd();
    }

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

        // Node registry (compact)
        sb.AppendLine("<node_registry>");
        sb.AppendLine("Tetikleyiciler: trigger_start (standart, her akista en az 1), webhook_trigger (HTTP POST), outbound_trigger (kampanya), schedule_trigger (cron).");
        sb.AppendLine("Mesaj: message_text (metin, {{degisken}} destekli), message_menu (secenekli menu, her secenek ayri cikis).");
        sb.AppendLine("Mantik: logic_condition (if/else, operator: equals/contains/starts_with/greater_than/less_than/is_empty/regex), logic_switch (coklu dallanma, cases + default).");
        sb.AppendLine("AI: ai_intent (niyet tespiti, intents listesi, confidence_threshold, cikis: high/low_confidence), ai_faq (FAQ arama, cikis: matched/no_match, FAQ onceden yuklenmeli), ai_sentiment (duygu analizi, cikis: positive/negative).");
        sb.AppendLine("Eylem: action_handoff (insan temsilciye aktar, terminal), action_api_call (HTTP cagrisi, method/url/headers/body_template, cikis: success/error), action_delay (bekleme, seconds).");
        sb.AppendLine("Yardimci: utility_set_variable (degisken ata), utility_note (tasarimci notu, calistirilmaz).");
        sb.AppendLine("</node_registry>");
        sb.AppendLine();

        // FlowConfigV2 schema (compact)
        sb.AppendLine("<output_schema>");
        sb.AppendLine("FlowConfigV2: { version: 2, metadata: { name }, nodes: [{ id, type, position: {x,y}, data: { label, ...config } }], edges: [{ id, source, target, sourceHandle }], settings: { off_hours_message, unknown_input_message, handoff_confidence_threshold, session_timeout_minutes, max_loop_count } }");
        sb.AppendLine("Node ID format: {type}_{sayi}. Coklu cikisli dugumler (logic_condition, ai_intent, ai_faq, ai_sentiment, action_api_call) icin sourceHandle zorunlu, tek cikisli icin null.");
        sb.AppendLine("</output_schema>");
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
    public string Label { get; set; } = "";
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
