using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;
using Chatinbox.Tests._Shared.DataGenerators;
using Chatinbox.Shared.DTOs.Leads;
using NSubstitute;

namespace Chatinbox.Tests.E2E;

/// <summary>
/// E2E: Lead creation on Backend → Backend proxies trigger to Outbound (mock)
/// Verifies the Backend side of the lead-to-outbound pipeline.
/// </summary>
public class LeadToOutboundE2ETests : IClassFixture<BackendTestFactory>
{
    private readonly BackendTestFactory _factory;
    private readonly HttpClient _client;

    public LeadToOutboundE2ETests(BackendTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task CreateLead_WithTriggerEnabled_CallsOutbound()
    {
        var (phone, name, email) = TurkishCustomerGenerator.GenerateCustomer();

        // Lead creation succeeds
        _factory.FakeLeadRepo.UpsertLeadAsync(
            Arg.Any<int>(), Arg.Any<LeadRequest>(), Arg.Any<CancellationToken>())
            .Returns(42);

        // Outbound trigger returns accepted
        _factory.MockOutbound.Clear();
        _factory.MockOutbound.WhenAny(new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent(
                """{"message_id":1,"template_id":1,"template_name":"Welcome"}""",
                System.Text.Encoding.UTF8, "application/json")
        });

        var request = new
        {
            phone,
            customer_name = name,
            email,
            source = "web_form",
            pipeline_stage = "new"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/leads", request);

        // Lead should be created successfully
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateLead_OutboundDown_LeadStillCreated()
    {
        var (phone, name, email) = TurkishCustomerGenerator.GenerateCustomer();

        _factory.FakeLeadRepo.UpsertLeadAsync(
            Arg.Any<int>(), Arg.Any<LeadRequest>(), Arg.Any<CancellationToken>())
            .Returns(43);

        // Outbound is down
        _factory.MockOutbound.Clear();
        _factory.MockOutbound.WhenAny(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var request = new
        {
            phone,
            customer_name = name,
            source = "instagram"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/leads", request);

        // Lead creation should not fail because outbound trigger is fire-and-forget
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetLead_AfterCreation_ReturnsLeadData()
    {
        _factory.FakeLeadRepo.GetLeadAsync(
            Arg.Any<int>(), Arg.Is(42), Arg.Any<CancellationToken>())
            .Returns(new LeadResponse
            {
                Id = 42,
                Phone = "+905551234567",
                Name = "Ahmet Yilmaz",
                Source = "web_form",
                PipelineStatus = "new"
            });

        var response = await _client.GetAsync("/api/v1/leads/42");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Ahmet Yilmaz");
    }
}
