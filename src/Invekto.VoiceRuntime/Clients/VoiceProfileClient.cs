using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Voice;
using Invekto.Shared.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Invekto.VoiceRuntime.Clients;

/// <summary>
/// FEAT-VFB F-VR-B Chunk B2 (spec §3): server-side fetch of a tenant's voice behavior profile.
///
/// Backend endpoint: GET /api/ops/tenants/{id}/voice-profile (B1, ValidateOpsAuth Bearer path =
/// Role==admin AND TenantId==0, D027). Uses the SAME admin-scope JWT pattern as
/// <see cref="TenantInfoClient"/> (5-minute admin token via JwtGenerator.GenerateToken), so the call
/// is accepted by Backend without a per-service secret store. Reuses the "Backend" named HttpClient.
///
/// GRACEFUL by contract (AD-53): on ANY transport/parse/auth/timeout/status failure this returns
/// null and logs an actionable WARN [INV-VR-024] (tenant_id + endpoint + status + failure category).
/// The caller degrades to a generic profile so the voice session NEVER breaks (spec "voice never
/// breaks"). Backend already synthesizes a generic row (200) for a not-yet-provisioned tenant, so a
/// null here means a real failure, not "tenant has no profile".
///
/// EXCEPTION: caller/session cancellation (OperationCanceledException with the caller's token
/// requesting cancellation) is RE-THROWN, never degraded — the WS is already tearing down and we must
/// not keep a synthetic generic session alive.
/// </summary>
public sealed class VoiceProfileClient
{
    private const string HttpClientName = "Backend";
    private const string AdminTokenSource = "voice_runtime";
    private static readonly TimeSpan AdminTokenLifetime = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpFactory;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;

    public VoiceProfileClient(IHttpClientFactory httpFactory, JwtGenerator jwtGenerator, JsonLinesLogger logger)
    {
        _httpFactory = httpFactory;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Returns the tenant's voice profile, or null on graceful-degrade (WARN [INV-VR-024] already
    /// logged with actionable fields). Re-throws OperationCanceledException only when the caller's
    /// token requested cancellation (session teardown). No retry — a single bounded attempt.
    /// </summary>
    public async Task<VoiceTenantProfileDto?> GetAsync(int tenantId, CancellationToken ct)
    {
        var path = $"/api/ops/tenants/{tenantId}/voice-profile";
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
            Degrade(tenantId, path, status: null, category: "jwt_mint", reason: ex.Message);
            return null;
        }
        catch (SecurityTokenException ex)
        {
            Degrade(tenantId, path, status: null, category: "jwt_mint", reason: ex.Message);
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Default ResponseContentRead (NOT ResponseHeadersRead): HttpClient.Timeout (5s) then bounds
        // the FULL response — headers AND body — and any transport/timeout failure (headers or body)
        // surfaces here at SendAsync. After this returns, the body is already buffered, so the
        // ReadFromJsonAsync below can only raise JsonException (no further network/timeout). This is
        // what makes the graceful-degrade contract complete + the fetch bounded (CQ1/CQ6).
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            Degrade(tenantId, path, status: null, category: "http", reason: ex.Message);
            return null;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient timeout (5s, covers headers+body) — NOT a caller cancel (filtered by `when`).
            Degrade(tenantId, path, status: null, category: "timeout", reason: ex.Message);
            return null;
        }
        // OperationCanceledException with ct.IsCancellationRequested (caller/session cancel) is NOT
        // caught here — it propagates so the WS teardown is not masked as a generic-degrade.

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                Degrade(tenantId, path, status: (int)response.StatusCode, category: "status", reason: response.StatusCode.ToString());
                return null;
            }

            try
            {
                // Body already buffered (ResponseContentRead) — parse-only, JsonException is the only
                // failure mode left. A 200 with a null/empty body degrades to generic but is LOGGED
                // (not silent) so the operator still sees the anomaly (CQ2).
                var dto = await response.Content.ReadFromJsonAsync<VoiceTenantProfileDto>(JsonOptions, ct);
                if (dto is null)
                {
                    Degrade(tenantId, path, status: (int)response.StatusCode, category: "empty_body", reason: "200 OK ama govde null/bos");
                    return null;
                }
                return dto;
            }
            catch (JsonException ex)
            {
                Degrade(tenantId, path, status: (int)response.StatusCode, category: "json", reason: ex.Message);
                return null;
            }
        }
    }

    private void Degrade(int tenantId, string endpoint, int? status, string category, string reason)
    {
        var statusPart = status.HasValue ? $" status={status.Value}" : string.Empty;
        _logger.SystemWarn(
            $"[{ErrorCodes.VoiceRuntimeVoiceProfileFetchDegraded}] [VoiceProfileClient] " +
            $"tenant={tenantId} endpoint={endpoint}{statusPart} category={category} " +
            $"-> generic business_type degrade: {reason}");
    }
}
