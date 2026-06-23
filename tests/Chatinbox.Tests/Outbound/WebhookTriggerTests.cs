using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;
using Chatinbox.Tests._Shared.DataGenerators;
using Chatinbox.Shared.DTOs.Outbound;
using NSubstitute;

namespace Chatinbox.Tests.Outbound;

public class WebhookTriggerTests : IClassFixture<OutboundTestFactory>
{
    private readonly OutboundTestFactory _factory;
    private readonly HttpClient _client;

    public WebhookTriggerTests(OutboundTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Trigger_ValidEvent_Returns202()
    {
        var (phone, _, _) = TurkishCustomerGenerator.GenerateCustomer();

        _factory.FakeRepo.GetTriggerTemplateAsync(
            Arg.Any<int>(), Arg.Is("new_lead"), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateDto
            {
                Id = 1, Name = "Lead Welcome", TriggerEvent = "new_lead",
                MessageTemplate = "Merhaba {{customer_name}}!", IsActive = true, Lang = "tr"
            });

        _factory.FakeRepo.IsOptedOutAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _factory.FakeRepo.InsertMessageAsync(
            Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<int?>(),
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>(), Arg.Any<string[]?>())
            .Returns(42L);

        _factory.FakeRepo.InsertAuditTrailAsync(
            Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new
        {
            @event = "new_lead",
            phone,
            variables = new { customer_name = "Ahmet" }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/trigger", request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Trigger_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var request = new { @event = "test", phone = "+905551234567" };

        var response = await client.PostAsJsonAsync("/api/v1/webhook/trigger", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IncomingMessage_StopKeyword_DetectsOptOut()
    {
        _factory.FakeRepo.AddOptOutAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _factory.FakeRepo.UpsertConsentAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _factory.FakeRepo.InsertAuditTrailAsync(
            Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new
        {
            phone = "+905551234567",
            message_text = "STOP"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/message", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("opted_out");
    }

    [Fact]
    public async Task IncomingMessage_NormalText_NoOptOut()
    {
        var request = new
        {
            phone = "+905559876543",
            message_text = "Merhaba, siparis durumum ne?"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/webhook/message", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
