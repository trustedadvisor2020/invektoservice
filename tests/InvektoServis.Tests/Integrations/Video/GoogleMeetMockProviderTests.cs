using System.Text.RegularExpressions;
using FluentAssertions;
using Invekto.Integrations.Services.Video;
using Invekto.Shared.Contracts.Video;
using InvektoServis.Tests._Shared.Infrastructure;

namespace InvektoServis.Tests.Integrations.Video;

/// <summary>
/// FEAT-VCP Chunk A AC9: the mock provider must be deterministic (same inputs →
/// same link), emit the <c>mock-</c> prefixed URL, return Provider=="mock" with
/// null CalendarEventId, and throw ArgumentException for malformed input.
/// </summary>
public class GoogleMeetMockProviderTests
{
    private static GoogleMeetMockProvider NewProvider() => new(FakeLoggerFactory.Create("VideoProviderTests"));

    private static MeetingCreateRequest SampleRequest() => new(
        TenantId: 5050,
        Title: "Dental Consultation — Alice",
        StartAtUtc: new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
        DurationMinutes: 30,
        DentistTimeZoneId: "Europe/Istanbul",
        Attendees: new List<AttendeeDto>
        {
            new("Alice", "alice@example.com", null, "Europe/Dublin", "lead"),
        });

    [Fact]
    public async Task Deterministic_Same_Inputs_Produce_Same_Link()
    {
        var provider = NewProvider();

        var first = await provider.CreateMeetingAsync(SampleRequest(), CancellationToken.None);
        var second = await provider.CreateMeetingAsync(SampleRequest(), CancellationToken.None);

        first.MeetingLink.Should().Be(second.MeetingLink);
    }

    [Fact]
    public async Task Provider_Identifier_Is_Mock()
    {
        var provider = NewProvider();

        var result = await provider.CreateMeetingAsync(SampleRequest(), CancellationToken.None);

        result.Provider.Should().Be("mock");
    }

    [Fact]
    public async Task CalendarEventId_Is_Null_For_Mock()
    {
        var provider = NewProvider();

        var result = await provider.CreateMeetingAsync(SampleRequest(), CancellationToken.None);

        result.CalendarEventId.Should().BeNull();
    }

    [Fact]
    public async Task Link_Matches_Mock_Meet_Format()
    {
        var provider = NewProvider();

        var result = await provider.CreateMeetingAsync(SampleRequest(), CancellationToken.None);

        Regex.IsMatch(result.MeetingLink, @"^https://meet\.google\.com/mock-[A-Za-z0-9_-]{10}$")
            .Should().BeTrue($"link was '{result.MeetingLink}'");
    }

    [Theory]
    [InlineData(0)]    // zero duration
    [InlineData(-15)]  // negative duration
    public async Task Throws_When_Duration_Not_Positive(int duration)
    {
        var provider = NewProvider();
        var req = SampleRequest() with { DurationMinutes = duration };

        var act = async () => await provider.CreateMeetingAsync(req, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Throws_When_No_Attendees()
    {
        var provider = NewProvider();
        var req = SampleRequest() with { Attendees = new List<AttendeeDto>() };

        var act = async () => await provider.CreateMeetingAsync(req, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Throws_When_Dentist_Timezone_Blank()
    {
        var provider = NewProvider();
        var req = SampleRequest() with { DentistTimeZoneId = "" };

        var act = async () => await provider.CreateMeetingAsync(req, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
