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
        _model = config["Claude:WizardModel"] ?? "claude-sonnet-4-6-20250514";
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

        // Check if the full response contains a flow config
        var fullResponse = fullText.ToString();
        var flowConfig = ExtractFlowConfig(fullResponse);
        var prerequisites = flowConfig != null ? ExtractPrerequisites(flowConfig) : null;

        yield return new WizardStreamChunk
        {
            Type = "done",
            Content = fullResponse,
            FlowConfig = flowConfig,
            Prerequisites = prerequisites
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
        sb.AppendLine("## Kurallar");
        sb.AppendLine("- Turkce konusursun.");
        sb.AppendLine("- Once kullanicinin amacini anla. Belirsiz noktalar varsa SORU SOR.");
        sb.AppendLine("- Flow'u en verimli, en az node ile tasarla. Gereksiz karmasikliktan kacin.");
        sb.AppendLine("- Kullanici 'olustur' veya 'tamam' diyene kadar flow_config JSON URETME.");
        sb.AppendLine("- Kullanici onayi alindiktan sonra once FAILURE-PATH analizi yap:");
        sb.AppendLine("  * Kosul dugumlerinin false dalinda ne olacak?");
        sb.AppendLine("  * Intent algilinamayan durumda ne olacak?");
        sb.AppendLine("  * Beklenmeyen girdi geldiginde ne olacak?");
        sb.AppendLine("  * Eksik path varsa kullaniciya sor.");
        sb.AppendLine("- Tum path'ler kapandiktan sonra flow_config JSON'i ```flowconfig blogu icinde uret.");
        sb.AppendLine("- Her zaman gecerli FlowConfigV2 JSON uret (asagidaki schema'ya uygun).");
        sb.AppendLine();

        // Node registry
        sb.AppendLine("## Kullanilabilir Node Tipleri");
        sb.AppendLine();
        sb.AppendLine("### Tetikleyiciler (Trigger) - Akisin baslangic noktasi");
        sb.AppendLine("- `trigger_start`: Standart baslangic. Musteri mesaj gonderdiginde tetiklenir. Her akista EN AZ 1 tane olmali.");
        sb.AppendLine("- `webhook_trigger`: Dis sistemden gelen HTTP POST ile tetiklenir. payload_variable degiskeni ayarlanabilir.");
        sb.AppendLine("- `outbound_trigger`: Toplu mesaj kampanyasi tetikleyicisi. campaign_variable ayarlanabilir.");
        sb.AppendLine("- `schedule_trigger`: Cron ifadesi ile zamanlanmis tetikleyici (ornek: '0 9 * * 1' = her Pazartesi 09:00).");
        sb.AppendLine();
        sb.AppendLine("### Mesaj Dugumleri");
        sb.AppendLine("- `message_text`: Kullaniciya metin mesaj gonderir. data.text alanina mesaj yazilir. {{degisken}} kullanilabilir.");
        sb.AppendLine("- `message_menu`: Kullaniciya secenekli menu gosterir. data.options = [{key, label, handle_id}]. Her secenek icin ayri cikis.");
        sb.AppendLine();
        sb.AppendLine("### Mantik Dugumleri");
        sb.AppendLine("- `logic_condition`: If/else dallanma. data.variable, data.operator (equals/contains/starts_with/greater_than/less_than/is_empty/regex), data.value. Cikislar: true_handle, false_handle.");
        sb.AppendLine("- `logic_switch`: Coklu dallanma. data.variable, data.cases = [{value, handle_id}], data.default_handle_id.");
        sb.AppendLine();
        sb.AppendLine("### AI Dugumleri (Claude destekli)");
        sb.AppendLine("- `ai_intent`: Musteri mesajini analiz eder, niyet tespit eder. data.intents = ['randevu', 'fiyat', 'iptal', ...], data.confidence_threshold (0-1). Cikislar: high_confidence (esik ustu), low_confidence (esik alti). Degiskenler: detected_intent, intent_confidence.");
        sb.AppendLine("- `ai_faq`: FAQ veritabaninda arama yapar. Eslesme bulunursa otomatik cevap gonderir. data.min_confidence (0-1). Cikislar: matched, no_match. NOT: FAQ verileri onceden yuklenmelidir!");
        sb.AppendLine("- `ai_sentiment`: Musteri duygusunu analiz eder (pozitif/negatif). data.threshold (0-1). Cikislar: positive, negative. Degiskenler: sentiment_result, sentiment_score.");
        sb.AppendLine();
        sb.AppendLine("### Eylem Dugumleri");
        sb.AppendLine("- `action_handoff`: Gorusmeyi insan temsilciye aktarir. Terminal dugum (akis burada biter). data.summary_template ile ozet olusturulabilir.");
        sb.AppendLine("- `action_api_call`: Harici API cagrisi yapar. data.method (GET/POST/PUT/DELETE), data.url, data.headers, data.body_template, data.response_variable, data.timeout_ms. Cikislar: success, error.");
        sb.AppendLine("- `action_delay`: Belirli sure bekler. data.seconds. Kullanim: kullaniciya dusunme suresi vermek.");
        sb.AppendLine();
        sb.AppendLine("### Yardimci Dugumler");
        sb.AppendLine("- `utility_set_variable`: Degisken ata. data.variable_name, data.value_expression. Ornek: '{{detected_intent}}_processed'.");
        sb.AppendLine("- `utility_note`: Gorsel yorum dugumu. Calistirilmaz, sadece tasarimcinin notlari icin.");
        sb.AppendLine();

        // FlowConfigV2 schema
        sb.AppendLine("## FlowConfigV2 JSON Schema");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"version\": 2,");
        sb.AppendLine("  \"metadata\": { \"name\": \"Akis Adi\" },");
        sb.AppendLine("  \"nodes\": [");
        sb.AppendLine("    { \"id\": \"unique_id\", \"type\": \"node_type\", \"position\": {\"x\": 0, \"y\": 0}, \"data\": { \"label\": \"...\", ...config_fields } }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"edges\": [");
        sb.AppendLine("    { \"id\": \"edge_id\", \"source\": \"node_id\", \"target\": \"node_id\", \"sourceHandle\": \"handle_name_or_null\" }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"settings\": {");
        sb.AppendLine("    \"off_hours_message\": \"Su anda mesai saatleri disindayiz.\",");
        sb.AppendLine("    \"unknown_input_message\": \"Anlayamadim. Lutfen gecerli bir secenek girin.\",");
        sb.AppendLine("    \"handoff_confidence_threshold\": 0.5,");
        sb.AppendLine("    \"session_timeout_minutes\": 30,");
        sb.AppendLine("    \"max_loop_count\": 10");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Edge Kurallari");
        sb.AppendLine("- Her edge'de source ve target node ID'si olmali.");
        sb.AppendLine("- Birden fazla cikisi olan dugumler (logic_condition, ai_intent, ai_faq, ai_sentiment, action_api_call) icin sourceHandle belirtilmeli.");
        sb.AppendLine("- Tek cikisli dugumler icin sourceHandle null olabilir.");
        sb.AppendLine("- Node ID'leri benzersiz olmali: genelde '{type}_{sayi}' formati kullanilir.");
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
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public string? Timestamp { get; set; }
    public string? FlowConfigSnapshot { get; set; }
}

public sealed class WizardStreamChunk
{
    public string Type { get; set; } = "text"; // text, done, error, flow_config
    public string Content { get; set; } = "";
    public string? FlowConfig { get; set; }
    public List<FlowPrerequisite>? Prerequisites { get; set; }
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
