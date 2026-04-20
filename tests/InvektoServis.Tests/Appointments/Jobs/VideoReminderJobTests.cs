using System.Net;
using System.Net.Http;
using FluentAssertions;
using Invekto.Appointments.Data;
using Invekto.Appointments.Services.Jobs;
using Invekto.Shared.Auth;
using Invekto.Shared.Logging;
using InvektoServis.Tests._Shared.Infrastructure;
using NSubstitute;

namespace InvektoServis.Tests.Appointments.Jobs;

/// <summary>
/// FEAT-VCP Chunk B AC11: VideoReminderJob skips (audit INV-INT-145) and the
/// happy dispatch + sent_at mark. Fire-time state-change guard is the secondary
/// defense for orphan reminder scheduling.
/// </summary>
public class VideoReminderJobTests
{
    private const int TenantId = 5050;
    private const long AppointmentId = 42L;

    [Fact]
    public async Task SkipsWhenAppointmentMissing()
    {
        var fixture = new Fixture();
        fixture.Repository
            .GetAppointmentVideoRowAsync(TenantId, AppointmentId, Arg.Any<CancellationToken>())
            .Returns((AppointmentVideoRow?)null);

        await fixture.Job.SendAsync(TenantId, AppointmentId, "24h");

        fixture.OutboundHandler.Calls.Should().Be(0);
        await fixture.Repository.DidNotReceive().MarkVideoReminderSentAsync(
            Arg.Any<int>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("completed")]
    [InlineData("no_show")]
    public async Task SkipsWhenStatusNotConfirmed(string status)
    {
        var fixture = new Fixture();
        fixture.RepositoryReturnsRow(status: status, meetingLink: "https://meet.google.com/mock-A");

        await fixture.Job.SendAsync(TenantId, AppointmentId, "24h");

        fixture.OutboundHandler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task SkipsWhenMeetingLinkNull()
    {
        var fixture = new Fixture();
        fixture.RepositoryReturnsRow(meetingLink: null);

        await fixture.Job.SendAsync(TenantId, AppointmentId, "24h");

        fixture.OutboundHandler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task SkipsWhenAlreadySent_24h()
    {
        var fixture = new Fixture();
        fixture.RepositoryReturnsRow(
            meetingLink: "https://meet.google.com/mock-A",
            sent24h: DateTime.UtcNow.AddMinutes(-5));

        await fixture.Job.SendAsync(TenantId, AppointmentId, "24h");

        fixture.OutboundHandler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task HappyPath_SendsAndMarksSent_24h()
    {
        var fixture = new Fixture();
        fixture.RepositoryReturnsRow(meetingLink: "https://meet.google.com/mock-XYZ");

        await fixture.Job.SendAsync(TenantId, AppointmentId, "24h");

        fixture.OutboundHandler.Calls.Should().Be(1);
        fixture.OutboundHandler.LastBody.Should().Contain("video_reminder_24h");
        fixture.OutboundHandler.LastBody.Should().Contain("https://meet.google.com/mock-XYZ");
        await fixture.Repository.Received(1).MarkVideoReminderSentAsync(
            TenantId, AppointmentId, "24h", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidReminderType_Throws()
    {
        var fixture = new Fixture();

        Func<Task> act = () => fixture.Job.SendAsync(TenantId, AppointmentId, "99h");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private sealed class Fixture
    {
        public AppointmentsRepository Repository { get; }
        public RecordingHandler OutboundHandler { get; }
        public VideoReminderJob Job { get; }

        public Fixture()
        {
            Repository = Substitute.For<AppointmentsRepository>(
                (Invekto.Shared.Data.PostgresConnectionFactory)null!,
                FakeLoggerFactory.Create("RepoStub"));
            Repository.GetTenantTimezoneAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<string?>("Europe/Istanbul"));

            OutboundHandler = new RecordingHandler();
            var outboundClient = new HttpClient(OutboundHandler)
            {
                BaseAddress = new Uri("http://outbound.test")
            };
            var httpClientFactory = Substitute.For<IHttpClientFactory>();
            httpClientFactory.CreateClient("Outbound").Returns(outboundClient);

            Job = new VideoReminderJob(
                Repository,
                httpClientFactory,
                new JwtGenerator(new JwtSettings
                {
                    SecretKey = new string('k', 64),
                    Issuer = "t",
                    Audience = "t",
                    ClockSkewSeconds = 60
                }),
                FakeLoggerFactory.Create("ReminderJobStub"));
        }

        public void RepositoryReturnsRow(
            string status = "confirmed",
            string? meetingLink = "https://meet.google.com/mock-A",
            DateTime? sent24h = null,
            DateTime? sent1h = null)
        {
            Repository
                .GetAppointmentVideoRowAsync(TenantId, AppointmentId, Arg.Any<CancellationToken>())
                .Returns(new AppointmentVideoRow
                {
                    Id = AppointmentId,
                    TenantId = TenantId,
                    PatientName = "Alice",
                    PatientPhone = "+905551112233",
                    AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
                    StartTime = new TimeOnly(10, 0),
                    EndTime = new TimeOnly(10, 30),
                    Status = status,
                    MeetingLink = meetingLink,
                    VideoReminder24hSentAt = sent24h,
                    VideoReminder1hSentAt = sent1h,
                    DoctorName = "Dr. Test"
                });
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
