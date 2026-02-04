using System.Net;
using System.Text.Json;
using FluentAssertions;
using Invekto.ChatAnalysis.Tests.Fixtures;

namespace Invekto.ChatAnalysis.Tests.IntegrationTests;

public class HealthEndpointTests : IClassFixture<ChatAnalysisWebApplicationFactory>
{
    private readonly ChatAnalysisWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HealthEndpointTests(ChatAnalysisWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        json.RootElement.GetProperty("status").GetString().Should().Be("ok");
        json.RootElement.GetProperty("service").GetString().Should().Be("Invekto.ChatAnalysis");
        json.RootElement.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Ready_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/ready");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        json.RootElement.GetProperty("status").GetString().Should().Be("ok");
        json.RootElement.GetProperty("service").GetString().Should().Be("Invekto.ChatAnalysis");
    }
}
