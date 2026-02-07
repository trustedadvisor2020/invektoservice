using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.Logging;

namespace Invekto.ChatAnalysis.Services;

/// <summary>
/// Service for sending analysis results to callback URLs
/// Implements 3x retry with exponential backoff
/// </summary>
public sealed class CallbackService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;
    private readonly string _authToken;
    private readonly JsonSerializerOptions _jsonOptions;

    private const int MaxRetries = 3;
    private const int BaseDelayMs = 1000; // 1s, 2s, 4s backoff
    private const int DefaultTimeoutSeconds = 30;
    private const int LastAttemptTimeoutSeconds = 60;

    public CallbackService(string authToken, JsonLinesLogger logger)
    {
        _authToken = authToken ?? throw new ArgumentNullException(nameof(authToken));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // HttpClient timeout will be set per-request
        _httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan // We control timeout per-request
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // Keep PascalCase
            WriteIndented = false
        };
    }

    /// <summary>
    /// Send callback to the specified URL with retry logic
    /// </summary>
    /// <typeparam name="T">Response type</typeparam>
    /// <param name="callbackUrl">Full callback URL</param>
    /// <param name="payload">Response payload</param>
    /// <param name="requestId">Request ID for logging</param>
    /// <returns>True if callback succeeded, false otherwise</returns>
    public async Task<bool> SendCallbackAsync<T>(string callbackUrl, T payload, string requestId)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            // Last attempt gets longer timeout (60s vs 30s)
            var timeoutSeconds = attempt == MaxRetries ? LastAttemptTimeoutSeconds : DefaultTimeoutSeconds;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, callbackUrl);
                request.Headers.Add("Authorization", $"Bearer {_authToken}");
                request.Content = JsonContent.Create(payload, options: _jsonOptions);

                var response = await _httpClient.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    _logger.StepInfo($"Callback başarılı (attempt {attempt})", requestId);
                    return true;
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.StepWarn($"Callback HTTP {(int)response.StatusCode}, attempt {attempt}/{MaxRetries}: {Truncate(errorBody, 200)}", requestId);

                // Retry on ALL errors (4xx and 5xx) - removed 4xx early exit
            }
            catch (OperationCanceledException)
            {
                _logger.StepWarn($"Callback timeout ({timeoutSeconds}s), attempt {attempt}/{MaxRetries}", requestId);
            }
            catch (HttpRequestException ex)
            {
                _logger.StepWarn($"Callback bağlantı hatası, attempt {attempt}/{MaxRetries}: {ex.Message}", requestId);
            }
            catch (Exception ex)
            {
                _logger.StepError($"Callback beklenmeyen hata, attempt {attempt}/{MaxRetries}: {ex.Message}", requestId);
            }

            // Wait before retry (exponential backoff: 1s, 2s, 4s)
            if (attempt < MaxRetries)
            {
                var delay = BaseDelayMs * (int)Math.Pow(2, attempt - 1);
                await Task.Delay(delay);
            }
        }

        _logger.StepError($"Callback {MaxRetries} denemede başarısız: {callbackUrl}", requestId);
        return false;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
