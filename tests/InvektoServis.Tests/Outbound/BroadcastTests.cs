using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using InvektoServis.Tests._Shared.DataGenerators;
using Invekto.Shared.DTOs.Outbound;
using NSubstitute;

namespace InvektoServis.Tests.Outbound;

public class BroadcastTests : IClassFixture<OutboundTestFactory>
{
    private readonly OutboundTestFactory _factory;
    private readonly HttpClient _client;

    public BroadcastTests(OutboundTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task SendBroadcast_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/broadcast/send", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SendBroadcast_ValidData_Returns202()
    {
        var broadcastId = Guid.NewGuid();

        _factory.FakeRepo.GetTemplateByIdAsync(
            Arg.Any<int>(), Arg.Is(1), Arg.Any<CancellationToken>())
            .Returns(new TemplateDto
            {
                Id = 1, Name = "Test", TriggerEvent = "manual",
                MessageTemplate = "Merhaba {{customer_name}}!", IsActive = true, Lang = "tr"
            });

        _factory.FakeRepo.BatchCheckOptOutsAsync(
            Arg.Any<int>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>());

        _factory.FakeRepo.HasMarketingConsentAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _factory.FakeRepo.BatchCheckMarketingConsentAsync(
            Arg.Any<int>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>());

        _factory.FakeRepo.CreateBroadcastAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<DateTime?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(broadcastId);

        _factory.FakeRepo.BatchInsertMessagesAsync(
            Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<int>(),
            Arg.Any<List<(string phone, string text, string[]? dynamicFields)>>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var phones = TurkishCustomerGenerator.GeneratePhoneList(3);
        var request = new
        {
            template_id = 1,
            recipients = phones.Select(p => new
            {
                phone = p,
                variables = new { customer_name = "Test" }
            }).ToArray()
        };

        var response = await _client.PostAsJsonAsync("/api/v1/broadcast/send", request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBroadcastStatus_ValidId_ReturnsStatus()
    {
        var broadcastId = Guid.NewGuid();
        _factory.FakeRepo.GetBroadcastStatusAsync(
            Arg.Any<int>(), Arg.Is(broadcastId), Arg.Any<CancellationToken>())
            .Returns(new BroadcastStatusResponse
            {
                BroadcastId = broadcastId, Status = "completed",
                TotalRecipients = 10, Sent = 10, Delivered = 8, Failed = 2
            });

        var response = await _client.GetAsync($"/api/v1/broadcast/{broadcastId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("completed");
    }

    [Fact]
    public async Task GetBroadcastStatus_NotFound_Returns404()
    {
        var unknownId = Guid.NewGuid();
        _factory.FakeRepo.GetBroadcastStatusAsync(
            Arg.Any<int>(), Arg.Is(unknownId), Arg.Any<CancellationToken>())
            .Returns((BroadcastStatusResponse?)null);

        var response = await _client.GetAsync($"/api/v1/broadcast/{unknownId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
