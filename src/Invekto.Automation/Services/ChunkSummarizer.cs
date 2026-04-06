using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Summarizes PDF document chunk content into a customer-friendly answer using Claude Haiku.
/// Called by AiFaqHandler when a chunk (not FAQ) matches the user's query.
/// Returns null on any failure — caller falls back to no_match or raw content.
/// </summary>
public sealed class ChunkSummarizer
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly JsonLinesLogger _logger;

    private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-haiku-4-5-20251001";
    private const int MaxTokens = 300;
    private const int TimeoutMs = 8000;

    private const string SystemPrompt =
        "Sen bir musteri hizmetleri chatbotu icin cevap ozetleyicisisin. " +
        "Sana bir dokumandan alinan metin parcasi ve musterinin sorusu verilecek. " +
        "Gorev: Metin parcasindaki bilgiyi kullanarak musterinin sorusuna kisa, nazik ve anlasilir bir cevap yaz. " +
        "Kurallar: (1) Sadece verilen metin parcasindaki bilgiyi kullan, uydurmak yasak. " +
        "(2) Turkce yaz, samimi ama profesyonel ton. (3) Maksimum 2-3 cumle. " +
        "(4) 'Dokumanimizda...' veya 'Bilgilerimize gore...' gibi girisler kullanabilirsin. " +
        "(5) Ic dusunceni, muhakemeni veya meta-yorumunu ASLA yazma. Sadece cevabini yaz. " +
        "(6) Musteri mesajinin dilini algilayip ayni dilde cevap ver.";

    public ChunkSummarizer(HttpClient httpClient, string apiKey, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _logger = logger;
    }

    /// <summary>
    /// Summarize a document chunk into a customer-friendly answer.
    /// Returns the summary text, or null on failure.
    /// </summary>
    public async Task<string?> SummarizeAsync(string chunkContent, string userQuery, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chunkContent) || string.IsNullOrWhiteSpace(userQuery))
            return null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeoutMs);

        try
        {
            var userMessage = $"Musteri sorusu: {userQuery}\n\nDokuman parcasi:\n{chunkContent}";

            var requestBody = new
            {
                model = Model,
                max_tokens = MaxTokens,
                system = SystemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userMessage }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, ClaudeApiUrl);
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = JsonContent.Create(requestBody);

            using var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationChunkSummarizationFailed}] Claude API HTTP {(int)response.StatusCode}: {errorBody}");
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cts.Token);
            if (json == null)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationChunkSummarizationFailed}] Claude API returned null JSON body");
                return null;
            }

            // Extract text from Claude response: content[0].text
            if (json.RootElement.TryGetProperty("content", out var contentArr)
                && contentArr.GetArrayLength() > 0)
            {
                var first = contentArr[0];
                if (first.TryGetProperty("text", out var textEl))
                {
                    var summary = textEl.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(summary))
                        return summary;
                }
            }

            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationChunkSummarizationFailed}] Claude response missing content[0].text");
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // App shutting down
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationChunkSummarizationFailed}] Chunk summarization timeout ({TimeoutMs}ms)");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationChunkSummarizationFailed}] Chunk summarization connection error: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationChunkSummarizationFailed}] Chunk summarization parse error: {ex.Message}");
            return null;
        }
    }
}
