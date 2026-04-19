using System.Globalization;
using System.Text;
using Invekto.Shared.Contracts.Video;

namespace Invekto.Shared.Ics;

/// <summary>
/// FEAT-VCP Chunk A: Minimal RFC 5545 VCALENDAR / VEVENT builder for video consultation
/// invites. Pure function — no I/O, no logging, no network. Produces a single VEVENT with
/// one VTIMEZONE block per distinct <see cref="AttendeeDto.TimeZoneId"/> so Outlook / Apple
/// Calendar render the correct local time (Google Calendar tolerates UTC-only but the other
/// major clients need explicit TZID per FEAT-VCP spec Mitigation).
///
/// Phone-only attendees (Email == null) are intentionally excluded from the ATTENDEE block;
/// Chunk B delivers the meeting link to them via WA outbound template instead.
/// Line endings are CRLF as required by RFC 5545 §3.1.
/// </summary>
public static class IcsBuilder
{
    /// <summary>
    /// Build a VCALENDAR payload for the given meeting and attendees.
    /// </summary>
    /// <param name="meeting">Result from <see cref="IVideoConsultProvider.CreateMeetingAsync"/>.
    /// <see cref="MeetingResult.CalendarEventId"/> seeds the UID; when null (mock provider) a
    /// deterministic fallback UID is derived from StartAtUtc so retries stay idempotent.</param>
    /// <param name="attendees">All attendees, including phone-only leads. The builder filters
    /// to email-bearing attendees for the ATTENDEE block but still materialises VTIMEZONE for
    /// every unique <see cref="AttendeeDto.TimeZoneId"/> so a non-Google client opened by the
    /// dentist still shows the lead's local time in tooltip/hover.</param>
    /// <param name="organizerEmail">Calendar organiser mailbox (typically the dentist or
    /// coordinator). Must be a valid RFC 5322 address.</param>
    /// <param name="dentistTimeZoneId">Primary DTSTART TZID. Normally mirrors the dentist
    /// attendee's <see cref="AttendeeDto.TimeZoneId"/>.</param>
    /// <param name="description">Optional meeting description (multi-line safe — value is
    /// escaped per RFC 5545 §3.3.11).</param>
    public static string Build(
        MeetingResult meeting,
        IReadOnlyList<AttendeeDto> attendees,
        string organizerEmail,
        string dentistTimeZoneId,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        ArgumentNullException.ThrowIfNull(attendees);
        if (attendees.Count == 0)
            throw new ArgumentException("[INV-INT-141] At least one attendee required.", nameof(attendees));
        if (string.IsNullOrWhiteSpace(organizerEmail))
            throw new ArgumentException("[INV-INT-141] Organizer email required.", nameof(organizerEmail));
        if (string.IsNullOrWhiteSpace(dentistTimeZoneId))
            throw new ArgumentException("[INV-INT-141] Dentist TimeZoneId required.", nameof(dentistTimeZoneId));

        var sb = new StringBuilder(1024);
        AppendLine(sb, "BEGIN:VCALENDAR");
        AppendLine(sb, "VERSION:2.0");
        AppendLine(sb, "PRODID:-//Invekto//FEAT-VCP//EN");
        AppendLine(sb, "METHOD:REQUEST");
        AppendLine(sb, "CALSCALE:GREGORIAN");

        // Emit one VTIMEZONE per unique attendee TZ plus the dentist TZ (deduped).
        var tzIds = new HashSet<string>(StringComparer.Ordinal) { dentistTimeZoneId };
        foreach (var a in attendees)
        {
            if (!string.IsNullOrWhiteSpace(a.TimeZoneId))
                tzIds.Add(a.TimeZoneId);
        }
        foreach (var tzId in tzIds)
            AppendVTimezone(sb, tzId);

        AppendLine(sb, "BEGIN:VEVENT");

        // Capture-into-local avoids the null-forgiving operator while keeping the
        // nullability analyzer satisfied (iter 2 CQ5 fix — `!` is forbidden).
        var existingUid = meeting.CalendarEventId;
        var uid = !string.IsNullOrWhiteSpace(existingUid)
            ? existingUid
            : $"invekto-mock-{meeting.StartAtUtc.ToUniversalTime():yyyyMMddTHHmmssZ}@invekto";
        AppendLine(sb, $"UID:{EscapeText(uid)}");

        var dtStamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        AppendLine(sb, $"DTSTAMP:{dtStamp}");

        var startLocal = ConvertToLocalIcsFormat(meeting.StartAtUtc, dentistTimeZoneId);
        var endUtc = meeting.StartAtUtc.AddMinutes(meeting.DurationMinutes);
        var endLocal = ConvertToLocalIcsFormat(endUtc, dentistTimeZoneId);
        AppendLine(sb, $"DTSTART;TZID={dentistTimeZoneId}:{startLocal}");
        AppendLine(sb, $"DTEND;TZID={dentistTimeZoneId}:{endLocal}");

        AppendLine(sb, $"SUMMARY:{EscapeText(FindSummary(meeting, attendees))}");
        if (!string.IsNullOrWhiteSpace(description))
            AppendLine(sb, $"DESCRIPTION:{EscapeText(description)}");
        AppendLine(sb, $"LOCATION:{EscapeText(meeting.MeetingLink)}");
        AppendLine(sb, $"URL:{meeting.MeetingLink}");

        AppendLine(sb, $"ORGANIZER:mailto:{organizerEmail}");

        foreach (var attendee in attendees)
        {
            if (string.IsNullOrWhiteSpace(attendee.Email))
                continue; // phone-only — handled via WA template in Chunk B
            AppendLine(sb, $"ATTENDEE;CN={EscapeParamText(attendee.Name)};RSVP=TRUE:mailto:{attendee.Email}");
        }

        AppendLine(sb, "STATUS:CONFIRMED");
        AppendLine(sb, "END:VEVENT");
        AppendLine(sb, "END:VCALENDAR");
        return sb.ToString();
    }

