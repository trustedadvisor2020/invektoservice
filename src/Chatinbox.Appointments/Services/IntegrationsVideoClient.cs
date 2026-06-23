using System.Net.Http.Json;
using Chatinbox.Appointments.Services.Video;
using Chatinbox.Shared.Contracts.Video;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Appointments.Services;

/// <summary>
/// FEAT-VCP Chunk B: HTTP hop from Chatinbox.Appointments' VideoMeetingCreationJob to
/// Chatinbox.Integrations' <c>POST /internal/video/meetings</c>. Preserves the three
/// operational outcomes encoded in the factory's result-semantics split:
/// <list type="bullet">
/// <item>200 envelope with <c>Meeting</c> populated → <see cref="VideoMeetingHopOutcome.Success"/>.</item>
/// <item>200 envelope with <c>Skipped=true</c> → <see cref="VideoMeetingHopOutcome.Skipped"/> (INV-INT-142).</item>
/// <item>400 envelope with <c>ErrorCode=INV-INT-141</c> → <see cref="VideoMeetingHopOutcome.Failed"/>.</item>
/// </list>
/// 503 responses and transport exceptions throw <see cref="VideoMeetingHopException"/>
/// so Hangfire's AutomaticRetry captures INV-INT-144 and re-issues the POST — this is
/// the only path that retries. The other two are deterministic terminal states.
/// Microservice isolation: no <c>using Chatinbox.Integrations</c>; contracts and envelopes
/// come from <c>Chatinbox.Shared.Contracts.Video</c>.
/// </summary>
// FEAT-VCP Chunk B: not sealed — CreateMeetingAsync is virtual so VideoMeetingCreationJob
// tests can substitute a Moq-backed client without spinning HttpClient + WireMock.
public class IntegrationsVideoClient
{
    public const string HttpClientName = "IntegrationsInternal";
    public const string InternalTokenHeader = "X-Internal-Service-Token";
    public const string SharedSecretConfigKey = "InternalServices:SharedSecret";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonLinesLogger _logger;
    private readonly IConfiguration _configuration;

