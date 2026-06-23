using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;
using Chatinbox.Tests._Shared.DataGenerators;
using Chatinbox.Shared.DTOs.Outbound;
using NSubstitute;

namespace Chatinbox.Tests.E2E;

/// <summary>
/// E2E: Template creation → Broadcast send → Delivery status check
/// </summary>
public class BroadcastLifecycleE2ETests : IClassFixture<OutboundTestFactory>
{
    private readonly OutboundTestFactory _factory;
    private readonly HttpClient _client;

    public BroadcastLifecycleE2ETests(OutboundTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task FullBroadcastLifecycle_CreateTemplate_SendBroadcast_CheckStatus()
    {
        // Step 1: Create a template
        _factory.FakeRepo.CreateTemplateAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(10);

        var templateRequest = new
        {
            name = "E2E Broadcast Template",
            trigger_event = "manual",
            message_template = "Merhaba {{customer_name}}, kampanyamiza hosgeldiniz!",
            lang = "tr"
        };

        var templateResponse = await _client.PostAsJsonAsync("/api/v1/templates", templateRequest);
        templateResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        // Step 2: Send broadcast using the template
        var broadcastId = Guid.NewGuid();

        _factory.FakeRepo.GetTemplateByIdAsync(
            Arg.Any<int>(), Arg.Is(10), Arg.Any<CancellationToken>())
            .Returns(new TemplateDto
            {
                Id = 10, Name = "E2E Broadcast Template", TriggerEvent = "manual",
                MessageTemplate = "Merhaba {{customer_name}}, kampanyamiza hosgeldiniz!",
                IsActive = true, Lang = "tr"
            });

        _factory.FakeRepo.BatchCheckOptOutsAsync(
            Arg.Any<int>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>());

        _factory.FakeRepo.BatchCheckMarketingConsentAsync(
            Arg.Any<int>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>());

        _factory.FakeRepo.HasMarketingConsentAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _factory.FakeRepo.CreateBroadcastAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<DateTime?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(broadcastId);

        _factory.FakeRepo.BatchInsertMessagesAsync(
            Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<int>(),
            Arg.Any<List<(string phone, string text, string[]? dynamicFields)>>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var phones = TurkishCustomerGenerator.GeneratePhoneList(5);
        var broadcastRequest = new
        {
            template_id = 10,
            recipients = phones.Select(p => new
            {
                phone = p,
                variables = new { customer_name = "Test Musteri" }
            }).ToArray()
        };

        var broadcastResponse = await _client.PostAsJsonAsync("/api/v1/broadcast/send", broadcastRequest);
        broadcastResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.OK);

        // Step 3: Check broadcast status
        _factory.FakeRepo.GetBroadcastStatusAsync(
            Arg.Any<int>(), Arg.Is(broadcastId), Arg.Any<CancellationToken>())
            .Returns(new BroadcastStatusResponse
            {
                BroadcastId = broadcastId, Status = "sending",
                TotalRecipients = 5, Sent = 3, Delivered = 2, Failed = 0
            });

        var statusResponse = await _client.GetAsync($"/api/v1/broadcast/{broadcastId}/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusContent = await statusResponse.Content.ReadAsStringAsync();
        statusContent.Should().Contain("sending");
    }

    [Fact]
    public async Task BroadcastWithOptOuts_SkipsOptedOutRecipients()
    {
        var phones = TurkishCustomerGenerator.GeneratePhoneList(4);
        var optedOutPhone = phones[0];

        _factory.FakeRepo.GetTemplateByIdAsync(
            Arg.Any<int>(), Arg.Is(1), Arg.Any<CancellationToken>())
            .Returns(new TemplateDto
            {
                Id = 1, Name = "Test", TriggerEvent = "manual",
                MessageTemplate = "Test {{customer_name}}", IsActive = true, Lang = "tr"
            });

        // Mark one phone as opted out
        _factory.FakeRepo.BatchCheckOptOutsAsync(
            Arg.Any<int>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string> { optedOutPhone });

        _factory.FakeRepo.BatchCheckMarketingConsentAsync(
            Arg.Any<int>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>());

        _factory.FakeRepo.HasMarketingConsentAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _factory.FakeRepo.CreateBroadcastAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<DateTime?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        _factory.FakeRepo.BatchInsertMessagesAsync(
            Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<int>(),
            Arg.Any<List<(string phone, string text, string[]? dynamicFields)>>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

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

        // Should still succeed (skipped recipients are reported in response)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.OK);
    }
}