    /// <summary>
    /// Emit a minimal VTIMEZONE block. STANDARD offset comes from the current IANA rule set;
    /// DST transitions are intentionally omitted because consumer clients (Google/Outlook/Apple)
    /// resolve the TZID against their own IANA database anyway — keeping the payload short
    /// sidesteps IANA rule-version drift between server and client.
    /// <see cref="TimeZoneNotFoundException"/> and <see cref="InvalidTimeZoneException"/> are
    /// intentionally NOT caught here (iter 2 CQ2 fix): silently substituting "+0000" would
    /// bury a genuine misconfiguration in tenant-supplied DentistTimeZoneId / AttendeeDto.TimeZoneId
    /// behind a malformed calendar invite. Callers (Chunk B appointment handler) MUST validate
    /// attendee timezone ids before invoking this builder; on an unexpected exception here the
    /// caller catches, surfaces INV-INT-141 in the failure envelope, and the appointment is
    /// confirmed without an ICS attachment.
    /// </summary>
    private static void AppendVTimezone(StringBuilder sb, string tzId)
    {
        // Propagates TimeZoneNotFoundException / InvalidTimeZoneException — see method XML doc.
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var offset = FormatOffset(tz.BaseUtcOffset);

        AppendLine(sb, "BEGIN:VTIMEZONE");
        AppendLine(sb, $"TZID:{tzId}");
        AppendLine(sb, "BEGIN:STANDARD");
        AppendLine(sb, "DTSTART:19700101T000000");
        AppendLine(sb, $"TZOFFSETFROM:{offset}");
        AppendLine(sb, $"TZOFFSETTO:{offset}");
        AppendLine(sb, $"TZNAME:{tzId}");
        AppendLine(sb, "END:STANDARD");
        AppendLine(sb, "END:VTIMEZONE");
    }

    // Propagates TimeZoneNotFoundException / InvalidTimeZoneException intentionally —
    // see AppendVTimezone XML doc for rationale (iter 2 CQ2 fix).
    private static string ConvertToLocalIcsFormat(DateTime utc, string tzId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        return local.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture);
    }

    private static string FormatOffset(TimeSpan offset)
    {
        var sign = offset.Ticks < 0 ? "-" : "+";
        var abs = offset.Duration();
        return $"{sign}{abs.Hours:D2}{abs.Minutes:D2}";
    }

    private static string FindSummary(MeetingResult meeting, IReadOnlyList<AttendeeDto> attendees)
    {
        // Prefer the lead's name for a recognisable calendar title; fall back to a generic label.
        foreach (var a in attendees)
        {
            if (string.Equals(a.Role, "lead", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(a.Name))
                return $"Video Consultation — {a.Name}";
        }
        return "Video Consultation";
    }

    // RFC 5545 §3.3.11 TEXT escape: backslash, semicolon, comma, newline.
    private static string EscapeText(string input)
    {
        return input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    // Parameter-value text (e.g. ATTENDEE;CN=...) — quote if it contains
    // whitespace / colon / semicolon / comma, and escape any embedded double-quotes.
    private static string EscapeParamText(string input)
    {
        var escaped = input.Replace("\"", "\\\"", StringComparison.Ordinal);
        if (escaped.IndexOfAny(new[] { ' ', ':', ';', ',' }) >= 0)
            return $"\"{escaped}\"";
        return escaped;
    }

    private static void AppendLine(StringBuilder sb, string value)
    {
        sb.Append(value).Append("\r\n");
    }
}
