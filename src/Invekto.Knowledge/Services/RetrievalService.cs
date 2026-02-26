using System.Diagnostics;
using Invekto.Knowledge.Data;
using Invekto.Shared.Logging;

namespace Invekto.Knowledge.Services;

/// <summary>
/// Search orchestrator: semantic search (pgvector) with keyword fallback.
/// Chooses method based on EmbeddingService availability.
/// </summary>
public sealed class RetrievalService
{
    private readonly KnowledgeRepository _repository;
    private readonly EmbeddingService _embeddingService;
    private readonly JsonLinesLogger _logger;

    public RetrievalService(
        KnowledgeRepository repository,
        EmbeddingService embeddingService,
        JsonLinesLogger logger)
    {
        _repository = repository;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<SearchResponse> SearchAsync(
        int tenantId, string query, int topK, string? lang, string? category,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Try semantic search first (FAQs + chunks)
        if (_embeddingService.IsAvailable)
        {
            try
            {
                var embedding = await _embeddingService.GetEmbeddingAsync(query, ct);
                if (embedding != null)
                {
                    var faqResults = await _repository.SemanticSearchAsync(tenantId, embedding, topK, lang, category, ct);
                    var chunkResults = await _repository.SemanticSearchChunksAsync(tenantId, embedding, topK, ct);

                    var merged = MergeResults(faqResults, chunkResults, topK);
                    sw.Stop();

                    return new SearchResponse
                    {
                        Results = merged,
                        Method = "semantic",
                        Reason = null,
                        DurationMs = (int)sw.ElapsedMilliseconds
                    };
                }

                _logger.SystemWarn("RetrievalService: Embedding generation failed, falling back to keyword search");
            }
            catch (Exception ex)
            {
                _logger.SystemWarn($"RetrievalService: Semantic search failed ({ex.Message}), falling back to keyword search");
            }
        }

        // Keyword fallback (FAQs + chunks)
        var reason = _embeddingService.IsAvailable
            ? "Embedding generation failed"
            : "OpenAI API key not configured";

        var kwFaqResults = await _repository.KeywordSearchAsync(tenantId, query, topK, lang, category, ct);
        var kwChunkResults = await _repository.KeywordSearchChunksAsync(tenantId, query, topK, ct);

        var kwMerged = MergeResults(kwFaqResults, kwChunkResults, topK);
        sw.Stop();

        return new SearchResponse
        {
            Results = kwMerged,
            Method = "keyword",
            Reason = reason,
            DurationMs = (int)sw.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Merge FAQ and chunk results by score descending, take topK.
    /// Converts ChunkSearchResultDto to SearchResultDto for unified response.
    /// </summary>
    private static List<SearchResultDto> MergeResults(
        List<SearchResultDto> faqResults, List<ChunkSearchResultDto> chunkResults, int topK)
    {
        var chunkAsSearch = chunkResults.Select(c => new SearchResultDto
        {
            SourceType = "chunk",
            ChunkId = c.ChunkId,
            Content = c.Content,
            DocumentId = c.DocumentId,
            DocumentTitle = c.DocumentTitle,
            PageNumber = c.PageNumber,
            Score = c.Score,
            Method = c.Method
        });

        return faqResults
            .Concat(chunkAsSearch)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }
}

public sealed class SearchResponse
{
    public List<SearchResultDto> Results { get; init; } = new();
    public required string Method { get; init; }
    public string? Reason { get; init; }
    public int DurationMs { get; init; }
}
