using Invekto.Integrations.Services.Video;
using Invekto.Shared.Contracts.Video;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Integrations.Endpoints;

/// <summary>
/// FEAT-VCP Chunk B: internal endpoint consumed by Invekto.Appointments'
/// <c>VideoMeetingCreationJob</c> over HTTP. Resolves the tenant's configured
/// <see cref="IVideoConsultProvider"/> via <see cref="VideoProviderFactory"/>,
/// invokes <see cref="IVideoConsultProvider.CreateMeetingAsync"/>, and returns
/// a <see cref="VideoMeetingHopResponse"/> envelope whose three outcomes mirror
/// the factory's result-semantics split:
/// <list type="bullet">
/// <item>200 + <c>{Skipped:true, ErrorCode:"INV-INT-142"}</c> — tenant not configured.</item>
/// <item>200 + <c>{Skipped:false, Meeting:{...}}</c> — success.</item>
/// <item>400 + <c>{Skipped:false, ErrorCode:"INV-INT-141"}</c> — provider threw ArgumentException.</item>
/// <item>503 + ErrorResponse(INV-INT-143) — NpgsqlException inside factory resolve (Appointments retries).</item>
/// </list>
/// Auth: <see cref="InternalAuth.HeaderName"/> shared-secret header (pattern mirrors
/// <c>InternalServices:SharedSecret</c> used by LIW Chunk B). No JWT — the hop carries
/// tenant_id in the body because Appointments holds no <c>TenantContext</c> for a
/// Hangfire-scheduled job.
/// </summary>
public static class VideoMeetingEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/internal/video/meetings", HandleCreateAsync);
    }

    internal static async Task<IResult> HandleCreateAsync(
        HttpContext ctx,
        MeetingCreateRequest request,
        VideoProviderFactory factory,
        JsonLinesLogger logger,
        IConfiguration config,
        CancellationToken ct)
    {
        // Every response path on this endpoint returns the same VideoMeetingHopResponse
        // envelope (auth-fail, validation-fail, provider-skip, provider-fail, DB outage,
        // success). The single-envelope discipline lets IntegrationsVideoClient deserialise
        // uniformly and map to the discriminated VideoMeetingHopOutcome without branching
        // by content-type. Auth/DB failures carry their distinct error codes
        // (INV-INT-147 / INV-INT-143) so the caller can still differentiate from the
        // business-state INV-INT-142 "skipped" path.
        var (authOk, authStatus, authReason) = InternalAuth.Validate(ctx, config);
        if (!authOk)
        {
            logger.SystemWarn(
                $"[{VideoErrorCodes.InternalServiceAuthFailed}] /internal/video/meetings: " +
                $"auth reject status={authStatus} reason={authReason}");
            return Results.Json(
                new VideoMeetingHopResponse(
                    Skipped: false,
                    ErrorCode: VideoErrorCodes.InternalServiceAuthFailed,
                    Meeting: null),
                statusCode: authStatus);
        }

        if (request.TenantId <= 0 || request.DurationMinutes <= 0
            || string.IsNullOrWhiteSpace(request.Title)
            || string.IsNullOrWhiteSpace(request.DentistTimeZoneId)
            || request.Attendees is null || request.Attendees.Count == 0)
        {
            return Results.Json(
                new VideoMeetingHopResponse(Skipped: false, ErrorCode: VideoErrorCodes.MeetingCreateFailed, Meeting: null),
                statusCode: 400);
        }

        IVideoConsultProvider? provider;
        try
        {
            provider = await factory.ResolveAsync(request.TenantId, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            logger.SystemError(
                $"[{VideoErrorCodes.ProviderResolveDbError}] /internal/video/meetings: " +
                $"tenant_settings probe failed tenant={request.TenantId}: {ex.Message}");
            return Results.Json(
                new VideoMeetingHopResponse(
                    Skipped: false,
                    ErrorCode: VideoErrorCodes.ProviderResolveDbError,
                    Meeting: null),
                statusCode: 503);
        }

        if (provider is null)
        {
            logger.SystemInfo(
                $"[{VideoErrorCodes.ProviderNotConfigured}] /internal/video/meetings: " +
                $"skipped (provider_not_configured) tenant={request.TenantId}");
            return Results.Json(
                new VideoMeetingHopResponse(Skipped: true, ErrorCode: VideoErrorCodes.ProviderNotConfigured, Meeting: null));
        }

        try
        {
            var result = await provider.CreateMeetingAsync(request, ct).ConfigureAwait(false);
            return Results.Json(new VideoMeetingHopResponse(Skipped: false, ErrorCode: null, Meeting: result));
        }
        catch (ArgumentException ex)
        {
            logger.SystemWarn(
                $"[{VideoErrorCodes.MeetingCreateFailed}] /internal/video/meetings: " +
                $"provider rejected input tenant={request.TenantId}: {ex.Message}");
            return Results.Json(
                new VideoMeetingHopResponse(Skipped: false, ErrorCode: VideoErrorCodes.MeetingCreateFailed, Meeting: null),
                statusCode: 400);
        }
    }

    /// <summary>
    /// Shared-secret header gate. Mirrors the Backend <c>IntakeInternalAuth</c> pattern
    /// (header + constant-time compare against <c>InternalServices:SharedSecret</c>). Kept
    /// local to this endpoint file so Integrations does not grow a service-wide auth
    /// dependency before other internal endpoints exist.
    /// </summary>
    internal static class InternalAuth
    {
        public const string HeaderName = "X-Internal-Service-Token";
        public const string ConfigKey = "InternalServices:SharedSecret";

        public static (bool Ok, int StatusCode, string Reason) Validate(
            HttpContext ctx, IConfiguration config)
        {
            var expected = config[ConfigKey];
            if (string.IsNullOrWhiteSpace(expected))
                return (false, 500, $"Internal service auth not configured (missing {ConfigKey}).");

            if (!ctx.Request.Headers.TryGetValue(HeaderName, out var provided) || provided.Count == 0)
                return (false, 401, $"Missing {HeaderName} header.");

            var supplied = provided[0];
            if (string.IsNullOrEmpty(supplied) || !SlowEquals(supplied, expected))
                return (false, 403, $"Invalid {HeaderName}.");

            return (true, 200, string.Empty);
        }

        private static bool SlowEquals(string? a, string b)
        {
            if (a is null) return false;
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
