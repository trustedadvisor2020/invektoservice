namespace Chatinbox.Shared.Contracts.Video;

/// <summary>
/// FEAT-VCP Chunk A: Single calendar attendee descriptor used by
/// <see cref="MeetingCreateRequest.Attendees"/> and the ICS builder.
/// </summary>
/// <param name="Name">Display name (e.g. "Dr. Mehmet Yilmaz"). Surfaced in the ICS
/// ATTENDEE CN parameter and — for phone-only leads — in the WA template greeting.</param>
/// <param name="Email">RFC 5322 address. <c>null</c> when the attendee is a phone-only lead;
/// such attendees are intentionally excluded from the ICS ATTENDEE block (lesson 2026-04-19:
/// bouncing fake addresses into Google Workspace invites risks spam flags).</param>
/// <param name="PhoneE164">E.164-formatted phone (e.g. "+905551234567"). Used by Chunk B
/// to send the WA meeting-link template when <see cref="Email"/> is null.</param>
/// <param name="TimeZoneId">IANA timezone id (e.g. "Europe/Dublin"). Each distinct value
/// across the attendee list produces a VTIMEZONE block so non-Google calendar clients
/// (Outlook, Apple Calendar) render the meeting in the correct local time.</param>
/// <param name="Role">Role hint for presentation / audit: <c>"dentist"</c>,
/// <c>"coordinator"</c>, or <c>"lead"</c>. Does not affect ICS semantics; callers use it
/// to pick the WA template variant for phone-only branches (lead vs coordinator copy).</param>
public sealed record AttendeeDto(
    string Name,
    string? Email,
    string? PhoneE164,
    string TimeZoneId,
    string Role);
