using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using NSubstitute;

namespace InvektoServis.Tests.Outbound;

public class DeliveryStatusTests : IClassFixture<OutboundTestFactory>
{
    private readonly OutboundTestFactory _factory;
    private readonly HttpClient _client;

    public DeliveryStatusTests(OutboundTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task DeliveryStatus_ValidUpdate_ReturnsOk()
    {
        _factory.FakeRepo.FindMessageByExternalIdAsync(
            Arg.Is("ext-msg-001"), Arg.Any<CancellationToken>())
            .Returns((1L, Guid.NewGuid(), 1001));

        _factory.FakeRepo.UpdateMessageStatusAsync(
            Arg.Is(1L), Arg.Is("delivered"), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _factory.FakeRepo.IncrementBroadcastCounterAsync(
            Arg.Any<Guid>(), Arg.Is("delivered"), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new
        {
            external_message_id = "ext-msg-001",
            status = "delivered"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/delivery-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeliveryStatus_UnknownExternalId_Returns404()
    {
        _factory.FakeRepo.FindMessageByExternalIdAsync(
            Arg.Is("unknown-id"), Arg.Any<CancellationToken>())
            .Returns(((long, Guid?, int)?)null);

        var request = new
        {
            external_message_id = "unknown-id",
            status = "delivered"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/delivery-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeliveryStatus_InvalidStatus_Returns400()
    {
        var request = new
        {
            external_message_id = "ext-msg-001",
            status = "invalid_status"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/delivery-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeliveryStatus_MissingExternalId_Returns400()
    {
        var request = new
        {
            external_message_id = "",
            status = "delivered"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/delivery-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeliveryStatus_FailedWithReason_ReturnsOk()
    {
        _factory.FakeRepo.FindMessageByExternalIdAsync(
            Arg.Is("ext-msg-002"), Arg.Any<CancellationToken>())
            .Returns((2L, (Guid?)null, 1001));

        _factory.FakeRepo.UpdateMessageStatusAsync(
            Arg.Is(2L), Arg.Is("failed"), Arg.Any<string?>(), Arg.Is("phone_unreachable"), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new
        {
            external_message_id = "ext-msg-002",
            status = "failed",
            failed_reason = "phone_unreachable"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/delivery-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
