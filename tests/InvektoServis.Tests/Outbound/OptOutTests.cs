using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using InvektoServis.Tests._Shared.DataGenerators;
using NSubstitute;

namespace InvektoServis.Tests.Outbound;

public class OptOutTests : IClassFixture<OutboundTestFactory>
{
    private readonly OutboundTestFactory _factory;
    private readonly HttpClient _client;

    public OptOutTests(OutboundTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task AddOptOut_ValidPhone_Returns200()
    {
        var (phone, _, _) = TurkishCustomerGenerator.GenerateCustomer();
        _factory.FakeRepo.AddOptOutAsync(
            Arg.Any<int>(), Arg.Is(phone), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.PostAsJsonAsync("/api/v1/optout", new { phone, reason = "customer_request" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveOptOut_Existing_Returns200()
    {
        _factory.FakeRepo.RemoveOptOutAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync("/api/v1/optout/+905551234567");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveOptOut_NotFound_Returns404()
    {
        _factory.FakeRepo.RemoveOptOutAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var response = await _client.DeleteAsync("/api/v1/optout/+905559999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CheckOptOut_OptedOutPhone_ReturnsTrue()
    {
        _factory.FakeRepo.GetOptOutDateAsync(
            Arg.Any<int>(), Arg.Is("+905551234567"), Arg.Any<CancellationToken>())
            .Returns(DateTime.UtcNow.AddDays(-1));

        var response = await _client.GetAsync("/api/v1/optout/check/+905551234567");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("true");
    }

    [Fact]
    public async Task CheckOptOut_NormalPhone_ReturnsFalse()
    {
        _factory.FakeRepo.GetOptOutDateAsync(
            Arg.Any<int>(), Arg.Is("+905559876543"), Arg.Any<CancellationToken>())
            .Returns((DateTime?)null);

        var response = await _client.GetAsync("/api/v1/optout/check/+905559876543");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("false");
    }

    // ───────────────────────── FEAT-J2 ─────────────────────────

    [Fact]
    public async Task WebhookMessage_StopKeywordWithInstance_EnqueuesInmaOutbox()
    {
        // Incoming WA webhook hits /api/v1/webhook/message; instance_id carried
        // in the payload (FEAT-J2 AC4). STOP keyword triggers opt-out enqueue.
        var (phone, _, _) = TurkishCustomerGenerator.GenerateCustomer();
        _factory.FakeRepo.AddOptOutAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _factory.FakeRepo.EnqueueOptOutSyncAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/message", new
        {
            phone,
            message_text = "STOP",
            instance_id = 101,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.FakeRepo.Received(1).EnqueueOptOutSyncAsync(
            Arg.Any<int>(),
            Arg.Is(phone),
            Arg.Is(101),
            Arg.Is("opt_out"),
            Arg.Is("all"),
            Arg.Any<string?>(),
            Arg.Is("whatsapp"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WebhookMessage_StopKeywordWithoutInstance_SkipsEnqueueButRegisters()
    {
        // Legacy caller (no instance_id field): INSE opt-out still succeeds, but
        // outbox enqueue is skipped with a warn — INMA sync is best-effort.
        var (phone, _, _) = TurkishCustomerGenerator.GenerateCustomer();
        _factory.FakeRepo.AddOptOutAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/message", new
        {
            phone,
            message_text = "STOP",
            // instance_id omitted
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.FakeRepo.DidNotReceive().EnqueueOptOutSyncAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
