using UglyToad.PdfPig;
using Invekto.Shared.Logging;

namespace Invekto.Knowledge.Services;

/// <summary>
/// Extracts text from PDFs using PdfPig and splits into overlapping chunks.
/// GR-2.1 Phase B: 512-token chunks with 50-token overlap. Thread-safe (stateless).
/// </summary>
public sealed class PdfChunkingService
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    private readonly JsonLinesLogger _logger;

    public PdfChunkingService(int chunkSize, int chunkOverlap, JsonLinesLogger logger)
    {
        _chunkSize = chunkSize > 0 ? chunkSize : 512;
        _chunkOverlap = chunkOverlap >= 0 ? chunkOverlap : 50;
        _logger = logger;
    }

    /// <summary>
    /// Extract text from PDF and split into overlapping chunks with page metadata.
    /// </summary>
    public PdfChunkingResult ProcessPdf(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"PDF file not found: {filePath}");

        var pageTexts = new List<(int PageNumber, string Text)>();

        using (var document = PdfDocument.Open(filePath))
        {
            for (int i = 0; i < document.NumberOfPages; i++)
            {
                var page = document.GetPage(i + 1);
                var text = page.Text?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.SystemWarn($"[PdfChunkingService] Page {i + 1} has no extractable text (may be scanned/image-only)");
                    continue;
                }

                pageTexts.Add((i + 1, text));
            }
        }

        if (pageTexts.Count == 0)
            throw new InvalidOperationException("No extractable text found in PDF. The file may contain only images or scanned content.");

        var chunks = BuildChunks(pageTexts);

        return new PdfChunkingResult
        {
            TotalPages = pageTexts.Count,
            TotalChunks = chunks.Count,
            Chunks = chunks
        };
    }

    /// <summary>
    /// Build overlapping token chunks from page texts, tracking source page numbers.
    /// </summary>
    private List<ChunkResult> BuildChunks(List<(int PageNumber, string Text)> pageTexts)
    {
        // Flatten all words with page number tracking
        var words = new List<(string Word, int PageNumber)>();
        foreach (var (pageNumber, text) in pageTexts)
        {
            var pageWords = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in pageWords)
            {
                words.Add((word, pageNumber));
            }
        }

        if (words.Count == 0)
            return new List<ChunkResult>();

        var chunks = new List<ChunkResult>();
        int chunkIndex = 0;
        int pos = 0;

        while (pos < words.Count)
        {
            int end = Math.Min(pos + _chunkSize, words.Count);
            var chunkWords = words.GetRange(pos, end - pos);

            var content = string.Join(" ", chunkWords.Select(w => w.Word));
            var pageNumber = chunkWords[0].PageNumber;

            chunks.Add(new ChunkResult
            {
                Content = content,
                ChunkIndex = chunkIndex,
                PageNumber = pageNumber,
                TokenCount = chunkWords.Count
            });

            chunkIndex++;

            // Advance by (chunkSize - overlap) to create overlapping windows
            int step = _chunkSize - _chunkOverlap;
            if (step <= 0) step = 1; // safety: prevent infinite loop
            pos += step;
        }

        return chunks;
    }
}

public sealed class PdfChunkingResult
{
    public int TotalPages { get; init; }
    public int TotalChunks { get; init; }
    public required List<ChunkResult> Chunks { get; init; }
}

public sealed class ChunkResult
{
    public required string Content { get; init; }
    public int ChunkIndex { get; init; }
    public int PageNumber { get; init; }
    public int TokenCount { get; init; }
}