    public IntegrationsVideoClient(
        IHttpClientFactory httpClientFactory,
        JsonLinesLogger logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    public virtual async Task<VideoMeetingHopOutcome> CreateMeetingAsync(
        MeetingCreateRequest request, CancellationToken ct)
    {
        var sharedSecret = _configuration[SharedSecretConfigKey];
        if (string.IsNullOrWhiteSpace(sharedSecret))
        {
            // Treat missing config as a transient deployment gap so Hangfire retries once
            // the operator sets the secret. Distinct from 404 tenant_settings and provider
            // ArgumentException paths.
            throw new VideoMeetingHopException(
                $"[{VideoHopErrorCodes.VideoMeetingHopFailed}] " +
                $"{SharedSecretConfigKey} not configured — hop aborted");
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/internal/video/meetings")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add(InternalTokenHeader, sharedSecret);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new VideoMeetingHopException(
                $"[{VideoHopErrorCodes.VideoMeetingHopFailed}] " +
                $"transport error tenant={request.TenantId}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new VideoMeetingHopException(
                $"[{VideoHopErrorCodes.VideoMeetingHopFailed}] " +
                $"timeout tenant={request.TenantId}: {ex.Message}", ex);
        }

        using (response)
        {
            var status = (int)response.StatusCode;

            if (status >= 500)
            {
                var body = await SafeReadBodyAsync(response, ct).ConfigureAwait(false);
                throw new VideoMeetingHopException(
                    $"[{VideoHopErrorCodes.VideoMeetingHopFailed}] " +
                    $"Integrations responded {status} tenant={request.TenantId}: {body}");
            }

            if (status != 200 && status != 400)
            {
                var body = await SafeReadBodyAsync(response, ct).ConfigureAwait(false);
                throw new VideoMeetingHopException(
                    $"[{VideoHopErrorCodes.VideoMeetingHopFailed}] " +
                    $"unexpected status {status} tenant={request.TenantId}: {body}");
            }

            VideoMeetingHopResponse? envelope;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<VideoMeetingHopResponse>(ct)
                    .ConfigureAwait(false);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new VideoMeetingHopException(
                    $"[{VideoHopErrorCodes.VideoMeetingHopFailed}] " +
                    $"malformed response tenant={request.TenantId}: {ex.Message}", ex);
            }

            if (envelope is null)
            {
                throw new VideoMeetingHopException(
                    $"[{VideoHopErrorCodes.VideoMeetingHopFailed}] " +
                    $"empty response body tenant={request.TenantId}");
            }

            if (envelope.Skipped)
            {
                _logger.SystemInfo(
                    $"[{envelope.ErrorCode ?? VideoHopErrorCodes.VideoReminderSkippedStateChanged}] " +
                    $"IntegrationsVideoClient: provider not configured tenant={request.TenantId}");
                return VideoMeetingHopOutcome.Skipped(envelope.ErrorCode ?? "INV-INT-142");
            }

            if (status == 400 || envelope.Meeting is null)
            {
                _logger.SystemWarn(
                    $"[{envelope.ErrorCode ?? VideoHopErrorCodes.VideoMeetingHopFailed}] " +
                    $"IntegrationsVideoClient: provider rejected input tenant={request.TenantId}");
                return VideoMeetingHopOutcome.Failed(envelope.ErrorCode ?? "INV-INT-141");
            }

            return VideoMeetingHopOutcome.Success(envelope.Meeting);
        }
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        // Body read is best-effort for log context only. Typed catch per CODEX UTANSIN
        // rule — HttpRequestException + IOException + ObjectDisposedException are the
        // three body-read failure modes .NET surfaces; any other exception escapes so
        // Hangfire's outer retry can capture it with the correct context.
        try
        {
            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return $"<body unreadable: HttpRequestException {ex.Message}>";
        }
        catch (System.IO.IOException ex)
        {
            return $"<body unreadable: IOException {ex.Message}>";
        }
        catch (ObjectDisposedException ex)
        {
            return $"<body unreadable: ObjectDisposedException {ex.Message}>";
        }
    }
}

/// <summary>
/// Thrown when the Integrations hop cannot complete — transport error, 5xx, malformed
/// body. Propagates out of <c>VideoMeetingCreationJob.RunAsync</c> so Hangfire retries
/// with the default exponential backoff. Not used for deterministic terminal states
/// (skipped, argument validation) — those return typed <see cref="VideoMeetingHopOutcome"/>.
/// </summary>
public sealed class VideoMeetingHopException : Exception
{
    public VideoMeetingHopException(string message) : base(message) { }
    public VideoMeetingHopException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Discriminated outcome returned by <see cref="IntegrationsVideoClient.CreateMeetingAsync"/>.
/// Callers branch on <see cref="Kind"/>: <c>Success</c> → persist + schedule; <c>Skipped</c>
/// → log INV-INT-142 and return without retry; <c>Failed</c> → log INV-INT-141 and return
/// without retry (input is the problem, retries won't help).
/// </summary>
public sealed record VideoMeetingHopOutcome(
    VideoMeetingHopOutcomeKind Kind,
    MeetingResult? Meeting,
    string? ErrorCode)
{
    public static VideoMeetingHopOutcome Success(MeetingResult meeting) =>
        new(VideoMeetingHopOutcomeKind.Success, meeting, null);

    public static VideoMeetingHopOutcome Skipped(string errorCode) =>
        new(VideoMeetingHopOutcomeKind.Skipped, null, errorCode);

    public static VideoMeetingHopOutcome Failed(string errorCode) =>
        new(VideoMeetingHopOutcomeKind.Failed, null, errorCode);
}

public enum VideoMeetingHopOutcomeKind
{
    Success,
    Skipped,
    Failed
}
