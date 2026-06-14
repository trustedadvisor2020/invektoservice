using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Integrations.Services.Ikas;

/// <summary>
/// Typed GraphQL client for ikas admin API.
/// POST to https://api.myikas.com/api/v1/admin/graphql with Bearer token.
/// Handles: request building, auth, response parsing, GraphQL error extraction.
/// </summary>
public sealed class IkasGraphQlClient
{
    private readonly HttpClient _httpClient;
    private readonly IkasTokenManager _tokenManager;
    private readonly JsonLinesLogger _logger;

    private const string GraphQlEndpoint = "https://api.myikas.com/api/v1/admin/graphql";
    private const int MaxResponseBytes = 256 * 1024; // 256KB limit

    public IkasGraphQlClient(HttpClient httpClient, IkasTokenManager tokenManager, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _tokenManager = tokenManager;
        _logger = logger;
    }

    /// <summary>
    /// Execute a GraphQL query/mutation. Returns parsed JSON response or null on failure.
    /// On 401, invalidates token and retries once.
    /// </summary>
    public async Task<IkasGraphQlResponse> ExecuteAsync(
        int tenantId, string query, object? variables, CancellationToken ct)
    {
        var token = await _tokenManager.GetAccessTokenAsync(tenantId, ct);
        if (token == null)
            return IkasGraphQlResponse.Fail(ErrorCodes.IntegrationsEcomOAuthFailed, "Token acquisition failed");

        var result = await SendRequestAsync(token, query, variables, ct);

        // Retry once on 401 (token might have expired)
        if (result.HttpStatus == 401)
        {
            _tokenManager.InvalidateToken(tenantId);
            token = await _tokenManager.GetAccessTokenAsync(tenantId, ct);
            if (token == null)
                return IkasGraphQlResponse.Fail(ErrorCodes.IntegrationsEcomOAuthFailed, "Token refresh failed");

            result = await SendRequestAsync(token, query, variables, ct);
        }

        return result;
    }

    private async Task<IkasGraphQlResponse> SendRequestAsync(
        string token, string query, object? variables, CancellationToken ct)
    {
        try
        {
            var payload = variables != null
                ? JsonSerializer.Serialize(new { query, variables })
                : JsonSerializer.Serialize(new { query });

            using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var (body, truncated) = await ReadBoundedResponseAsync(response, ct);

            // Audit Int-5: a response that fills the 256KB cap is truncated, so the body is
            // unreliable for BOTH the HTTP-error snippet and JSON parsing (the latter would throw
            // a JsonException masquerading as a generic parse error). Handle truncation first,
            // before the status/parse branches, and surface it as an explicit coded failure.
            // (No raw body logged — only the byte cap and the HTTP status.)
            if (truncated)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.IntegrationsEcomGraphQlFailed}] ikas GraphQL response truncated at {MaxResponseBytes} bytes " +
                    $"(HTTP {(int)response.StatusCode}) — response too large.");
                return IkasGraphQlResponse.Fail(
                    ErrorCodes.IntegrationsEcomGraphQlFailed,
                    $"Response too large (truncated at {MaxResponseBytes / 1024}KB)");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.IntegrationsEcomGraphQlFailed}] ikas GraphQL HTTP {(int)response.StatusCode}: " +
                    $"{Truncate(body, 200)}");
                return new IkasGraphQlResponse
                {
                    Success = false,
                    HttpStatus = (int)response.StatusCode,
                    ErrorCode = ErrorCodes.IntegrationsEcomGraphQlFailed,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            // Parse GraphQL response
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Check for GraphQL errors array
            if (root.TryGetProperty("errors", out var errorsArr) &&
                errorsArr.ValueKind == JsonValueKind.Array && errorsArr.GetArrayLength() > 0)
            {
                var firstError = errorsArr[0];
                var message = firstError.TryGetProperty("message", out var msgProp)
                    ? msgProp.GetString() ?? "Unknown GraphQL error"
                    : "Unknown GraphQL error";

                _logger.SystemWarn(
                    $"[{ErrorCodes.IntegrationsEcomGraphQlFailed}] ikas GraphQL error: {Truncate(message, 200)}");

                return new IkasGraphQlResponse
                {
                    Success = false,
                    HttpStatus = 200,
                    ErrorCode = ErrorCodes.IntegrationsEcomGraphQlFailed,
                    ErrorMessage = message
                };
            }

            // Extract "data" portion
            if (root.TryGetProperty("data", out var dataProp))
            {
                return new IkasGraphQlResponse
                {
                    Success = true,
                    HttpStatus = 200,
                    DataJson = dataProp.GetRawText()
                };
            }

            return IkasGraphQlResponse.Fail(ErrorCodes.IntegrationsEcomGraphQlFailed, "No 'data' in GraphQL response");
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomGraphQlFailed}] ikas GraphQL connection error: {ex.Message}");
            return IkasGraphQlResponse.Fail(ErrorCodes.IntegrationsEcomGraphQlFailed, $"Connection error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomGraphQlFailed}] ikas GraphQL JSON parse error: {ex.Message}");
            return IkasGraphQlResponse.Fail(ErrorCodes.IntegrationsEcomGraphQlFailed, $"Response parse error: {ex.Message}");
        }
    }

    private static async Task<(string Body, bool Truncated)> ReadBoundedResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[MaxResponseBytes];
        var totalRead = 0;

        while (totalRead < MaxResponseBytes)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, MaxResponseBytes - totalRead), ct);
            if (bytesRead == 0) break;
            totalRead += bytesRead;
        }

        // Buffer filled to the cap → there may be unread bytes on the wire; treat as truncated.
        var truncated = totalRead >= MaxResponseBytes;
        return (Encoding.UTF8.GetString(buffer, 0, totalRead), truncated);
    }

    private static string Truncate(string s, int maxLength) =>
        s.Length <= maxLength ? s : s[..maxLength] + "...";
}

/// <summary>
/// Result of an ikas GraphQL API call.
/// </summary>
public sealed class IkasGraphQlResponse
{
    public bool Success { get; init; }
    public int HttpStatus { get; init; }
    public string? DataJson { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static IkasGraphQlResponse Fail(string errorCode, string message) => new()
    {
        Success = false,
        HttpStatus = 0,
        ErrorCode = errorCode,
        ErrorMessage = message
    };
}
