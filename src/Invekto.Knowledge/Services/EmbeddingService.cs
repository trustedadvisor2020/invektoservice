using System.Net.Http.Json;
using System.Text.Json;
using Pgvector;
using Invekto.Shared.Logging;

namespace Invekto.Knowledge.Services;

/// <summary>
/// OpenAI embedding generation with graceful degradation.
/// Returns null when API key is missing/invalid, allowing fallback to keyword search.
/// </summary>
public sealed class EmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _dimensions;
    private readonly int _maxRetries;

    private const string OpenAIEmbeddingsUrl = "https://api.openai.com/v1/embeddings";

    public EmbeddingService(
        HttpClient httpClient,
        JsonLinesLogger logger,
        string apiKey,
        string model,
        int dimensions,
        int maxRetries)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey;
        _model = model;
        _dimensions = dimensions;
        _maxRetries = maxRetries;
    }

    /// <summary>
    /// Returns true if OpenAI API key is configured and usable.
    /// </summary>
    public bool IsAvailable =>
        !string.IsNullOrEmpty(_apiKey) &&
        _apiKey != "REPLACE_WITH_OPENAI_KEY" &&
        !_apiKey.StartsWith("REPLACE_");

    /// <summary>
    /// Generate embedding for text. Returns null on failure (graceful degradation).
    /// </summary>
    public async Task<Vector?> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            _logger.SystemWarn("EmbeddingService: OpenAI API key not configured -- semantic search disabled");
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.SystemWarn("EmbeddingService: Empty text provided for embedding");
            return null;
        }

        // Truncate very long texts (OpenAI limit is 8191 tokens, ~32K chars safe estimate)
        if (text.Length > 30000)
            text = text[..30000];

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, OpenAIEmbeddingsUrl);
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");
                request.Content = JsonContent.Create(new
                {
                    model = _model,
                    input = text,
                    dimensions = _dimensions
                });

                using var response = await _httpClient.SendAsync(request, ct);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.SystemWarn($"EmbeddingService: OpenAI rate limit (attempt {attempt + 1}/{_maxRetries + 1})");
                    if (attempt < _maxRetries)
                    {
                        await Task.Delay(1000 * (attempt + 1), ct);
                        continue;
                    }
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.SystemWarn($"EmbeddingService: OpenAI error {response.StatusCode}: {errorBody}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var doc = JsonDocument.Parse(json);

                var embeddingArray = doc.RootElement
                    .GetProperty("data")[0]
                    .GetProperty("embedding");

                var values = new float[_dimensions];
                var i = 0;
                foreach (var val in embeddingArray.EnumerateArray())
                {
                    if (i >= _dimensions) break;
                    values[i++] = val.GetSingle();
                }

                return new Vector(values);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.SystemWarn($"EmbeddingService: OpenAI timeout (attempt {attempt + 1}/{_maxRetries + 1})");
                if (attempt < _maxRetries)
                    continue;
                return null;
            }
            catch (TaskCanceledException)
            {
                // Cancellation requested by caller -- propagate
                throw;
            }
            catch (JsonException ex)
            {
                _logger.SystemWarn($"EmbeddingService: Failed to parse OpenAI response: {ex.Message}");
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.SystemWarn($"EmbeddingService: HTTP error: {ex.Message}");
                if (attempt < _maxRetries)
                {
                    await Task.Delay(1000 * (attempt + 1), ct);
                    continue;
                }
                return null;
            }
        }

        return null;
    }
}
