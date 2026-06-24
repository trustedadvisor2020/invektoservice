using FluentAssertions;
using Chatinbox.Shared.Contracts.Video;
using Chatinbox.Shared.Ics;

namespace Chatinbox.Tests._Shared.Ics;

/// <summary>
/// FEAT-VCP Chunk A AC10: IcsBuilder must emit a CRLF-terminated VCALENDAR with
/// a VTIMEZONE per distinct attendee timezone, DTSTART/DTEND with explicit TZID,
/// and ATTENDEE rows only for attendees that actually have an email address.
/// Phone-only leads are delivered the meeting link via WA template in Chunk B.
/// </summary>
public class IcsBuilderTests
{
    private static MeetingResult SampleMeeting() => new(
        MeetingLink: "https://meet.google.com/mock-ABcd123456",
        CalendarEventId: null,
        Provider: "mock",
        StartAtUtc: new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
        DurationMinutes: 30);

    private static List<AttendeeDto> SampleAttendees() => new()
    {
        new AttendeeDto("Dr. Dentist", "dentist@clinic.tr", null, "Europe/Istanbul", "dentist"),
        new AttendeeDto("Alice Lead", "alice@example.ie", null, "Europe/Dublin", "lead"),
    };

    [Fact]
    public void Build_Emits_VCalendar_Headers()
    {
        var ics = IcsBuilder.Build(
            SampleMeeting(),
            SampleAttendees(),
            organizerEmail: "organizer@clinic.tr",
            dentistTimeZoneId: "Europe/Istanbul");

        ics.Should().Contain("BEGIN:VCALENDAR\r\n");
        ics.Should().Contain("VERSION:2.0\r\n");
        ics.Should().Contain("PRODID:-//Chatinbox//FEAT-VCP//EN\r\n");
        ics.Should().Contain("END:VCALENDAR\r\n");
    }

    [Fact]
    public void Build_Emits_Vtimezone_Per_Unique_Attendee_Tz()
    {
        var ics = IcsBuilder.Build(
            SampleMeeting(),
            SampleAttendees(),
            organizerEmail: "organizer@clinic.tr",
            dentistTimeZoneId: "Europe/Istanbul");

        // Two distinct TZIDs (Istanbul + Dublin) — both must appear under a VTIMEZONE/TZID line.
        ics.Should().Contain("TZID:Europe/Istanbul\r\n");
        ics.Should().Contain("TZID:Europe/Dublin\r\n");

        var vtzCount = System.Text.RegularExpressions.Regex.Matches(ics, "BEGIN:VTIMEZONE").Count;
        vtzCount.Should().Be(2);
    }

    [Fact]
    public void Build_Attendee_Line_Only_For_Email_Bearing_Attendees()
    {
        var attendees = new List<AttendeeDto>
        {
            new("Dr. Dentist", "dentist@clinic.tr", null, "Europe/Istanbul", "dentist"),
            new("Phone Lead", null, "+905551112233", "Europe/Istanbul", "lead"),
        };

        var ics = IcsBuilder.Build(
            SampleMeeting(),
            attendees,
            organizerEmail: "organizer@clinic.tr",
            dentistTimeZoneId: "Europe/Istanbul");

        ics.Should().Contain("ATTENDEE;CN=\"Dr. Dentist\";RSVP=TRUE:mailto:dentist@clinic.tr");

        // Phone-only attendees must not produce an ATTENDEE line. The lead's name may
        // legitimately appear in SUMMARY / DESCRIPTION (that's how the dentist recognises
        // the meeting); what we're enforcing is that no ATTENDEE entry carries the phone
        // number or the (absent) email.
        var attendeeLines = ics.Split("\r\n").Where(l => l.StartsWith("ATTENDEE", StringComparison.Ordinal)).ToList();
        attendeeLines.Should().HaveCount(1);
        attendeeLines[0].Should().Contain("dentist@clinic.tr");
        ics.Should().NotContain("+905551112233");
    }

    [Fact]
    public void Build_Dtstart_Has_Tzid_And_Local_Time_Format()
    {
        var ics = IcsBuilder.Build(
            SampleMeeting(),
            SampleAttendees(),
            organizerEmail: "organizer@clinic.tr",
            dentistTimeZoneId: "Europe/Istanbul");

        // DTSTART;TZID=Europe/Istanbul:YYYYMMDDTHHMMSS (no trailing Z — local time, not UTC)
        ics.Should().MatchRegex(@"DTSTART;TZID=Europe/Istanbul:\d{8}T\d{6}\r\n");
        ics.Should().MatchRegex(@"DTEND;TZID=Europe/Istanbul:\d{8}T\d{6}\r\n");
    }

    [Fact]
    public void Build_Uses_Crlf_Line_Endings()
    {
        var ics = IcsBuilder.Build(
            SampleMeeting(),
            SampleAttendees(),
            organizerEmail: "organizer@clinic.tr",
            dentistTimeZoneId: "Europe/Istanbul");

        // RFC 5545 §3.1: content lines terminate with CRLF.
        ics.Should().EndWith("END:VCALENDAR\r\n");
        ics.Should().NotContain("\n\n");   // no LF-only paragraph breaks
        // Every \n should be preceded by \r.
        for (var i = 0; i < ics.Length; i++)
        {
            if (ics[i] == '\n')
                (i > 0 && ics[i - 1] == '\r').Should().BeTrue(
                    $"RFC 5545 line endings must be CRLF (offset {i})");
        }
    }
}
