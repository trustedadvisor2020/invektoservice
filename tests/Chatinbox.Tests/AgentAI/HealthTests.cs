using System.Net;
using System.Text.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;

namespace Chatinbox.Tests.AgentAI;

public class HealthTests : IClassFixture<AgentAITestFactory>
{
    private readonly HttpClient _client;

    public HealthTests(AgentAITestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk_WithoutAuth()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ReturnsValidJson()
    {
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("status").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task Endpoints_ReturnsDiscovery()
    {
        var response = await _client.GetAsync("/api/ops/endpoints");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
