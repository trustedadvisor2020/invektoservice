namespace Invekto.Shared.Contracts.Video;

/// <summary>
/// FEAT-VCP Chunk A: Provider interface for video consultation meeting creation.
/// Implementations live in Invekto.Integrations (Chunk A: GoogleMeetMockProvider;
/// Chunk C: GoogleMeetProvider with Workspace OAuth). Callers (Invekto.Appointments
/// in Chunk B) resolve the active provider per tenant via <c>VideoProviderFactory</c>
/// and invoke <see cref="CreateMeetingAsync"/> when an appointment is confirmed.
///
/// Chunk-A contract notes:
/// - Implementations MUST be deterministic OR idempotent for the same
///   (<see cref="MeetingCreateRequest.TenantId"/>, <see cref="MeetingCreateRequest.Title"/>,
///   <see cref="MeetingCreateRequest.StartAtUtc"/>) tuple so retry storms cannot
///   double-book calendars.
/// - Implementations MUST throw <see cref="ArgumentException"/> for malformed input
///   (non-positive duration, empty timezone, no attendees). Caller (Chunk B) maps
///   the exception to an INV-INT-141 failure response envelope.
/// </summary>
public interface IVideoConsultProvider
{
    /// <summary>
    /// Create a video consultation meeting and return the join link + provider metadata.
    /// Throws <see cref="ArgumentException"/> on malformed input.
    /// </summary>
    Task<MeetingResult> CreateMeetingAsync(MeetingCreateRequest request, CancellationToken ct);
}
