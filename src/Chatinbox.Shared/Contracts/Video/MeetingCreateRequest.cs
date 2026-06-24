namespace Chatinbox.Shared.Contracts.Video;

/// <summary>
/// FEAT-VCP Chunk A: Input payload for <see cref="IVideoConsultProvider.CreateMeetingAsync"/>.
/// Caller (Chunk B Appointments) builds this from the confirmed appointment row plus
/// the tenant/lead/dentist attendee triple.
/// </summary>
/// <param name="TenantId">Chatinbox tenant_id; providers use it to scope mock link hashes
/// and (Chunk C) select tenant-scoped OAuth credentials.</param>
/// <param name="Title">Human-readable meeting title (e.g. "Dental Consultation — Alice Q").
/// Factored into the mock hash so re-titling yields a new link.</param>
/// <param name="StartAtUtc">Meeting start time in UTC. Caller responsibility to convert
/// from tenant-local time. Provider formats for calendar using per-attendee TZID.</param>
/// <param name="DurationMinutes">Meeting length in minutes (must be &gt; 0).</param>
/// <param name="DentistTimeZoneId">IANA timezone id for the dentist (e.g. "Europe/Istanbul").
/// Used as the primary DTSTART TZID when the organiser calendar invite is generated.</param>
/// <param name="Attendees">At least one attendee required. Email-less (phone-only) attendees
/// are excluded from the calendar invite's ATTENDEE block and receive the meeting link via
/// WA outbound template instead (Chunk B).</param>
public sealed record MeetingCreateRequest(
    int TenantId,
    string Title,
    DateTime StartAtUtc,
    int DurationMinutes,
    string DentistTimeZoneId,
    IReadOnlyList<AttendeeDto> Attendees);
