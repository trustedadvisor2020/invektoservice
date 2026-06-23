using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Chatinbox.VoiceRuntime.Clients;

/// <summary>
/// FEAT-VFB F0.5 Chunk B (AD-22 + AD-25): server-side authoritative tenant lookup for Voice Test
/// instruction rendering.
///
/// Backend endpoint: GET /api/ops/tenants (ValidateOpsAuth Bearer path = Role==admin AND TenantId==0,
/// per D027 audit fix). VoiceRuntime mints a 5-minute admin-scope JWT via JwtGenerator.GenerateToken
/// — this is the ONLY call site in VoiceRuntime that mints an admin-scope token; all other
/// cross-service calls use per-tenant service tokens (FlowInfoClient, KnowledgeSearchClient).
///
/// Q karari (2026-05-26): admin JWT path chosen over Basic Auth — keeps all three sibling clients on
/// the same JWT pattern (no per-service secret store), and reuses the existing JwtSettings already
/// validated by Backend. Blast radius is bounded to this single class (TenantInfoClient) by design.
///
/// Lifecycle: per-WS-session single call, ~hundreds of bytes payload, &lt;200ms typical roundtrip.
/// Result NOT cached — F0.5 sessions are infrequent (sysadmin demo), staleness risk &gt; cache benefit.
/// </summary>
public sealed class TenantInfoClient
{
    private const string HttpClientName = "Backend";
    private const string AdminTokenSource = "voice_runtime";
    private static readonly TimeSpan AdminTokenLifetime = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpFactory;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;

    public TenantInfoClient(IHttpClientFactory httpFactory, JwtGenerator jwtGenerator, JsonLinesLogger logger)
    {
        _httpFactory = httpFactory;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Fetches the full tenant list from Backend and returns the entry whose tenant_id matches.
    /// Returns null if the tenant is not present in the list (caller should map to INV-VR-021).
    /// Throws TenantInfoFetchException on HTTP/JSON failures (caller should map to INV-VR-014 + close WS 1011).
    /// </summary>
    public async Task<TenantInfo?> GetTenantAsync(int tenantId, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient(HttpClientName);

        string token;
        try
        {
            token = _jwtGenerator.GenerateToken(
                tenantId: 0,
                role: "admin",
                source: AdminTokenSource,
                expiry: AdminTokenLifetime);
        }
        catch (ArgumentException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeServiceJwtMintFailed}] [TenantInfoClient] admin token mint argument invalid: {ex.Message}");
            throw new TenantInfoFetchException(ErrorCodes.VoiceRuntimeServiceJwtMintFailed, "Yetki anahtari uretilemedi (gecersiz claim).", ex);
        }
        catch (SecurityTokenException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeServiceJwtMintFailed}] [TenantInfoClient] admin token mint signing failed: {ex.Message}");
            throw new TenantInfoFetchException(ErrorCodes.VoiceRuntimeServiceJwtMintFailed, "Yetki anahtari uretilemedi (imzalama hatasi).", ex);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/ops/tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeTenantFetchFailed}] [TenantInfoClient] HTTP error: {ex.Message}");
            throw new TenantInfoFetchException(ErrorCodes.VoiceRuntimeTenantFetchFailed, "Tenant servis cagrisi basarisiz.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeTenantFetchFailed}] [TenantInfoClient] HTTP timeout: {ex.Message}");
            throw new TenantInfoFetchException(ErrorCodes.VoiceRuntimeTenantFetchFailed, "Tenant servis zaman asimi.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeTenantFetchFailed}] [TenantInfoClient] Backend rejected admin token: {(int)response.StatusCode} {response.StatusCode}");
                throw new TenantInfoFetchException(ErrorCodes.VoiceRuntimeTenantFetchFailed, "Tenant servis yetki reddi.");
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeTenantFetchFailed}] [TenantInfoClient] Backend {(int)response.StatusCode} {response.StatusCode}");
                throw new TenantInfoFetchException(ErrorCodes.VoiceRuntimeTenantFetchFailed, "Tenant servis hata dondu.");
            }

            TenantListResponse? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<TenantListResponse>(JsonOptions, ct);
            }
            catch (JsonException ex)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeTenantFetchFailed}] [TenantInfoClient] JSON parse failed: {ex.Message}");
                throw new TenantInfoFetchException(ErrorCodes.VoiceRuntimeTenantFetchFailed, "Tenant servis cevap formati gecersiz.", ex);
            }

            if (body?.Tenants == null)
            {
                _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeTenantFetchFailed}] [TenantInfoClient] empty tenants envelope");
                throw new TenantInfoFetchException(ErrorCodes.VoiceRuntimeTenantFetchFailed, "Tenant listesi bos donmus.");
            }

            foreach (var t in body.Tenants)
            {
                if (t.TenantId == tenantId)
                    return t;
            }
            return null;  // Not found — caller maps to INV-VR-021
        }
    }

    private sealed class TenantListResponse
    {
        public List<TenantInfo>? Tenants { get; init; }
    }
}

/// <summary>
/// Subset of Backend TenantEntry returned by /api/ops/tenants — only fields VoiceRuntime needs
/// for instruction rendering. Backend serializes camelCase (ASP.NET Core defaults), JsonOptions
/// in TenantInfoClient uses JsonSerializerDefaults.Web to match.
/// </summary>
public sealed record TenantInfo
{
    public int TenantId { get; init; }
    public string? TenantName { get; init; }
    public string? Sector { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// Thrown by TenantInfoClient on HTTP/JSON/auth failure. Caller (VoicePocEndpoints handshake)
/// maps ErrorCode to WS close 1011 + INV-VR-014/018 control frame.
/// </summary>
public sealed class TenantInfoFetchException : Exception
{
    public string ErrorCode { get; }

    public TenantInfoFetchException(string errorCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}
