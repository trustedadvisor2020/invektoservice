using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Invekto.Shared.Logging;

namespace Invekto.WebChat.Services;

/// <summary>
/// Generates AI auto-replies using Claude API when operator is unavailable.
/// Pattern: ReplyGenerator.cs from AgentAI (HttpClient + Claude Messages API).
/// </summary>
public sealed class AIReplyService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _timeoutMs;
    private readonly JsonLinesLogger _logger;
    private readonly string _systemPrompt;

    private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
    private const int MaxTokens = 512;

    public AIReplyService(HttpClient httpClient, string apiKey, string model, int timeoutMs, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _timeoutMs = timeoutMs;
        _logger = logger;

        _systemPrompt = """
            Sen Invekto'nun web sitesindeki otomatik musteri temsilcisisin.
            Invekto, isletmeler icin CRM ve iletisim platformu sunan bir SaaS sirketidir.

            Kurallarin:
            - Kibarca, samimi ve kisa yanitla (maks 2-3 cumle).
            - Turkce yaz.
            - Invekto urunleri hakkinda genel bilgi verebilirsin: WhatsApp Business API,
              cok kanalli iletisim, AI destekli analiz, otomasyon, randevu yonetimi.
            - Fiyat bilgisi verme, "ekibimiz size detayli bilgi verecektir" de.
            - Teknik destek sorularinda "ekibimiz en kisa surede size donecek" de.
            - Bilmedigin konularda "bu konuda size yardimci olmam icin ekibimize iletiyorum" de.
            - Asla uydurma bilgi verme.
            - Ic dusunceni, muhakemeni veya meta-yorumunu ASLA yazma. Sadece cevabini yaz.
            - "Dusuneyim", "Hmm", "Aslinda" gibi dolgu ifadeler kullanma.
            """;
    }

    public async Task<string?> GenerateReplyAsync(
        string visitorMessage, List<(string role, string text)>? history, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeoutMs);

            var messages = new List<object>();

            // Add conversation history (last 10 messages)
            if (history != null)
            {
                foreach (var (role, text) in history.TakeLast(10))
                {
                    messages.Add(new { role, content = text });
                }
            }

            // Add the current visitor message
            messages.Add(new { role = "user", content = visitorMessage });

            var requestBody = new
            {
                model = _model,
                max_tokens = MaxTokens,
                system = _systemPrompt,
                messages
            };

            var json = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, ClaudeApiUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _httpClient.SendAsync(request, cts.Token);
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemError($"Claude API error: {response.StatusCode} - {responseBody}");
                return null;
            }

            using var doc = JsonDocument.Parse(responseBody);
            var content = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            sw.Stop();
            _logger.SystemInfo($"AI reply generated in {sw.ElapsedMilliseconds}ms");

            return content;
        }
        catch (OperationCanceledException)
        {
            _logger.SystemError($"AI reply timeout after {sw.ElapsedMilliseconds}ms");
            return null;
        }
        catch (Exception ex)
        {
            _logger.SystemError($"AI reply failed: {ex.Message}");
            return null;
        }
    }
}
