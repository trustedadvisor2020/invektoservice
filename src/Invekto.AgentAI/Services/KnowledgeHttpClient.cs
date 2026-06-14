using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.DTOs.AgentAI;
using Invekto.Shared.Logging;

namespace Invekto.AgentAI.Services;

/// <summary>
/// GR-2.2: Direct service-to-service HTTP client for Knowledge API.
/// AgentAI:7105 -> Knowledge:7104 (localhost, no Backend proxy hop).
/// Graceful degradation: returns empty results on failure.
/// </summary>
public sealed class KnowledgeHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;
    private readonly bool _enabled;

    public KnowledgeHttpClient(HttpClient httpClient, string baseUrl, int timeoutMs, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
        _logger = logger;
        _enabled = !string.IsNullOrEmpty(baseUrl);
    }

    public bool IsAvailable => _enabled;

    /// <summary>
    /// Search Knowledge service for relevant FAQs and document chunks.
    /// Returns empty result on any failure (graceful degradation).
    /// </summary>
    public async Task<KnowledgeSearchResult> SearchAsync(
        int tenantId, string query, string? lang = null, int topK = 5,
        string? jwtToken = null, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            _logger.SystemWarn("[KnowledgeHttpClient] Knowledge service URL not configured, skipping search");
            return KnowledgeSearchResult.Unavailable("Knowledge service URL not configured");
        }

        try
        {
            var requestBody = new
            {
                query,
                topK,
                lang,
                category = (string?)null
            };

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"api/v1/knowledge/{tenantId}/search");

            // TryAddWithoutValidation: a malformed jwt must not throw FormatException past the
            // graceful-degradation catch set below (would escape as an unhandled error).
            if (!string.IsNullOrEmpty(jwtToken))
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {jwtToken}");

            request.Content = JsonContent.Create(requestBody);

            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.SystemWarn($"[KnowledgeHttpClient] Search failed HTTP {(int)response.StatusCode}: {errorBody}");
                return KnowledgeSearchResult.Unavailable($"HTTP {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
            if (json == null)
            {
                _logger.SystemWarn("[KnowledgeHttpClient] Search returned null JSON body");
                return KnowledgeSearchResult.Unavailable("Null response body");
            }

            return ParseSearchResponse(json);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // App shutting down
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn("[KnowledgeHttpClient] Search timeout");
            return KnowledgeSearchResult.Unavailable("Timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[KnowledgeHttpClient] Search connection error: {ex.Message}");
            return KnowledgeSearchResult.Unavailable($"Connection error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"[KnowledgeHttpClient] Search unexpected error: {ex.Message}");
            return KnowledgeSearchResult.Unavailable($"Unexpected: {ex.Message}");
        }
    }

    private KnowledgeSearchResult ParseSearchResponse(JsonDocument json)
    {
        var root = json.RootElement;
        var results = new List<KnowledgeSourceRef>();
        var contextParts = new List<string>();

        // Single pass over the results array builds both the structured source refs and the
        // Claude prompt-injection context text (previously two separate enumerations).
        if (root.TryGetProperty("results", out var resultsArr))
        {
            foreach (var item in resultsArr.EnumerateArray())
            {
                var sourceType = item.TryGetProperty("sourceType", out var st) ? st.GetString() ?? "" : "";
                var score = item.TryGetProperty("score", out var sc) ? sc.GetDouble() : 0;

                var source = new KnowledgeSourceRef
                {
                    SourceType = sourceType,
                    Score = score
                };

                if (sourceType == "faq")
                {
                    source.FaqId = item.TryGetProperty("faqId", out var fi) && fi.ValueKind == JsonValueKind.Number
                        ? fi.GetInt32() : null;
                    var question = item.TryGetProperty("question", out var q) ? q.GetString() : null;
                    source.Title = question;

                    var answer = item.TryGetProperty("answer", out var ae) ? ae.GetString() : null;
                    if (!string.IsNullOrEmpty(question) && !string.IsNullOrEmpty(answer))
                        contextParts.Add($"FAQ: S: {question} C: {answer}");
                }
                else if (sourceType == "chunk")
                {
                    source.DocumentId = item.TryGetProperty("documentId", out var di) && di.ValueKind == JsonValueKind.Number
                        ? di.GetInt32() : null;
                    source.Title = item.TryGetProperty("documentTitle", out var dt) ? dt.GetString() : null;
                    source.PageNumber = item.TryGetProperty("pageNumber", out var pn) && pn.ValueKind == JsonValueKind.Number
                        ? pn.GetInt32() : null;

                    var content = item.TryGetProperty("content", out var ce) ? ce.GetString() : null;
                    if (!string.IsNullOrEmpty(content))
                        contextParts.Add($"Dokuman ({source.Title}): {content}");
                }

                results.Add(source);
            }
        }

        var method = root.TryGetProperty("method", out var m) ? m.GetString() : "unknown";
        var durationMs = root.TryGetProperty("durationMs", out var d) ? d.GetInt32() : 0;

        return new KnowledgeSearchResult
        {
            Available = true,
            Sources = results,
            ContextText = contextParts.Count > 0 ? string.Join("\n\n", contextParts) : null,
            Method = method ?? "unknown",
            DurationMs = durationMs
        };
    }
}

/// <summary>
/// Result from Knowledge service search.
/// </summary>
public sealed class KnowledgeSearchResult
{
    public bool Available { get; init; }
    public string? UnavailableReason { get; init; }
    public List<KnowledgeSourceRef> Sources { get; init; } = new();
    public string? ContextText { get; init; }
    public string Method { get; init; } = "unknown";
    public int DurationMs { get; init; }

    public static KnowledgeSearchResult Unavailable(string reason) => new()
    {
        Available = false,
        UnavailableReason = reason,
        Sources = new List<KnowledgeSourceRef>(),
        ContextText = null,
        Method = "none",
        DurationMs = 0
    };
}
