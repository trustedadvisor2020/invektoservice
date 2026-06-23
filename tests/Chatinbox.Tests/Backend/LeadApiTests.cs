using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;
using Chatinbox.Tests._Shared.DataGenerators;
using Chatinbox.Tests._Shared.Infrastructure;
using Chatinbox.Shared.DTOs.Leads;
using NSubstitute;

namespace Chatinbox.Tests.Backend;

public class LeadApiTests : IClassFixture<BackendTestFactory>
{
    private readonly BackendTestFactory _factory;
    private readonly HttpClient _client;

    public LeadApiTests(BackendTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task CreateLead_WithValidData_ReturnsCreated()
    {
        var (phone, name, email) = TurkishCustomerGenerator.GenerateCustomer();
        _factory.FakeLeadRepo.UpsertLeadAsync(
            Arg.Any<int>(), Arg.Any<LeadRequest>(), Arg.Any<CancellationToken>())
            .Returns(42);

        var request = new LeadRequest { Phone = phone, Name = name, Email = email, Source = "whatsapp" };
        var response = await _client.PostAsJsonAsync("/api/v1/leads", request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateLead_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var request = new LeadRequest { Phone = "+905551234567", Name = "Test" };

        var response = await client.PostAsJsonAsync("/api/v1/leads", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLead_ExistingId_ReturnsLead()
    {
        _factory.FakeLeadRepo.GetLeadAsync(
            Arg.Any<int>(), Arg.Is(1), Arg.Any<CancellationToken>())
            .Returns(new LeadResponse
            {
                Id = 1, Phone = "+905551234567", Name = "Ahmet Yilmaz",
                PipelineStatus = "new", Score = 50
            });

        var response = await _client.GetAsync("/api/v1/leads/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Ahmet Yilmaz");
    }

    [Fact]
    public async Task GetLead_NonExistent_Returns404()
    {
        _factory.FakeLeadRepo.GetLeadAsync(
            Arg.Any<int>(), Arg.Is(999), Arg.Any<CancellationToken>())
            .Returns((LeadResponse?)null);

        var response = await _client.GetAsync("/api/v1/leads/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListLeads_ReturnsPagedResults()
    {
        _factory.FakeLeadRepo.ListLeadsAsync(
            Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<bool?>(),
            Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeadResponse>
            {
                new() { Id = 1, Phone = "+905551111111", Name = "Lead 1", PipelineStatus = "new" },
                new() { Id = 2, Phone = "+905552222222", Name = "Lead 2", PipelineStatus = "contacted" }
            });

        var response = await _client.GetAsync("/api/v1/leads?limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHotLeads_ReturnsOnlyHotLeads()
    {
        _factory.FakeLeadRepo.GetHotLeadsAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeadResponse>
            {
                new() { Id = 1, Phone = "+905551111111", Name = "Hot Lead", IsHot = true, Score = 90 }
            });

        var response = await _client.GetAsync("/api/v1/leads/hot");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFunnelStats_ReturnsStatusCounts()
    {
        _factory.FakeLeadRepo.GetFunnelStatsAsync(
            Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LeadFunnelStatsResponse
            {
                TotalLeads = 100,
                ByStatus = new Dictionary<string, int>
                {
                    ["new"] = 40, ["contacted"] = 30, ["consultation"] = 20, ["patient"] = 10
                }
            });

        var response = await _client.GetAsync("/api/v1/leads/funnel");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("100");
    }

    [Fact]
    public async Task GetActivities_ReturnsActivityHistory()
    {
        _factory.FakeLeadRepo.GetActivitiesAsync(
            Arg.Any<int>(), Arg.Is(1), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeadActivityResponse>
            {
                new() { Id = 1, LeadId = 1, ActivityType = "status_change", OldValue = "new", NewValue = "contacted" }
            });

        var response = await _client.GetAsync("/api/v1/leads/1/activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
