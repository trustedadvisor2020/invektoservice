using System.Net;
using System.Text.Json;
using FluentAssertions;
using Invekto.Backend.Tests.Fixtures;

namespace Invekto.Backend.Tests.IntegrationTests;

public class OpsApiTests : IClassFixture<BackendWebApplicationFactory>
{
    private readonly BackendWebApplicationFactory _factory;

    public OpsApiTests(BackendWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ops_RequiresAuth()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ops");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ops_ReturnsOk_WithValidAuth()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/ops");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpsHealth_RequiresAuth()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/ops/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OpsHealth_ReturnsServiceList_WithAuth()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/ops/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.TryGetProperty("services", out var services).Should().BeTrue();
        services.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task OpsLogStream_RequiresAuth()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/ops/logs/stream");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OpsLogStream_ReturnsEntries_WithAuth()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/ops/logs/stream?limit=10");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.TryGetProperty("entries", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("hasMore", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OpsErrorStats_RequiresAuth()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/ops/stats/errors");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OpsErrorStats_ReturnsStats_WithAuth()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/ops/stats/errors?hours=24");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.TryGetProperty("buckets", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("total", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OpsLogContext_RequiresFileAndLine()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/ops/logs/context");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("/ops/errors")]
    [InlineData("/ops/slow")]
    [InlineData("/ops/search?requestId=test")]
    public async Task OpsEndpoints_RequireAuth(string endpoint)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(endpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
