namespace Invekto.Shared.Contracts.Video;

/// <summary>
/// FEAT-VCP Chunk A: Output payload returned by <see cref="IVideoConsultProvider.CreateMeetingAsync"/>.
/// Chunk B (Appointments) persists <see cref="MeetingLink"/> + <see cref="CalendarEventId"/> on the
/// appointment row and schedules Hangfire reminder jobs keyed by appointment id.
/// </summary>
/// <param name="MeetingLink">HTTPS URL the attendee opens to join the meeting.
/// Mock provider returns a non-joinable link with the <c>mock-</c> prefix so the format
/// is recognisable in logs and tickets.</param>
/// <param name="CalendarEventId">Google Calendar event id (Chunk C). <c>null</c> for the mock
/// provider since no calendar entry is created; callers must tolerate null.</param>
/// <param name="Provider">Provider identifier: <c>"mock"</c> or <c>"googlemeet"</c> (Chunk C adds).
/// Persisted verbatim so operators can trace which implementation produced a link.</param>
/// <param name="StartAtUtc">Echoed from the request so caller retains a single struct when
/// forwarding to the Hangfire reminder scheduler.</param>
/// <param name="DurationMinutes">Echoed from the request for the same reason.</param>
public sealed record MeetingResult(
    string MeetingLink,
    string? CalendarEventId,
    string Provider,
    DateTime StartAtUtc,
    int DurationMinutes);
