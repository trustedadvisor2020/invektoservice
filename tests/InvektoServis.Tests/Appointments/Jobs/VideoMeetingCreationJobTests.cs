using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Invekto.Appointments.Data;
using Invekto.Appointments.Services;
using Invekto.Appointments.Services.Jobs;
using Invekto.Shared.Auth;
using Invekto.Shared.Contracts.Video;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using InvektoServis.Tests._Shared.Infrastructure;
using NSubstitute;

namespace InvektoServis.Tests.Appointments.Jobs;

/// <summary>
/// FEAT-VCP Chunk B AC10: VideoMeetingCreationJob guards + happy path.
/// Covers the five states the job must distinguish at its entry point:
/// missing appointment, already-created (idempotency), status changed,
/// hop skipped (INV-INT-142), and full success path (hop → persist →
/// schedule → outbound trigger).
/// </summary>
public class VideoMeetingCreationJobTests
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

        await fixture.Job.RunAsync(TenantId, AppointmentId);

        await fixture.VideoClient.DidNotReceive().CreateMeetingAsync(
            Arg.Any<MeetingCreateRequest>(), Arg.Any<CancellationToken>());
        await fixture.Repository.DidNotReceive().SetMeetingLinkAsync(
            Arg.Any<int>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SkipsWhenAlreadyCreated()
    {
        var fixture = new Fixture();
        fixture.RepositoryReturnsRow(meetingLink: "https://meet.google.com/mock-ABC");

        await fixture.Job.RunAsync(TenantId, AppointmentId);

        await fixture.VideoClient.DidNotReceive().CreateMeetingAsync(
            Arg.Any<MeetingCreateRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("completed")]
    [InlineData("no_show")]
    public async Task SkipsWhenStatusNotConfirmed(string status)
    {
        var fixture = new Fixture();
        fixture.RepositoryReturnsRow(status: status);

        await fixture.Job.RunAsync(TenantId, AppointmentId);

        await fixture.VideoClient.DidNotReceive().CreateMeetingAsync(
            Arg.Any<MeetingCreateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HopSkippedNotConfigured_DoesNotPersistOrSchedule()
    {
        var fixture = new Fixture();
        fixture.RepositoryReturnsRow();
        fixture.VideoClient
            .CreateMeetingAsync(Arg.Any<MeetingCreateRequest>(), Arg.Any<CancellationToken>())
            .Returns(VideoMeetingHopOutcome.Skipped("INV-INT-142"));

        await fixture.Job.RunAsync(TenantId, AppointmentId);

        await fixture.Repository.DidNotReceive().SetMeetingLinkAsync(
            Arg.Any<int>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
        fixture.Enqueuer.DidNotReceive().Schedule(
            Arg.Any<System.Linq.Expressions.Expression<Func<VideoReminderJob, Task>>>(),
            Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task HappyPath_PersistsAndSchedulesReminders()
    {
        var fixture = new Fixture();
        fixture.RepositoryReturnsRow();
        fixture.VideoClient
            .CreateMeetingAsync(Arg.Any<MeetingCreateRequest>(), Arg.Any<CancellationToken>())
            .Returns(VideoMeetingHopOutcome.Success(new MeetingResult(
                MeetingLink: "https://meet.google.com/mock-XYZ",
                CalendarEventId: null,
                Provider: "mock",
                StartAtUtc: DateTime.UtcNow.AddDays(3),
                DurationMinutes: 30)));
        fixture.Repository
            .SetMeetingLinkAsync(TenantId, AppointmentId, Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        fixture.Enqueuer
            .Schedule(
                Arg.Any<System.Linq.Expressions.Expression<Func<VideoReminderJob, Task>>>(),
                Arg.Any<TimeSpan>())
            .Returns("job-stub-id");

        await fixture.Job.RunAsync(TenantId, AppointmentId);

        await fixture.Repository.Received(1).SetMeetingLinkAsync(
            TenantId, AppointmentId,
            "https://meet.google.com/mock-XYZ", "mock", null, Arg.Any<CancellationToken>());
        fixture.Enqueuer.Received(2).Schedule(
            Arg.Any<System.Linq.Expressions.Expression<Func<VideoReminderJob, Task>>>(),
            Arg.Any<TimeSpan>());
        await fixture.Repository.Received(1).SetVideoReminderJobIdsAsync(
            TenantId, AppointmentId, "job-stub-id", "job-stub-id", Arg.Any<CancellationToken>());
        fixture.OutboundHandler.Calls.Should().Be(1);
        fixture.OutboundHandler.LastBody.Should().Contain("video_meeting_confirmed");
    }

    private sealed class Fixture
    {
        public AppointmentsRepository Repository { get; }
        public IntegrationsVideoClient VideoClient { get; }
        public IBackgroundJobEnqueuer Enqueuer { get; }
        public RecordingHandler OutboundHandler { get; }
        public VideoMeetingCreationJob Job { get; }

        public Fixture()
        {
            Repository = Substitute.For<AppointmentsRepository>(
                /* db */ (Invekto.Shared.Data.PostgresConnectionFactory)null!,
                /* logger */ FakeLoggerFactory.Create("RepoStub"));
            Repository.GetTenantTimezoneAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<string?>("Europe/Istanbul"));

            VideoClient = Substitute.For<IntegrationsVideoClient>(
                /* factory */ Substitute.For<IHttpClientFactory>(),
                FakeLoggerFactory.Create("VideoClientStub"),
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

            Enqueuer = Substitute.For<IBackgroundJobEnqueuer>();

            OutboundHandler = new RecordingHandler();
            var outboundClient = new HttpClient(OutboundHandler)
            {
                BaseAddress = new Uri("http://outbound.test")
            };
            var httpClientFactory = Substitute.For<IHttpClientFactory>();
            httpClientFactory.CreateClient("Outbound").Returns(outboundClient);

            Job = new VideoMeetingCreationJob(
                Repository,
                VideoClient,
                Enqueuer,
                httpClientFactory,
                new JwtGenerator(new JwtSettings
                {
                    SecretKey = new string('k', 64),
                    Issuer = "t",
                    Audience = "t",
                    ClockSkewSeconds = 60
                }),
                FakeLoggerFactory.Create("JobStub"));
        }

        public void RepositoryReturnsRow(string status = "confirmed", string? meetingLink = null)
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
                    DoctorName = "Dr. Test"
                });
        }
    }

    /// <summary>
    /// Minimal HttpMessageHandler recorder. Captures the last request body + call
    /// count so tests can assert the Outbound trigger payload without spinning
    /// a real HTTP server. 200 OK for every request.
    /// </summary>
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
