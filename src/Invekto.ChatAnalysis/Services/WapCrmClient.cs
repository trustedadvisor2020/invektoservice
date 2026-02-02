using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.ChatAnalysis;

namespace Invekto.ChatAnalysis.Services;

/// <summary>
/// WapCRM API client for fetching chat messages
/// Stage-0: 600ms timeout, 0 retry
/// </summary>
public sealed class WapCrmClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _secretKey;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string BaseUrl = "https://cxapi.wapcrm.net/api";
    private const string SecretKeyHeader = "X-CIB-SecretKey";
    private const int TimeoutMs = 600; // Stage-0 requirement

    public WapCrmClient(string secretKey)
    {
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromMilliseconds(TimeoutMs)
        };
        _httpClient.DefaultRequestHeaders.Add(SecretKeyHeader, _secretKey);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Fetch messages for a phone number from WapCRM
    /// </summary>
    /// <param name="phoneNumber">Customer phone number</param>
    /// <param name="instanceId">Optional instance ID for specific channel</param>
    /// <returns>Result with messages or error code</returns>
    public async Task<WapCrmResult<List<WapCrmMessage>>> GetMessagesForPhoneAsync(
        string phoneNumber,
        int? instanceId = null)
    {
        try
        {
            var request = new
            {
                chatPhoneNumber = phoneNumber,
                instanceID = instanceId
            };

            var response = await _httpClient.PostAsJsonAsync("messagelistforphone", request);

            if (!response.IsSuccessStatusCode)
            {
                return WapCrmResult<List<WapCrmMessage>>.Fail(
                    ErrorCodes.ChatAnalysisWapCrmError,
                    $"WapCRM returned {(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<WapCrmApiResponse<List<WapCrmMessage>>>(content, _jsonOptions);

            if (apiResponse == null)
            {
                return WapCrmResult<List<WapCrmMessage>>.Fail(
                    ErrorCodes.ChatAnalysisWapCrmError,
                    "WapCRM returned null response");
            }

            if (!apiResponse.Status)
            {
                return WapCrmResult<List<WapCrmMessage>>.Fail(
                    ErrorCodes.ChatAnalysisWapCrmError,
                    apiResponse.Message ?? "WapCRM returned status=false");
            }

            var messages = apiResponse.Data ?? [];

            if (messages.Count == 0)
            {
                return WapCrmResult<List<WapCrmMessage>>.Fail(
                    ErrorCodes.ChatAnalysisNoMessages,
                    $"No messages found for phone number: {phoneNumber}");
            }

            return WapCrmResult<List<WapCrmMessage>>.Success(messages);
        }
        catch (TaskCanceledException)
        {
            return WapCrmResult<List<WapCrmMessage>>.Fail(
                ErrorCodes.ChatAnalysisWapCrmTimeout,
                $"WapCRM timeout after {TimeoutMs}ms");
        }
        catch (HttpRequestException ex)
        {
            return WapCrmResult<List<WapCrmMessage>>.Fail(
                ErrorCodes.ChatAnalysisWapCrmError,
                $"WapCRM connection error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return WapCrmResult<List<WapCrmMessage>>.Fail(
                ErrorCodes.ChatAnalysisWapCrmError,
                $"WapCRM invalid JSON response: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// Result wrapper for WapCRM operations
/// </summary>
public sealed class WapCrmResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Data { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    private WapCrmResult() { }

    public static WapCrmResult<T> Success(T data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static WapCrmResult<T> Fail(string errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
