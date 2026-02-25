using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// HTTP client for Knowledge service semantic search API.
/// Called by AiFaqHandler to search FAQs + document chunks via pgvector.
/// Graceful degradation: returns unavailable result on any failure.
/// Pattern: same as KnowledgeIntentClient (typed HttpClient, 3s timeout).
/// </summary>
public sealed class KnowledgeSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;

    public KnowledgeSearchClient(HttpClient httpClient, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Search Knowledge service for relevant FAQs and document chunks.
    /// Returns KnowledgeFaqSearchResult with parsed results or unavailable fallback.
    /// </summary>
    public async Task<KnowledgeFaqSearchResult> SearchAsync(
        int tenantId, string query, int topK, string? searchSource,
        string jwtToken, CancellationToken ct = default)
    {
        try
        {
            var requestBody = new
            {
                query,
                topK,
                lang = (string?)null,
                category = (string?)null
            };

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"/api/v1/knowledge/{tenantId}/search");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("N"));
            request.Content = JsonContent.Create(requestBody);

            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationKnowledgeSearchFailed}] Knowledge search HTTP {(int)response.StatusCode} for tenant {tenantId}: {errorBody}");
                return KnowledgeFaqSearchResult.Unavailable($"HTTP {(int)response.StatusCode}");
            }

            using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
            if (json == null)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationKnowledgeSearchFailed}] Knowledge search null response for tenant {tenantId}");
                return KnowledgeFaqSearchResult.Unavailable("Null response body");
            }

            return ParseSearchResponse(json, searchSource);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // App shutting down — propagate
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationKnowledgeSearchFailed}] Knowledge search timeout for tenant {tenantId}");
            return KnowledgeFaqSearchResult.Unavailable("Timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationKnowledgeSearchFailed}] Knowledge search connection error for tenant {tenantId}: {ex.Message}");
            return KnowledgeFaqSearchResult.Unavailable($"Connection error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationKnowledgeSearchFailed}] Knowledge search parse error for tenant {tenantId}: {ex.Message}");
            return KnowledgeFaqSearchResult.Unavailable($"Parse error: {ex.Message}");
        }
    }

    private static KnowledgeFaqSearchResult ParseSearchResponse(JsonDocument json, string? searchSource)
    {
        var root = json.RootElement;
        var items = new List<KnowledgeFaqSearchItem>();

        if (!root.TryGetProperty("results", out var resultsArr))
            return new KnowledgeFaqSearchResult { Available = true, Items = items };

        foreach (var item in resultsArr.EnumerateArray())
        {
            var sourceType = item.TryGetProperty("sourceType", out var st) ? st.GetString() ?? "" : "";
            var score = item.TryGetProperty("score", out var sc) ? sc.GetDouble() : 0;

            // Filter by search_source: "faq_only" skips chunks
            if (searchSource == "faq_only" && sourceType != "faq")
                continue;

            if (sourceType == "faq")
            {
                var question = item.TryGetProperty("question", out var q) ? q.GetString() : null;
                var answer = item.TryGetProperty("answer", out var a) ? a.GetString() : null;

                if (!string.IsNullOrEmpty(answer))
                {
                    items.Add(new KnowledgeFaqSearchItem
                    {
                        SourceType = "faq",
                        Score = score,
                        Answer = answer,
                        Question = question ?? "",
                        ChunkContent = null,
                        DocumentTitle = null,
                        PageNumber = null
                    });
                }
            }
            else if (sourceType == "chunk")
            {
                var content = item.TryGetProperty("content", out var c) ? c.GetString() : null;
                var docTitle = item.TryGetProperty("documentTitle", out var dt) ? dt.GetString() : null;
                var pageNum = item.TryGetProperty("pageNumber", out var pn) && pn.ValueKind == JsonValueKind.Number
                    ? (int?)pn.GetInt32() : null;

                if (!string.IsNullOrEmpty(content))
                {
                    items.Add(new KnowledgeFaqSearchItem
                    {
                        SourceType = "chunk",
                        Score = score,
                        Answer = null,
                        Question = null,
                        ChunkContent = content,
                        DocumentTitle = docTitle ?? "Dokuman",
                        PageNumber = pageNum
                    });
                }
            }
        }

        var method = root.TryGetProperty("method", out var m) ? m.GetString() ?? "unknown" : "unknown";
        var durationMs = root.TryGetProperty("durationMs", out var d) ? d.GetInt32() : 0;

        return new KnowledgeFaqSearchResult
        {
            Available = true,
            Items = items,
            Method = method,
            DurationMs = durationMs
        };
    }
}

/// <summary>
/// Result from Knowledge service search, tailored for ai_faq node usage.
/// </summary>
public sealed class KnowledgeFaqSearchResult
{
    public bool Available { get; init; }
    public string? UnavailableReason { get; init; }
    public List<KnowledgeFaqSearchItem> Items { get; init; } = new();
    public string Method { get; init; } = "unknown";
    public int DurationMs { get; init; }

    public static KnowledgeFaqSearchResult Unavailable(string reason) => new()
    {
        Available = false,
        UnavailableReason = reason,
        Items = new List<KnowledgeFaqSearchItem>()
    };
}

/// <summary>
/// Single search result item — either FAQ (has Answer) or chunk (has ChunkContent).
/// </summary>
public sealed class KnowledgeFaqSearchItem
{
    public required string SourceType { get; init; }
    public double Score { get; init; }

    // FAQ fields
    public string? Answer { get; init; }
    public string? Question { get; init; }

    // Chunk fields
    public string? ChunkContent { get; init; }
    public string? DocumentTitle { get; init; }
    public int? PageNumber { get; init; }
}
