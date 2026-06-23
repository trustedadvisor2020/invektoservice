using System.Security.Cryptography;
using System.Text;
using Chatinbox.Shared.Contracts.Video;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Integrations.Services.Video;

/// <summary>
/// FEAT-VCP Chunk A: OAuth-free mock implementation of <see cref="IVideoConsultProvider"/>.
/// Produces a deterministic link of the form <c>https://meet.google.com/mock-{hash}</c>
/// (Q override during 2026-04-19 interview: the <c>mock-</c> prefix on the Google domain
/// guarantees a visible 404 when anyone actually clicks the link, signalling "mock mode"
/// unambiguously in logs, support tickets, and pilot screenshots).
///
/// Determinism rationale: for the same <c>(TenantId, Title, StartAtUtc)</c> tuple the hash
/// is stable, so a retry storm cannot produce two calendar entries with different links.
/// Chunk B's Hangfire reminder scheduler can also recompute the expected link when it fires
/// without needing to persist extra state.
///
/// No network calls, no DB writes, no I/O — pure function plus logging.
/// </summary>
public sealed class GoogleMeetMockProvider : IVideoConsultProvider
{
    private readonly JsonLinesLogger _logger;

    public GoogleMeetMockProvider(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    public Task<MeetingResult> CreateMeetingAsync(MeetingCreateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Exception messages are prefixed with the error code Chunk B's appointment handler
        // surfaces in its failure envelope (INV-INT-141 meeting_create_failed), so operators
        // can grep the code end-to-end even though the throw happens in Shared-contract territory
        // before the handler-owned envelope is built (iter 2 CQ1/CQ10/CQ12 fix).
        if (request.DurationMinutes <= 0)
            throw new ArgumentException("[INV-INT-141] DurationMinutes must be > 0.", nameof(request));
        if (request.Attendees == null || request.Attendees.Count == 0)
            throw new ArgumentException("[INV-INT-141] At least one attendee required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.DentistTimeZoneId))
            throw new ArgumentException("[INV-INT-141] DentistTimeZoneId required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("[INV-INT-141] Title required.", nameof(request));

        ct.ThrowIfCancellationRequested();

        var hash = ComputeDeterministicHash(request.TenantId, request.Title, request.StartAtUtc);
        var link = $"https://meet.google.com/mock-{hash}";

        _logger.SystemInfo(
            $"[VCP-MOCK] CreateMeetingAsync tenant={request.TenantId} title=\"{request.Title}\" " +
            $"start_utc={request.StartAtUtc:O} attendees={request.Attendees.Count} link={link}");

        var result = new MeetingResult(
            MeetingLink: link,
            CalendarEventId: null,   // mock does not create a calendar event
            Provider: "mock",
            StartAtUtc: request.StartAtUtc,
            DurationMinutes: request.DurationMinutes);

        return Task.FromResult(result);
    }

    // Deterministic SHA256(tenantId|title|startAtUtc) -> first 10 base64url chars.
    // Collision space ~60 bits — plenty for pilot-scale traffic; prod provider (Chunk C)
    // delegates uniqueness to Google Calendar's event-id generator anyway.
    private static string ComputeDeterministicHash(int tenantId, string title, DateTime startAtUtc)
    {
        var input = $"{tenantId}|{title.Trim()}|{startAtUtc.ToUniversalTime():O}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var digest = SHA256.HashData(bytes);
        return Convert.ToBase64String(digest)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_')
            .Substring(0, 10);
    }
}
