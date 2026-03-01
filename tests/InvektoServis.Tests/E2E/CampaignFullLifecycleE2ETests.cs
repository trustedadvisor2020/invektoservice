using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using Invekto.Shared.DTOs.Outbound;
using NSubstitute;

namespace InvektoServis.Tests.E2E;

/// <summary>
/// E2E: Campaign create → activate → record conversion → check ROI
/// </summary>
public class CampaignFullLifecycleE2ETests : IClassFixture<OutboundTestFactory>
{
    private readonly OutboundTestFactory _factory;
    private readonly HttpClient _client;

    public CampaignFullLifecycleE2ETests(OutboundTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task FullCampaignLifecycle_Create_Activate_Convert_ROI()
    {
        // Step 1: Create campaign
        _factory.FakeRepo.GetTemplateByIdAsync(
            Arg.Any<int>(), Arg.Is(5), Arg.Any<CancellationToken>())
            .Returns(new TemplateDto
            {
                Id = 5, Name = "Campaign Template", TriggerEvent = "manual",
                MessageTemplate = "Kampanya mesaji!", IsActive = true, Lang = "tr"
            });

        _factory.FakeRepo.CreateCampaignAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(100);

        _factory.FakeRepo.GetCampaignAsync(
            Arg.Any<int>(), Arg.Is(100), Arg.Any<CancellationToken>())
            .Returns(new CampaignResponse
            {
                Id = 100, Name = "E2E Campaign", TriggerType = "manual",
                TemplateId = 5, Status = "draft", CreatedAt = DateTime.UtcNow
            });

        var createResponse = await _client.PostAsJsonAsync("/api/v1/campaigns", new
        {
            name = "E2E Campaign",
            trigger_type = "manual",
            template_id = 5
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 2: Activate campaign
        _factory.FakeRepo.GetCampaignAsync(
            Arg.Any<int>(), Arg.Is(100), Arg.Any<CancellationToken>())
            .Returns(new CampaignResponse
            {
                Id = 100, Name = "E2E Campaign", TriggerType = "manual",
                TemplateId = 5, Status = "draft", CreatedAt = DateTime.UtcNow
            });

        _factory.FakeRepo.UpdateCampaignStatusAsync(
            Arg.Any<int>(), Arg.Is(100), Arg.Is("active"), Arg.Any<CancellationToken>())
            .Returns(true);

        var activateResponse = await _client.PostAsync("/api/v1/campaigns/100/activate", null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 3: Record conversion
        _factory.FakeRepo.RecordConversionAsync(
            Arg.Any<int>(), Arg.Any<long?>(), Arg.Is(100),
            Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(1);

        var conversionResponse = await _client.PostAsJsonAsync("/api/v1/conversions", new
        {
            campaign_id = 100,
            conversion_type = "purchase",
            value_amount = 499.90m
        });
        conversionResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 4: Check ROI
        _factory.FakeRepo.GetCampaignRoiAsync(
            Arg.Any<int>(), Arg.Is(100), Arg.Any<CancellationToken>())
            .Returns(new CampaignRoiResponse
            {
                CampaignId = 100, CampaignName = "E2E Campaign",
                TotalSent = 50, TotalDelivered = 45,
                TotalConversions = 5, ConversionRate = 0.11,
                TotalRevenue = 2499.50m
            });

        var roiResponse = await _client.GetAsync("/api/v1/campaigns/100/roi");
        roiResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var roiContent = await roiResponse.Content.ReadAsStringAsync();
        roiContent.Should().Contain("2499.5");
        roiContent.Should().Contain("E2E Campaign");
    }

    [Fact]
    public async Task CampaignLifecycle_PauseAndResume()
    {
        // Pause
        _factory.FakeRepo.UpdateCampaignStatusAsync(
            Arg.Any<int>(), Arg.Is(100), Arg.Is("paused"), Arg.Any<CancellationToken>())
            .Returns(true);

        var pauseResponse = await _client.PostAsync("/api/v1/campaigns/100/pause", null);
        pauseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Re-activate
        _factory.FakeRepo.GetCampaignAsync(
            Arg.Any<int>(), Arg.Is(100), Arg.Any<CancellationToken>())
            .Returns(new CampaignResponse
            {
                Id = 100, Name = "E2E Campaign", TriggerType = "manual",
                TemplateId = 5, Status = "paused", CreatedAt = DateTime.UtcNow
            });

        _factory.FakeRepo.UpdateCampaignStatusAsync(
            Arg.Any<int>(), Arg.Is(100), Arg.Is("active"), Arg.Any<CancellationToken>())
            .Returns(true);

        var activateResponse = await _client.PostAsync("/api/v1/campaigns/100/activate", null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
