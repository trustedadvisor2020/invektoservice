using System.Net;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using InvektoServis.Tests._Shared.Infrastructure;
using Invekto.Shared.DTOs.AgentAI;

namespace InvektoServis.Tests.AgentAI;

public class OrderCardTests : IClassFixture<AgentAITestFactory>
{
    private readonly AgentAITestFactory _factory;
    private readonly HttpClient _client;

    public OrderCardTests(AgentAITestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task OrderCard_ValidPhone_ReturnsOrders()
    {
        // Setup Integrations mock to return order data
        _factory.MockIntegrations.Clear();
        var ordersResponse = """
        {
            "customerPhone": "+905551234567",
            "orders": [
                { "provider": "trendyol", "externalOrderId": "TY-123", "status": "shipped", "totalAmount": 199.90, "currency": "TRY" }
            ]
        }
        """;
        _factory.MockIntegrations.WhenAny(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ordersResponse, System.Text.Encoding.UTF8, "application/json")
        });

        var response = await _client.GetAsync("/api/v1/ecom/order-card/+905551234567");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("trendyol");
    }

    [Fact]
    public async Task OrderCard_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/ecom/order-card/+905551234567");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OrderCard_IntegrationsUnavailable_Returns502()
    {
        _factory.MockIntegrations.Clear();
        _factory.MockIntegrations.WhenAny(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var response = await _client.GetAsync("/api/v1/ecom/order-card/+905559999999");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadGateway, HttpStatusCode.InternalServerError);
    }
}
