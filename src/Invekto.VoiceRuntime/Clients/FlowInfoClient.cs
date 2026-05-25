using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Invekto.VoiceRuntime.Clients;

/// <summary>
/// FEAT-VFB F0.5 Chunk B (AD-22): server-side authoritative flow lookup for Voice Test
/// instruction rendering.
///
/// Automation endpoint: GET /api/v1/flows/{tenantId}/{flowId} (NOT /api/v1/automation/flows/*;
/// that prefix only carries the LIW Chunk C /lookup endpoint). Auth = per-tenant service JWT
/// minted via JwtGenerator.GenerateServiceToken — mirrors the AiFaqHandler pattern proven in
/// Automation, so the standard JwtAuth middleware accepts the call and GetValidatedTenant
/// enforces tenant_id == route-tenantId.
///
/// Returns flow_name (display string for instruction template). flow_config (raw JSON) is
/// intentionally NOT surfaced — AD-24 keeps the generic template, persona extraction from
/// flow JSON nodes is a non-goal of F0.5 (deferred to F2 Migration 050 voice_persona kolonu).
/// </summary>
public sealed class FlowInfoClient
{
    private const string HttpClientName = "Automation";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpFactory;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;

    public FlowInfoClient(IHttpClientFactory httpFactory, JwtGenerator jwtGenerator, JsonLinesLogger logger)
    {
        _httpFactory = httpFactory;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Returns flow_name + is_active when the flow exists.
    /// Returns null on 404 (caller maps to INV-VR-022).
    /// Throws FlowInfoFetchException on HTTP/JSON/auth failure (caller maps to INV-VR-015 + close WS 1011).
    /// </summary>
    public async Task<FlowInfo?> GetFlowAsync(int tenantId, int flowId, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient(HttpClientName);

        string token;
        try
        {
            token = _jwtGenerator.GenerateServiceToken(tenantId);
        }
        catch (ArgumentException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeServiceJwtMintFailed}] [FlowInfoClient] service token mint argument invalid tenant={tenantId}: {ex.Message}");
            throw new FlowInfoFetchException(ErrorCodes.VoiceRuntimeServiceJwtMintFailed, "Yetki anahtari uretilemedi (gecersiz claim).", ex);
        }
        catch (SecurityTokenException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeServiceJwtMintFailed}] [FlowInfoClient] service token mint signing failed tenant={tenantId}: {ex.Message}");
            throw new FlowInfoFetchException(ErrorCodes.VoiceRuntimeServiceJwtMintFailed, "Yetki anahtari uretilemedi (imzalama hatasi).", ex);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/flows/{tenantId}/{flowId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeFlowFetchFailed}] [FlowInfoClient] HTTP error tenant={tenantId} flow={flowId}: {ex.Message}");
            throw new FlowInfoFetchException(ErrorCodes.VoiceRuntimeFlowFetchFailed, "Flow servis cagrisi basarisiz.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeFlowFetchFailed}] [FlowInfoClient] HTTP timeout tenant={tenantId} flow={flowId}: {ex.Message}");
            throw new FlowInfoFetchException(ErrorCodes.VoiceRuntimeFlowFetchFailed, "Flow servis zaman asimi.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;  // Not found — caller maps to INV-VR-022

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeFlowFetchFailed}] [FlowInfoClient] Automation rejected service token tenant={tenantId}: {(int)response.StatusCode}");
                throw new FlowInfoFetchException(ErrorCodes.VoiceRuntimeFlowFetchFailed, "Flow servis yetki reddi.");
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeFlowFetchFailed}] [FlowInfoClient] Automation {(int)response.StatusCode} tenant={tenantId} flow={flowId}");
                throw new FlowInfoFetchException(ErrorCodes.VoiceRuntimeFlowFetchFailed, "Flow servis hata dondu.");
            }

            FlowInfo? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<FlowInfo>(JsonOptions, ct);
            }
            catch (JsonException ex)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeFlowFetchFailed}] [FlowInfoClient] JSON parse failed: {ex.Message}");
                throw new FlowInfoFetchException(ErrorCodes.VoiceRuntimeFlowFetchFailed, "Flow servis cevap formati gecersiz.", ex);
            }

            return body;
        }
    }
}

/// <summary>
/// Subset of Automation flow response — only fields VoiceRuntime needs.
/// Automation endpoint returns snake_case keys (Dictionary&lt;string, object?&gt; serialized by
/// ASP.NET Core defaults, but the upstream payload uses snake_case keys in the response body
/// — flow_id, flow_name, is_active). JsonSerializerDefaults.Web is case-insensitive so it
/// matches both flow_id and flowId. JsonPropertyName attributes pin the snake_case keys
/// explicitly to remove ambiguity.
/// </summary>
public sealed record FlowInfo
{
    [System.Text.Json.Serialization.JsonPropertyName("flow_id")]
    public int FlowId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("tenant_id")]
    public int TenantId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("flow_name")]
    public string? FlowName { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("is_active")]
    public bool IsActive { get; init; }
}

/// <summary>
/// Thrown by FlowInfoClient on HTTP/JSON/auth failure. Caller (VoicePocEndpoints handshake)
/// maps ErrorCode to WS close 1011 + INV-VR-015/018 control frame.
/// </summary>
public sealed class FlowInfoFetchException : Exception
{
    public string ErrorCode { get; }

    public FlowInfoFetchException(string errorCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}
