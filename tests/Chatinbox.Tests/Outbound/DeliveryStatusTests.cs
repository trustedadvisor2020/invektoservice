using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Chatinbox.Outbound.Data;
using Chatinbox.Tests._Shared.Factories;
using NSubstitute;

namespace Chatinbox.Tests.Outbound;

/// <summary>
/// /api/v1/webhook/delivery-status — PR-3b-2 rewired the handler onto the single atomic
/// OutboundRepository.ApplyDeliveryStatusAsync (tenant-scoped lookup + monotonic downgrade guard +
/// transition-gated counter). These tests assert the HTTP contract over a mocked repo: validation,
/// 404 vs 200, idempotent no-op, the failed+reason path, and the SOFT InstanceID cross-check.
/// </summary>
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
        _factory.FakeRepo.ApplyDeliveryStatusAsync(
            Arg.Any<int>(), Arg.Is("ext-msg-001"), Arg.Is("delivered"), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new OutboundRepository.DeliveryStatusApplyResult(Matched: true, Applied: true, PreviousStatus: "sent", RowInstanceId: null));

        var request = new { external_message_id = "ext-msg-001", status = "delivered" };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/delivery-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeliveryStatus_UnknownExternalId_Returns404()
    {
        _factory.FakeRepo.ApplyDeliveryStatusAsync(
            Arg.Any<int>(), Arg.Is("unknown-id"), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new OutboundRepository.DeliveryStatusApplyResult(Matched: false, Applied: false, PreviousStatus: null, RowInstanceId: null));

        var request = new { external_message_id = "unknown-id", status = "delivered" };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/delivery-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeliveryStatus_InvalidStatus_Returns400()
    {
        var request = new { external_message_id = "ext-msg-001", status = "invalid_status" };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/delivery-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeliveryStatus_MissingExternalId_Returns400()
    {
        var request = new { external_message_id = "", status = "delivered" };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/delivery-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeliveryStatus_FailedWithReason_ForwardsReason_ReturnsOk()
    {
        _factory.FakeRepo.ApplyDeliveryStatusAsync(
            Arg.Any<int>(), Arg.Is("ext-msg-002"), Arg.Is("failed"), Arg.Is("phone_unreachable"), Arg.Any<CancellationToken>())
            .Returns(new OutboundRepository.DeliveryStatusApplyResult(Matched: true, Applied: true, PreviousStatus: "sent", RowInstanceId: null));

        var request = new { external_message_id = "ext-msg-002", status = "failed", failed_reason = "phone_unreachable" };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/delivery-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.FakeRepo.Received().ApplyDeliveryStatusAsync(
            Arg.Any<int>(), "ext-msg-002", "failed", "phone_unreachable", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliveryStatus_IdempotentNoOp_StillReturnsOk()
    {
        // A 'delivered' ack arriving after the row is already 'read' — matched but not applied (downgrade
        // guard / duplicate). The endpoint still returns 200 so WapCRM does not retry-storm.
        _factory.FakeRepo.ApplyDeliveryStatusAsync(
            Arg.Any<int>(), Arg.Is("ext-msg-003"), Arg.Is("delivered"), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new OutboundRepository.DeliveryStatusApplyResult(Matched: true, Applied: false, PreviousStatus: "read", RowInstanceId: null));

        var request = new { external_message_id = "ext-msg-003", status = "delivered" };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/delivery-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeliveryStatus_InstanceIdMismatch_AppliesAnyway_ReturnsOk()
    {
        // SOFT cross-check: ack instance_id differs from the matched row's — apply anyway (200).
        _factory.FakeRepo.ApplyDeliveryStatusAsync(
            Arg.Any<int>(), Arg.Is("ext-msg-004"), Arg.Is("read"), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new OutboundRepository.DeliveryStatusApplyResult(Matched: true, Applied: true, PreviousStatus: "delivered", RowInstanceId: 999));

        var request = new { external_message_id = "ext-msg-004", status = "read", instance_id = 111 };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/delivery-status", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
