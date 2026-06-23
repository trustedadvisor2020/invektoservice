using System.Net.Http.Json;
using System.Text.Json;
using Chatinbox.Shared.Contracts.Inma.Dtos;

namespace Chatinbox.Shared.Contracts.Inma;

/// <summary>
/// FEAT-J2: Production client. Issues authenticated POST requests to INMA
/// /api/optout and /api/optin, parses the nested WapCrmApiResponse envelope
/// into a normalized InmaOptOutResult so upstream consumers do not need to
/// understand INMA's raw JSON shape.
///
/// Wire contract: wapcrm-marketing-api.md sections 5.1 and 5.2.
/// Auth: X-CIB-SecretKey header (per-tenant or global, configured at DI time).
/// Timeout: configurable via InmaAuth:OptOutSync:TimeoutSeconds (default 5s).
/// </summary>
public sealed class HttpInmaContactOptOutClient : IInmaContactOptOutClient
{
    private readonly HttpClient _httpClient;
    private readonly string _secretKey;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private const string SecretKeyHeader = "X-CIB-SecretKey";

    public HttpInmaContactOptOutClient(HttpClient httpClient, string baseUrl, string secretKey, int timeoutSeconds)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));

        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 5);
        if (!_httpClient.DefaultRequestHeaders.Contains(SecretKeyHeader))
        {
            _httpClient.DefaultRequestHeaders.Add(SecretKeyHeader, _secretKey);
        }
    }

    public Task<InmaOptOutResult> PushOptOutAsync(InmaOptOutRequest request, CancellationToken ct = default)
        => SendAsync("api/optout", request, ct);

    public Task<InmaOptOutResult> PushOptInAsync(InmaOptOutRequest request, CancellationToken ct = default)
        => SendAsync("api/optin", request, ct);

    private async Task<InmaOptOutResult> SendAsync(string path, InmaOptOutRequest request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(path, request, JsonOptions, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return NetworkFailure(0, $"Timeout: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return NetworkFailure(0, $"HTTP error: {ex.Message}");
        }

        var bodyText = await response.Content.ReadAsStringAsync(ct);

        WapCrmApiResponse<InmaOptOutData>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<WapCrmApiResponse<InmaOptOutData>>(bodyText, JsonOptions);
        }
        catch (JsonException)
        {
            return new InmaOptOutResult
            {
                Success = false,
                StatusCode = "PARSE-FAIL",
                Message = $"Unparseable INMA response (HTTP {(int)response.StatusCode})",
                HttpStatusCode = (int)response.StatusCode,
            };
        }

        if (parsed == null)
        {
            return new InmaOptOutResult
            {
                Success = false,
                StatusCode = "PARSE-FAIL",
                Message = "INMA response deserialized to null",
                HttpStatusCode = (int)response.StatusCode,
            };
        }

        var statusCode = parsed.StatusCode ?? string.Empty;
        var data = parsed.Data;

        return new InmaOptOutResult
        {
            Success = parsed.Status,
            StatusCode = statusCode,
            Message = parsed.Message,
            HttpStatusCode = (int)response.StatusCode,
            AlreadyOptedOut = data?.AlreadyOptedOut ?? false,
            AffectedChatCount = data?.AffectedChatCount ?? 0,
        };
    }

    private static InmaOptOutResult NetworkFailure(int httpStatus, string message) => new()
    {
        Success = false,
        StatusCode = "NETWORK",
        Message = message,
        HttpStatusCode = httpStatus,
    };
}
