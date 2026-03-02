using System.Collections.Concurrent;
using System.Text.Json;
using Invekto.Knowledge.Data;
using Invekto.Shared.Logging;
using Pgvector;

namespace Invekto.Knowledge.Services;

/// <summary>
/// Background worker that processes uploaded documents (PDF + website).
/// GR-2.1 Phase B: source -> chunk -> embed -> ready. One document at a time.
/// On startup, re-enqueues pending/processing documents (restart recovery).
/// </summary>
public sealed class DocumentProcessingService : BackgroundService
{
    private readonly ConcurrentQueue<DocumentProcessJob> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly KnowledgeRepository _repository;
    private readonly PdfChunkingService _chunkingService;
    private readonly WebScrapingService _scrapingService;
    private readonly EmbeddingService _embeddingService;
    private readonly JsonLinesLogger _logger;

    public DocumentProcessingService(
        KnowledgeRepository repository,
        PdfChunkingService chunkingService,
        WebScrapingService scrapingService,
        EmbeddingService embeddingService,
        JsonLinesLogger logger)
    {
        _repository = repository;
        _chunkingService = chunkingService;
        _scrapingService = scrapingService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public void EnqueueDocument(DocumentProcessJob job)
    {
        _queue.Enqueue(job);
        _signal.Release();
        _logger.SystemInfo($"[DocumentProcessingService] Document enqueued: id={job.DocumentId}, tenant={job.TenantId}, title={job.Title}, type={job.SourceType}");
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Re-enqueue documents stuck in pending/processing (restart recovery)
        try
        {
            var stuckDocs = await _repository.GetStuckDocumentsAsync(cancellationToken);
            foreach (var doc in stuckDocs)
            {
                _queue.Enqueue(new DocumentProcessJob
                {
                    TenantId = doc.TenantId,
                    DocumentId = doc.Id,
                    FilePath = doc.FilePath ?? "",
                    Title = doc.Title,
                    SourceType = doc.SourceType
                });
                _signal.Release();
                _logger.SystemWarn($"[DocumentProcessingService] Re-enqueued stuck document: id={doc.Id}, status={doc.Status}, type={doc.SourceType}");
            }
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"[DocumentProcessingService] Failed to recover stuck documents on startup: {ex.Message}");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.SystemInfo("[DocumentProcessingService] Background document processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (_queue.TryDequeue(out var job))
            {
                try
                {
                    await ProcessDocumentAsync(job, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.SystemError($"[DocumentProcessingService] Fatal error processing document {job.DocumentId}: {ex.Message}");
                    try
                    {
                        await _repository.UpdateDocumentStatusAsync(
                            job.TenantId, job.DocumentId, "error", 0, stoppingToken);
                    }
                    catch (Exception dbEx)
                    {
                        _logger.SystemError($"[DocumentProcessingService] Failed to set error status for document {job.DocumentId}: {dbEx.Message}");
                    }
                }
            }
        }

        _logger.SystemInfo("[DocumentProcessingService] Background document processor stopped");
    }

    private async Task ProcessDocumentAsync(DocumentProcessJob job, CancellationToken ct)
    {
        await _repository.UpdateDocumentStatusAsync(job.TenantId, job.DocumentId, "processing", 0, ct);

        if (job.SourceType == "website")
            await ProcessWebsiteAsync(job, ct);
        else
            await ProcessPdfAsync(job, ct);
    }

    private async Task ProcessPdfAsync(DocumentProcessJob job, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _logger.SystemInfo($"[DocumentProcessingService] Processing PDF {job.DocumentId}: {job.Title}");

        // Validate file exists
        if (string.IsNullOrEmpty(job.FilePath) || !File.Exists(job.FilePath))
        {
            _logger.SystemError($"[DocumentProcessingService] File not found for document {job.DocumentId}: {job.FilePath}");
            await _repository.UpdateDocumentStatusAsync(job.TenantId, job.DocumentId, "error", 0, ct);
            return;
        }

        PdfChunkingResult chunkResult;
        try
        {
            chunkResult = _chunkingService.ProcessPdf(job.FilePath);
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn($"[DocumentProcessingService] No text extracted from document {job.DocumentId}: {ex.Message}");
            await _repository.UpdateDocumentStatusAsync(job.TenantId, job.DocumentId, "error", 0, ct);
            return;
        }
        catch (Exception ex)
        {
            _logger.SystemError($"[DocumentProcessingService] PDF extraction failed for document {job.DocumentId}: {ex.Message}");
            await _repository.UpdateDocumentStatusAsync(job.TenantId, job.DocumentId, "error", 0, ct);
            return;
        }

        _logger.SystemInfo($"[DocumentProcessingService] Document {job.DocumentId}: {chunkResult.TotalPages} pages, {chunkResult.TotalChunks} chunks");

        // Batch insert chunks
        var chunkRows = chunkResult.Chunks.Select(c => new ChunkInsertRow
        {
            Content = c.Content,
            ChunkIndex = c.ChunkIndex,
            MetadataJson = $"{{\"page_number\":{c.PageNumber},\"token_count\":{c.TokenCount}}}"
        }).ToList();

        int inserted = await _repository.BatchInsertChunksAsync(job.TenantId, job.DocumentId, chunkRows, ct);

        // Generate embeddings
        await GenerateEmbeddingsAsync(job, ct);

        await _repository.UpdateDocumentStatusAsync(job.TenantId, job.DocumentId, "ready", inserted, ct);

        sw.Stop();
        _logger.SystemInfo($"[DocumentProcessingService] PDF {job.DocumentId} complete: {inserted} chunks in {sw.ElapsedMilliseconds}ms");
    }

    private async Task ProcessWebsiteAsync(DocumentProcessJob job, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _logger.SystemInfo($"[DocumentProcessingService] Website crawl starting: id={job.DocumentId}, url={job.FilePath}");

        WebCrawlResult crawlResult;
        try
        {
            crawlResult = await _scrapingService.CrawlAsync(job.FilePath, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemError($"[DocumentProcessingService] Website crawl HTTP error for doc {job.DocumentId}: {ex.Message}");
            await _repository.UpdateDocumentStatusAsync(job.TenantId, job.DocumentId, "error", 0, ct);
            return;
        }
        catch (OperationCanceledException)
        {
            _logger.SystemError($"[DocumentProcessingService] Website crawl cancelled/timed out for doc {job.DocumentId}");
            await _repository.UpdateDocumentStatusAsync(job.TenantId, job.DocumentId, "error", 0, ct);
            return;
        }

        if (crawlResult.TotalChunks == 0)
        {
            _logger.SystemWarn($"[DocumentProcessingService] Website doc {job.DocumentId}: no content extracted (0 chunks)");
            await _repository.UpdateDocumentStatusAsync(job.TenantId, job.DocumentId, "error", 0, ct);
            return;
        }

        // Flatten all page chunks into ChunkInsertRows with per-page URL metadata
        var chunkRows = new List<ChunkInsertRow>();
        int globalIndex = 0;
        foreach (var page in crawlResult.Pages)
        {
            foreach (var chunk in page.Chunks)
            {
                var urlJson = JsonSerializer.Serialize(page.Url);
                var titleJson = JsonSerializer.Serialize(page.PageTitle);
                chunkRows.Add(new ChunkInsertRow
                {
                    Content = chunk.Content,
                    ChunkIndex = globalIndex++,
                    MetadataJson = $"{{\"url\":{urlJson},\"page_title\":{titleJson},\"token_count\":{chunk.TokenCount}}}"
                });
            }
        }

        int inserted = await _repository.BatchInsertChunksAsync(job.TenantId, job.DocumentId, chunkRows, ct);

        // Generate embeddings (loop for 500+ chunks)
        await GenerateEmbeddingsAsync(job, ct);

        await _repository.UpdateDocumentStatusAsync(job.TenantId, job.DocumentId, "ready", inserted, ct);

        sw.Stop();
        _logger.SystemInfo($"[DocumentProcessingService] Website doc {job.DocumentId} complete: {crawlResult.PagesScraped} pages, {inserted} chunks, {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Generate embeddings for all un-embedded chunks of the tenant.
    /// Loops in batches of 500 to handle documents with many chunks.
    /// </summary>
    private async Task GenerateEmbeddingsAsync(DocumentProcessJob job, CancellationToken ct)
    {
        if (!_embeddingService.IsAvailable)
        {
            _logger.SystemWarn("[DocumentProcessingService] OpenAI embedding not available -- chunks stored without embeddings (keyword search only)");
            return;
        }

        int totalEmbedded = 0;
        int totalFailed = 0;

        while (true)
        {
            var chunksToEmbed = await _repository.GetChunksWithoutEmbeddingAsync(job.TenantId, 500, ct);
            if (chunksToEmbed.Count == 0) break;

            var embeddingUpdates = new List<(long ChunkId, Pgvector.Vector Embedding)>();

            foreach (var (chunkId, text) in chunksToEmbed)
            {
                if (ct.IsCancellationRequested) break;

                var embedding = await _embeddingService.GetEmbeddingAsync(text, ct);
                if (embedding != null)
                {
                    embeddingUpdates.Add((chunkId, embedding));
                }
                else
                {
                    totalFailed++;
                    _logger.SystemWarn($"[DocumentProcessingService] Embedding failed for chunk {chunkId} of document {job.DocumentId}");
                }
            }

            var embedded = await _repository.BatchUpdateChunkEmbeddingsAsync(job.TenantId, embeddingUpdates, ct);
            totalEmbedded += embedded;

            // If no embeddings succeeded in this batch, stop to prevent infinite loop
            // (failed chunks remain without embeddings — keyword search still works)
            if (embedded == 0)
            {
                _logger.SystemWarn($"[DocumentProcessingService] Document {job.DocumentId}: embedding batch produced 0 results, stopping loop ({totalFailed} total failed)");
                break;
            }
        }

        _logger.SystemInfo($"[DocumentProcessingService] Document {job.DocumentId}: {totalEmbedded} embeddings generated, {totalFailed} failed");
    }
}

public sealed class DocumentProcessJob
{
    public int TenantId { get; init; }
    public int DocumentId { get; init; }
    public required string FilePath { get; init; }
    public required string Title { get; init; }
    public string SourceType { get; init; } = "pdf";
}
