using System.Collections.Concurrent;
using Invekto.Knowledge.Data;
using Invekto.Shared.Logging;
using Pgvector;

namespace Invekto.Knowledge.Services;

/// <summary>
/// Background worker that processes uploaded PDF documents.
/// GR-2.1 Phase B: PDF -> chunk -> embed -> ready. One document at a time.
/// On startup, re-enqueues pending/processing documents (restart recovery).
/// </summary>
public sealed class DocumentProcessingService : BackgroundService
{
    private readonly ConcurrentQueue<DocumentProcessJob> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly KnowledgeRepository _repository;
    private readonly PdfChunkingService _chunkingService;
    private readonly EmbeddingService _embeddingService;
    private readonly JsonLinesLogger _logger;

    public DocumentProcessingService(
        KnowledgeRepository repository,
        PdfChunkingService chunkingService,
        EmbeddingService embeddingService,
        JsonLinesLogger logger)
    {
        _repository = repository;
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public void EnqueueDocument(DocumentProcessJob job)
    {
        _queue.Enqueue(job);
        _signal.Release();
        _logger.SystemInfo($"[DocumentProcessingService] Document enqueued: id={job.DocumentId}, tenant={job.TenantId}, title={job.Title}");
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
                    Title = doc.Title
                });
                _signal.Release();
                _logger.SystemWarn($"[DocumentProcessingService] Re-enqueued stuck document: id={doc.Id}, status={doc.Status}");
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
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _logger.SystemInfo($"[DocumentProcessingService] Processing document {job.DocumentId}: {job.Title}");

        // 1. Set status to processing
        await _repository.UpdateDocumentStatusAsync(job.TenantId, job.DocumentId, "processing", 0, ct);

        // 2. Extract text and chunk
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

        // 3. Batch insert chunks
        var chunkRows = chunkResult.Chunks.Select(c => new ChunkInsertRow
        {
            Content = c.Content,
            ChunkIndex = c.ChunkIndex,
            MetadataJson = $"{{\"page_number\":{c.PageNumber},\"token_count\":{c.TokenCount}}}"
        }).ToList();

        int inserted = await _repository.BatchInsertChunksAsync(job.TenantId, job.DocumentId, chunkRows, ct);

        // 4. Generate embeddings for each chunk (graceful - failures don't block)
        if (_embeddingService.IsAvailable)
        {
            int embedded = 0;
            int failed = 0;
            var chunksToEmbed = await _repository.GetChunksWithoutEmbeddingAsync(job.TenantId, 500, ct);

            foreach (var (chunkId, text) in chunksToEmbed)
            {
                if (ct.IsCancellationRequested) break;

                var embedding = await _embeddingService.GetEmbeddingAsync(text, ct);
                if (embedding != null)
                {
                    await _repository.UpdateChunkEmbeddingAsync(job.TenantId, chunkId, embedding, ct);
                    embedded++;
                }
                else
                {
                    failed++;
                    _logger.SystemWarn($"[DocumentProcessingService] Embedding failed for chunk {chunkId} of document {job.DocumentId}");
                }
            }

            _logger.SystemInfo($"[DocumentProcessingService] Document {job.DocumentId}: {embedded} embeddings generated, {failed} failed");
        }
        else
        {
            _logger.SystemWarn("[DocumentProcessingService] OpenAI embedding not available -- chunks stored without embeddings (keyword search only)");
        }

        // 5. Mark as ready
        await _repository.UpdateDocumentStatusAsync(job.TenantId, job.DocumentId, "ready", inserted, ct);

        sw.Stop();
        _logger.SystemInfo($"[DocumentProcessingService] Document {job.DocumentId} complete: {inserted} chunks in {sw.ElapsedMilliseconds}ms");
    }
}

public sealed class DocumentProcessJob
{
    public int TenantId { get; init; }
    public int DocumentId { get; init; }
    public required string FilePath { get; init; }
    public required string Title { get; init; }
}
