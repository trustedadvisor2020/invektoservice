// FEAT-VCP Chunk A: service-local error code constants mirroring arch/errors.md INV-INT-140..142.
// Pattern follows Invekto.Integrations.Services.Zoho.ZohoErrorCodes — service-scoped error codes
// live in the consuming service rather than Invekto.Shared.Constants.ErrorCodes, which is reserved
// for cross-service codes (INV-INT-001..004 generic webhook/callback).
//
// Chunk A only surfaces INV-INT-142 (provider_not_configured) via VideoProviderFactory's
// graceful null-return path; INV-INT-140 / INV-INT-141 are declared for forward use by
// Chunk B (appointment handler meeting-create failure) and Chunk C (GoogleMeetProvider OAuth
// refresh failure). Declaring them now keeps the error catalogue contiguous and callers
// can reference the names before the code that throws them exists.

namespace Invekto.Integrations.Services.Video;

public static class VideoErrorCodes
{
    /// <summary>
    /// Chunk C — Google Workspace OAuth refresh token invalid or expired.
    /// Declared in Chunk A; first thrown by <c>GoogleMeetProvider</c> in Chunk C
    /// after a 400/401 from the Google token endpoint exhausts the local retry.
    /// </summary>
    public const string OAuthTokenInvalid = "INV-INT-140";

    /// <summary>
    /// Chunk B — provider's <c>CreateMeetingAsync</c> threw or returned a malformed result.
    /// Declared in Chunk A; Chunk B's appointment handler catches provider exceptions and
    /// wraps them in a failure envelope using this code. Retry is safe because Chunk A
    /// contract requires deterministic-or-idempotent implementations.
    /// </summary>
    public const string MeetingCreateFailed = "INV-INT-141";

    /// <summary>
    /// Chunk A — <c>VideoProviderFactory.ResolveAsync</c> returned null for the tenant,
    /// either because <c>tenant_settings.video_provider</c> is null (never configured) or
    /// because the selected provider name is not yet registered in DI (e.g. 'googlemeet'
    /// before Chunk C wires the production implementation). Caller maps this to a
    /// non-blocking confirmation without a meeting link.
    /// </summary>
    public const string ProviderNotConfigured = "INV-INT-142";

    /// <summary>
    /// Chunk A — the tenant_settings probe in <c>VideoProviderFactory.ResolveAsync</c>
    /// raised <c>NpgsqlException</c>. The factory intentionally lets the exception
    /// propagate so Chunk B's appointment handler can distinguish a DB outage
    /// (this code / 503 response) from a genuine "tenant not configured" gap
    /// (<see cref="ProviderNotConfigured"/> / null return / non-blocking confirmation).
    /// Catching and masking the exception would collapse two operationally distinct
    /// states into the same graceful-skip path and send support teams chasing a config
    /// issue when the real problem is database health.
    /// </summary>
    public const string ProviderResolveDbError = "INV-INT-143";
}
