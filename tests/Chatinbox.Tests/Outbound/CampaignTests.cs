using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;
using Chatinbox.Tests._Shared.DataGenerators;
using Chatinbox.Shared.DTOs.Outbound;
using NSubstitute;

namespace Chatinbox.Tests.Outbound;

public class CampaignTests : IClassFixture<OutboundTestFactory>
{
    private readonly OutboundTestFactory _factory;
    private readonly HttpClient _client;

    public CampaignTests(OutboundTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task CreateCampaign_ValidData_Returns201()
    {
        _factory.FakeRepo.GetTemplateByIdAsync(
            Arg.Any<int>(), Arg.Is(1), Arg.Any<CancellationToken>())
            .Returns(new TemplateDto
            {
                Id = 1, Name = "Campaign Template", TriggerEvent = "manual",
                MessageTemplate = "Merhaba {{customer_name}}!", IsActive = true, Lang = "tr"
            });

        _factory.FakeRepo.CreateCampaignAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(1);

        _factory.FakeRepo.GetCampaignAsync(
            Arg.Any<int>(), Arg.Is(1), Arg.Any<CancellationToken>())
            .Returns(new CampaignResponse
            {
                Id = 1, Name = "Test Campaign", TriggerType = "manual",
                TemplateId = 1, Status = "draft", CreatedAt = DateTime.UtcNow
            });

        var request = new
        {
            name = "Test Campaign",
            trigger_type = "manual",
            template_id = 1
        };

        var response = await _client.PostAsJsonAsync("/api/v1/campaigns", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ListCampaigns_ReturnsOk()
    {
        _factory.FakeRepo.ListCampaignsAsync(
            Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<CampaignResponse>
            {
                new() { Id = 1, Name = "Campaign A", Status = "active", TriggerType = "manual" }
            });

        var response = await _client.GetAsync("/api/v1/campaigns");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Campaign A");
    }

    [Fact]
    public async Task GetCampaign_Existing_ReturnsOk()
    {
        _factory.FakeRepo.GetCampaignAsync(
            Arg.Any<int>(), Arg.Is(1), Arg.Any<CancellationToken>())
            .Returns(new CampaignResponse
            {
                Id = 1, Name = "Test", Status = "active", TriggerType = "manual"
            });

        var response = await _client.GetAsync("/api/v1/campaigns/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCampaign_NotFound_Returns404()
    {
        _factory.FakeRepo.GetCampaignAsync(
            Arg.Any<int>(), Arg.Is(999), Arg.Any<CancellationToken>())
            .Returns((CampaignResponse?)null);

        var response = await _client.GetAsync("/api/v1/campaigns/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PauseCampaign_Existing_ReturnsOk()
    {
        _factory.FakeRepo.UpdateCampaignStatusAsync(
            Arg.Any<int>(), Arg.Is(1), Arg.Is("paused"), Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.PostAsync("/api/v1/campaigns/1/pause", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PauseCampaign_NotFound_Returns404()
    {
        _factory.FakeRepo.UpdateCampaignStatusAsync(
            Arg.Any<int>(), Arg.Is(999), Arg.Is("paused"), Arg.Any<CancellationToken>())
            .Returns(false);

        var response = await _client.PostAsync("/api/v1/campaigns/999/pause", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCampaignRoi_Existing_ReturnsOk()
    {
        _factory.FakeRepo.GetCampaignRoiAsync(
            Arg.Any<int>(), Arg.Is(1), Arg.Any<CancellationToken>())
            .Returns(new CampaignRoiResponse
            {
                CampaignId = 1, CampaignName = "ROI Test",
                TotalSent = 100, TotalDelivered = 90,
                TotalConversions = 10, ConversionRate = 0.11,
                TotalRevenue = 5000m
            });

        var response = await _client.GetAsync("/api/v1/campaigns/1/roi");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("5000");
    }

    [Fact]
    public async Task GetCampaignRoi_NotFound_Returns404()
    {
        _factory.FakeRepo.GetCampaignRoiAsync(
            Arg.Any<int>(), Arg.Is(999), Arg.Any<CancellationToken>())
            .Returns((CampaignRoiResponse?)null);

        var response = await _client.GetAsync("/api/v1/campaigns/999/roi");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Campaigns_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/campaigns");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RecordConversion_ValidData_Returns201()
    {
        _factory.FakeRepo.RecordConversionAsync(
            Arg.Any<int>(), Arg.Any<long?>(), Arg.Any<int?>(),
            Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(1);

        var request = new
        {
            campaign_id = 1,
            conversion_type = "purchase",
            value_amount = 299.90m
        };

        var response = await _client.PostAsJsonAsync("/api/v1/conversions", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
