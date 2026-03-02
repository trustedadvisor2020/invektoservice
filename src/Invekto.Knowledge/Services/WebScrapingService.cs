using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;
using Invekto.Shared.Logging;

namespace Invekto.Knowledge.Services;

/// <summary>
/// Website crawling: sitemap discovery, HTML scraping, text extraction, chunking.
/// GR-2.1 Website Indexing: one document per website submission.
/// Thread-safe (stateless). Uses same 512w/50w overlap as PdfChunkingService.
/// </summary>
public sealed class WebScrapingService
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    private readonly int _maxPages;
    private readonly int _pageTimeoutMs;
    private readonly int _delayBetweenRequestsMs;
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;

    private static readonly HashSet<string> NoiseTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "nav", "footer", "header", "aside", "noscript", "svg", "iframe"
    };

    public WebScrapingService(
        HttpClient httpClient,
        JsonLinesLogger logger,
        int chunkSize,
        int chunkOverlap,
        int maxPages,
        int pageTimeoutMs,
        int delayBetweenRequestsMs)
    {
        _httpClient = httpClient;
        _logger = logger;
        _chunkSize = chunkSize > 0 ? chunkSize : 512;
        _chunkOverlap = chunkOverlap >= 0 ? chunkOverlap : 50;
        _maxPages = maxPages > 0 ? maxPages : 200;
        _pageTimeoutMs = pageTimeoutMs > 0 ? pageTimeoutMs : 15000;
        _delayBetweenRequestsMs = delayBetweenRequestsMs >= 0 ? delayBetweenRequestsMs : 500;
    }

    /// <summary>
    /// Crawl a website: discover pages via sitemap, scrape each, extract text, chunk.
    /// </summary>
    public async Task<WebCrawlResult> CrawlAsync(string baseUrl, CancellationToken ct)
    {
        var normalizedBase = baseUrl.TrimEnd('/');

        // 1. Discover pages via sitemap
        var urls = await FetchSitemapUrlsAsync(normalizedBase, ct);

        if (urls.Count == 0)
        {
            _logger.SystemWarn($"[WebScrapingService] No sitemap URLs found for {normalizedBase}, falling back to root URL");
            urls.Add(normalizedBase);
        }

        // 2. Apply max pages cap
        if (urls.Count > _maxPages)
        {
            _logger.SystemInfo($"[WebScrapingService] Capping {urls.Count} URLs to {_maxPages}");
            urls = urls.Take(_maxPages).ToList();
        }

        // 3. Filter by robots.txt disallow rules
        var disallowedPaths = await FetchRobotsDisallowAsync(normalizedBase, ct);
        if (disallowedPaths.Count > 0)
        {
            var before = urls.Count;
            urls = urls.Where(u =>
            {
                if (!Uri.TryCreate(u, UriKind.Absolute, out var uri)) return false;
                var path = uri.AbsolutePath;
                return !disallowedPaths.Any(d => path.StartsWith(d, StringComparison.OrdinalIgnoreCase));
            }).ToList();
            _logger.SystemInfo($"[WebScrapingService] robots.txt filtered {before - urls.Count} URLs");
        }

        _logger.SystemInfo($"[WebScrapingService] Crawling {urls.Count} URLs from {normalizedBase}");

        // 4. Scrape each URL
        var pages = new List<WebPageChunks>();
        int skipped = 0;

        for (int i = 0; i < urls.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var scraped = await ScrapePageAsync(urls[i], ct);
            if (scraped == null)
            {
                skipped++;
                continue;
            }

            // 5. Chunk extracted text
            var chunks = BuildChunks(scraped.CleanText);
            if (chunks.Count == 0)
            {
                skipped++;
                continue;
            }

            pages.Add(new WebPageChunks
            {
                Url = scraped.Url,
                PageTitle = scraped.PageTitle,
                Chunks = chunks
            });

            // Rate limiting between requests
            if (i < urls.Count - 1 && _delayBetweenRequestsMs > 0)
                await Task.Delay(_delayBetweenRequestsMs, ct);
        }

        var totalChunks = pages.Sum(p => p.Chunks.Count);
        _logger.SystemInfo($"[WebScrapingService] Crawl complete: {pages.Count} pages scraped, {skipped} skipped, {totalChunks} chunks");

        return new WebCrawlResult
        {
            PagesScraped = pages.Count,
            PagesSkipped = skipped,
            TotalChunks = totalChunks,
            Pages = pages
        };
    }

    /// <summary>
    /// Parse sitemap.xml (and sitemap index) to discover page URLs.
    /// </summary>
    private async Task<List<string>> FetchSitemapUrlsAsync(string baseUrl, CancellationToken ct)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Try sitemap.xml first, then sitemap_index.xml
        var sitemapCandidates = new[] { $"{baseUrl}/sitemap.xml", $"{baseUrl}/sitemap_index.xml" };

        foreach (var sitemapUrl in sitemapCandidates)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_pageTimeoutMs);

                var response = await _httpClient.GetAsync(sitemapUrl, cts.Token);
                if (!response.IsSuccessStatusCode) continue;

                var xml = await response.Content.ReadAsStringAsync(cts.Token);
                var doc = XDocument.Parse(xml);
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                // Check if sitemap index
                if (doc.Root?.Name.LocalName == "sitemapindex")
                {
                    var nestedSitemapUrls = doc.Descendants(ns + "loc").Select(e => e.Value.Trim()).ToList();
                    _logger.SystemInfo($"[WebScrapingService] Sitemap index found with {nestedSitemapUrls.Count} nested sitemaps");

                    foreach (var nestedUrl in nestedSitemapUrls)
                    {
                        try
                        {
                            using var nestedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            nestedCts.CancelAfter(_pageTimeoutMs);

                            var nestedResponse = await _httpClient.GetAsync(nestedUrl, nestedCts.Token);
                            if (!nestedResponse.IsSuccessStatusCode) continue;

                            var nestedXml = await nestedResponse.Content.ReadAsStringAsync(nestedCts.Token);
                            var nestedDoc = XDocument.Parse(nestedXml);
                            var nestedNs = nestedDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                            foreach (var loc in nestedDoc.Descendants(nestedNs + "loc"))
                                urls.Add(loc.Value.Trim());
                        }
                        catch (Exception ex)
                        {
                            _logger.SystemWarn($"[WebScrapingService] Failed to fetch nested sitemap {nestedUrl}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // Standard urlset sitemap
                    foreach (var loc in doc.Descendants(ns + "loc"))
                        urls.Add(loc.Value.Trim());
                }

                if (urls.Count > 0)
                {
                    _logger.SystemInfo($"[WebScrapingService] Sitemap parsed: {urls.Count} URLs from {sitemapUrl}");
                    break; // Found a working sitemap
                }
            }
            catch (Exception ex)
            {
                _logger.SystemWarn($"[WebScrapingService] Failed to parse {sitemapUrl}: {ex.Message}");
            }
        }

        // Filter to same-origin only
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            urls.RemoveWhere(u =>
                !Uri.TryCreate(u, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase));
        }

        return urls.ToList();
    }

    /// <summary>
    /// Fetch robots.txt and extract Disallow paths for our user-agent.
    /// </summary>
    private async Task<List<string>> FetchRobotsDisallowAsync(string baseUrl, CancellationToken ct)
    {
        var disallowed = new List<string>();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(5000);

            var response = await _httpClient.GetAsync($"{baseUrl}/robots.txt", cts.Token);
            if (!response.IsSuccessStatusCode) return disallowed;

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            bool inRelevantAgent = false;

            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
                {
                    var agent = line["User-agent:".Length..].Trim();
                    inRelevantAgent = agent == "*" ||
                                     agent.Equals("InvektoBot", StringComparison.OrdinalIgnoreCase);
                }
                else if (inRelevantAgent && line.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                {
                    var path = line["Disallow:".Length..].Trim();
                    if (!string.IsNullOrEmpty(path))
                        disallowed.Add(path);
                }
            }
        }
        catch
        {
            // robots.txt fetch failure is non-fatal
        }

        return disallowed;
    }

    /// <summary>
    /// Scrape a single page: fetch HTML, extract clean text content.
    /// </summary>
    private async Task<ScrapedPage?> ScrapePageAsync(string url, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_pageTimeoutMs);

            var response = await _httpClient.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemWarn($"[WebScrapingService] HTTP {(int)response.StatusCode} for {url}");
                return null;
            }

            // Only process HTML content
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                return null;

            var html = await response.Content.ReadAsStringAsync(cts.Token);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove noise nodes
            RemoveNoiseNodes(doc);

            // Extract title
            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            var pageTitle = titleNode?.InnerText.Trim() ?? url;

            // Prefer main/article content, fallback to body
            var contentNode = doc.DocumentNode.SelectSingleNode("//main")
                              ?? doc.DocumentNode.SelectSingleNode("//article")
                              ?? doc.DocumentNode.SelectSingleNode("//body");

            if (contentNode == null)
                return null;

            var rawText = WebUtility.HtmlDecode(contentNode.InnerText);

            // Normalize whitespace: collapse multiple spaces/newlines to single space
            var cleanText = NormalizeWhitespace(rawText);

            // Skip pages with too little content
            if (cleanText.Length < 100)
                return null;

            return new ScrapedPage
            {
                Url = url,
                PageTitle = pageTitle,
                CleanText = cleanText
            };
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn($"[WebScrapingService] Timeout scraping {url}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"[WebScrapingService] Error scraping {url}: {ex.Message}");
            return null;
        }
    }

    private static void RemoveNoiseNodes(HtmlDocument doc)
    {
        var nodesToRemove = new List<HtmlNode>();

        foreach (var node in doc.DocumentNode.DescendantsAndSelf())
        {
            if (NoiseTags.Contains(node.Name))
                nodesToRemove.Add(node);
        }

        // Also remove cookie/popup related elements by class
        var cookieNodes = doc.DocumentNode.SelectNodes(
            "//*[contains(@class,'cookie') or contains(@class,'popup') or contains(@class,'modal') or contains(@id,'cookie') or contains(@id,'popup')]");
        if (cookieNodes != null)
            nodesToRemove.AddRange(cookieNodes);

        foreach (var node in nodesToRemove)
        {
            node.Remove();
        }
    }

    private static string NormalizeWhitespace(string text)
    {
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Build overlapping word-based chunks from clean text.
    /// Same algorithm as PdfChunkingService.BuildChunks but without page tracking.
    /// </summary>
    private List<ChunkResult> BuildChunks(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return new List<ChunkResult>();

        var chunks = new List<ChunkResult>();
        int chunkIndex = 0;
        int pos = 0;

        while (pos < words.Length)
        {
            int end = Math.Min(pos + _chunkSize, words.Length);
            var chunkWords = words[pos..end];

            var content = string.Join(" ", chunkWords);

            chunks.Add(new ChunkResult
            {
                Content = content,
                ChunkIndex = chunkIndex,
                PageNumber = 0, // Not applicable for website chunks
                TokenCount = chunkWords.Length
            });

            chunkIndex++;

            int step = _chunkSize - _chunkOverlap;
            if (step <= 0) step = 1;
            pos += step;
        }

        return chunks;
    }
}

// ── Result types ──

public sealed class WebCrawlResult
{
    public int PagesScraped { get; init; }
    public int PagesSkipped { get; init; }
    public int TotalChunks { get; init; }
    public required List<WebPageChunks> Pages { get; init; }
}

public sealed class WebPageChunks
{
    public required string Url { get; init; }
    public required string PageTitle { get; init; }
    public required List<ChunkResult> Chunks { get; init; }
}

internal sealed class ScrapedPage
{
    public required string Url { get; init; }
    public required string PageTitle { get; init; }
    public required string CleanText { get; init; }
}
