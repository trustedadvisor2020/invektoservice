using FluentAssertions;
using Invekto.Outbound.Data;
using Invekto.Outbound.Services;
using Invekto.Shared.Contracts.Inma;
using Invekto.Shared.Contracts.Inma.Dtos;
using Invekto.Shared.Data;
using InvektoServis.Tests._Shared.Infrastructure;
using NSubstitute;

namespace InvektoServis.Tests.Outbound;

/// <summary>
/// FEAT-J2: AC10 scenarios covering the InmaOptOutSyncJob drain flow end-to-end
/// at the unit boundary — OutboundRepository is mocked, so no DB is required.
/// HTTP hops to INMA are mocked via <see cref="IInmaContactOptOutClient"/>.
///
/// Scenarios covered: happy (S1), idempotent 909 (S2), not-found 908 (S3),
/// network fail (S4), NoOp (S5). MessageCategory roundtrip (S6) and 906 block
/// (S7) live in Backend-integration tests because they exercise the bridge.
/// </summary>
public class InmaOptOutSyncJobTests
{
    private static InmaOptOutSyncOptions DefaultOptions() => new()
    {
        Mode = "Http",
        BaseUrl = "https://cxapi.test",
        SecretKey = "test",
        BatchSize = 10,
        MaxAttempts = 5,
        TimeoutSeconds = 5,
        IntervalMs = 60_000,
    };

    private static OutboxRow Row(long id = 1, string eventType = "opt_out") => new()
    {
        Id = id,
        TenantId = 42,
        Phone = "905551112233",
        InstanceId = 101,
        EventType = eventType,
        Scope = "all",
        Reason = "Keyword: STOP",
        Source = "whatsapp",
        Attempts = 0,
    };

    private static (OutboundRepository repo, IInmaContactOptOutClient client, InmaOptOutSyncJob job) BuildSut(
        InmaOptOutResult clientResult)
    {
        var fakeDb = new PostgresConnectionFactory("Host=localhost;Port=5432;Database=t;Username=t;Password=t");
        var logger = FakeLoggerFactory.Create("InmaOptOutSyncJob.Test");

        var repo = Substitute.For<OutboundRepository>(fakeDb, logger);
        var client = Substitute.For<IInmaContactOptOutClient>();
        client.PushOptOutAsync(Arg.Any<InmaOptOutRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(clientResult));
        client.PushOptInAsync(Arg.Any<InmaOptOutRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(clientResult));

        var job = new InmaOptOutSyncJob(repo, client, logger, DefaultOptions());
        return (repo, client, job);
    }

