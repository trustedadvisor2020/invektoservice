using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Integration;
using Invekto.Shared.Logging;

namespace Invekto.Shared.Integration;

/// <summary>
/// Sends async callback results to Main App after processing webhook events.
/// GR-1.9: 3x retry with exponential backoff. Thread-safe, register as singleton.
/// </summary>
public sealed class MainAppCallbackClient
{
    private readonly HttpClient _httpClient;
    private readonly CallbackSettings _settings;
    private readonly JsonLinesLogger _logger;

    public MainAppCallbackClient(HttpClient httpClient, CallbackSettings settings, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Send callback to Main App with retry logic.
    /// Returns true if callback was delivered successfully (any retry).
    /// </summary>
    public async Task<bool> SendCallbackAsync(
        OutgoingCallback callback,
        string? callbackUrl = null,
        CancellationToken ct = default)
    {
        var url = callbackUrl ?? _settings.DefaultCallbackUrl;

        for (var attempt = 0; attempt <= _settings.MaxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    // Exponential backoff: 500ms, 1000ms, 2000ms
                    var delayMs = _settings.BaseDelayMs * (1 << (attempt - 1));
                    await Task.Delay(delayMs, ct);
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_settings.TimeoutMs);

                using var response = await _httpClient.PostAsJsonAsync(url, callback, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    if (attempt > 0)
                    {
                        _logger.SystemInfo(
                            $"Callback delivered on retry {attempt}: request_id={callback.RequestId}, action={callback.Action}");
                    }
                    return true;
                }

                _logger.SystemWarn(
                    $"Callback HTTP {(int)response.StatusCode} on attempt {attempt + 1}/{_settings.MaxRetries + 1}: " +
                    $"request_id={callback.RequestId}, url={url}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Application shutting down, don't retry
                throw;
            }
            catch (Exception ex)
            {
                _logger.SystemWarn(
                    $"Callback attempt {attempt + 1}/{_settings.MaxRetries + 1} failed: " +
                    $"request_id={callback.RequestId}, url={url}, error={ex.Message}");
            }
        }

        // All retries exhausted
        _logger.SystemError(
            $"[{ErrorCodes.IntegrationCallbackFailed}] Callback FAILED after {_settings.MaxRetries + 1} attempts: " +
            $"request_id={callback.RequestId}, action={callback.Action}, tenant_id={callback.TenantId}, chat_id={callback.ChatId}");

        return false;
    }
}
