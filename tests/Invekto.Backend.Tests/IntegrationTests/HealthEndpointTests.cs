using System.Net;
using System.Text.Json;
using FluentAssertions;
using Invekto.Backend.Tests.Fixtures;

namespace Invekto.Backend.Tests.IntegrationTests;

public class HealthEndpointTests : IClassFixture<BackendWebApplicationFactory>
{
    private readonly BackendWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HealthEndpointTests(BackendWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk_WithoutAuth()
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
        json.RootElement.GetProperty("service").GetString().Should().Be("Invekto.Backend");
        json.RootElement.TryGetProperty("timestamp", out _).Should().BeTrue();
    }
}