    /// <summary>
    /// Reflection-invoked ProcessRowAsync stub — exercises a single row against a
    /// mocked INMA client without spinning the timer.
    /// </summary>
    private static async Task InvokeProcessRowAsync(InmaOptOutSyncJob job, OutboxRow row)
    {
        var method = typeof(InmaOptOutSyncJob).GetMethod(
            "ProcessRowAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var task = (Task)method.Invoke(job, new object[] { row, CancellationToken.None })!;
        await task;
    }

    [Fact]
    public async Task Scenario1_HappyPath_MarksProcessedWithStatus0()
    {
        var (repo, client, job) = BuildSut(new InmaOptOutResult
        {
            Success = true,
            StatusCode = "0",
            HttpStatusCode = 200,
        });

        await InvokeProcessRowAsync(job, Row(id: 11));

        await client.Received(1).PushOptOutAsync(
            Arg.Is<InmaOptOutRequest>(r =>
                r.IdentifierType == "phone" &&
                r.Identifier == "905551112233" &&
                r.InstanceID == 101 &&
                r.Scope == "all"),
            Arg.Any<CancellationToken>());
        await repo.Received(1).MarkOutboxProcessedAsync(11, "0", Arg.Any<CancellationToken>());
        await repo.DidNotReceive().MarkOutboxFailedAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Scenario2_Idempotent909_MarksProcessedNotFailed()
    {
        var (repo, _, job) = BuildSut(new InmaOptOutResult
        {
            Success = false,
            StatusCode = "909",
            AlreadyOptedOut = true,
            HttpStatusCode = 200,
        });

        await InvokeProcessRowAsync(job, Row(id: 22));

        await repo.Received(1).MarkOutboxProcessedAsync(22, "909", Arg.Any<CancellationToken>());
        await repo.DidNotReceive().MarkOutboxFailedAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Scenario3_NotFound908_MarksFinalFailureNoRetry()
    {
        var (repo, _, job) = BuildSut(new InmaOptOutResult
        {
            Success = false,
            StatusCode = "908",
            Message = "Contact not found",
            HttpStatusCode = 200,
        });

        await InvokeProcessRowAsync(job, Row(id: 33));

        await repo.Received(1).MarkOutboxFailedAsync(
            33, "908", "Contact not found", isFinal: true, Arg.Any<int>(), Arg.Any<CancellationToken>());
        await repo.DidNotReceive().MarkOutboxProcessedAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Scenario4_NetworkFailure_MarksTransientFailure()
    {
        var (repo, _, job) = BuildSut(new InmaOptOutResult
        {
            Success = false,
            StatusCode = "NETWORK",
            Message = "Timeout",
            HttpStatusCode = 0,
        });

        await InvokeProcessRowAsync(job, Row(id: 44));

        await repo.Received(1).MarkOutboxFailedAsync(
            44, "NETWORK", "Timeout", isFinal: false, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Scenario5_NoOpMode_ParksRowAsSkippedNoOp()
    {
        var (repo, _, job) = BuildSut(new InmaOptOutResult
        {
            Success = false,
            StatusCode = "SKIPPED-NOOP",
            Message = "NoOp mode active",
            HttpStatusCode = 0,
        });

        await InvokeProcessRowAsync(job, Row(id: 55));

        await repo.Received(1).MarkOutboxSkippedNoOpAsync(55, Arg.Any<CancellationToken>());
        await repo.DidNotReceive().MarkOutboxFailedAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OptInEventType_DispatchesToPushOptIn()
    {
        var (_, client, job) = BuildSut(new InmaOptOutResult
        {
            Success = true,
            StatusCode = "0",
            HttpStatusCode = 200,
        });

        await InvokeProcessRowAsync(job, Row(id: 66, eventType: "opt_in"));

        await client.Received(1).PushOptInAsync(Arg.Any<InmaOptOutRequest>(), Arg.Any<CancellationToken>());
        await client.DidNotReceive().PushOptOutAsync(Arg.Any<InmaOptOutRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClientThrows_MarksTransientFailureAndSurvives()
    {
        var fakeDb = new PostgresConnectionFactory("Host=localhost;Port=5432;Database=t;Username=t;Password=t");
        var logger = FakeLoggerFactory.Create("InmaOptOutSyncJob.Test");
        var repo = Substitute.For<OutboundRepository>(fakeDb, logger);
        var client = Substitute.For<IInmaContactOptOutClient>();
        client.PushOptOutAsync(Arg.Any<InmaOptOutRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InmaOptOutResult>(new HttpRequestException("connection refused")));

        var job = new InmaOptOutSyncJob(repo, client, logger, DefaultOptions());
        await InvokeProcessRowAsync(job, Row(id: 77));

        await repo.Received(1).MarkOutboxFailedAsync(
            77, "NETWORK", Arg.Is<string>(s => s != null && s.Contains("connection refused")),
            isFinal: false, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TransactionalEventRegistry_AllowList_ExactMatchOnly()
    {
        // Q7 guard: prefix spoofing ('offer_sent_spam') must NOT bypass opt-out.
        Invekto.Shared.Services.TransactionalEventRegistry.IsTransactional("appointment_confirmed_en")
            .Should().BeTrue();
        Invekto.Shared.Services.TransactionalEventRegistry.IsTransactional("offer_sent_consult_tr")
            .Should().BeTrue();
        Invekto.Shared.Services.TransactionalEventRegistry.IsTransactional("offer_sent_spam")
            .Should().BeFalse();
        Invekto.Shared.Services.TransactionalEventRegistry.IsTransactional("APPOINTMENT_CONFIRMED_EN")
            .Should().BeFalse();   // case-sensitive
        Invekto.Shared.Services.TransactionalEventRegistry.IsTransactional(null)
            .Should().BeFalse();
        Invekto.Shared.Services.TransactionalEventRegistry.IsTransactional("")
            .Should().BeFalse();
    }
}
