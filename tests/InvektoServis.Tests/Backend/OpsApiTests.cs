using System.Net;
using System.Text.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;

namespace InvektoServis.Tests.Backend;

public class OpsApiTests : IClassFixture<BackendTestFactory>
{
    private readonly BackendTestFactory _factory;

    public OpsApiTests(BackendTestFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/ops")]
    [InlineData("/api/ops/health")]
    [InlineData("/api/ops/logs/stream")]
    [InlineData("/api/ops/stats/errors")]
    public async Task OpsEndpoints_RequireAuth(string endpoint)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OpsHealth_ReturnsServiceList_WithAuth()
    {
        var client = _factory.CreateOpsClient();

        var response = await client.GetAsync("/api/ops/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.TryGetProperty("services", out var services).Should().BeTrue();
        services.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task OpsLogStream_ReturnsEntries_WithAuth()
    {
        var client = _factory.CreateOpsClient();

        var response = await client.GetAsync("/api/ops/logs/stream?limit=10");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.TryGetProperty("entries", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OpsErrorStats_ReturnsBuckets_WithAuth()
    {
        var client = _factory.CreateOpsClient();

        var response = await client.GetAsync("/api/ops/stats/errors?hours=24");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.TryGetProperty("buckets", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("total", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OpsLogContext_RequiresFileAndLine()
    {
        var client = _factory.CreateOpsClient();

        var response = await client.GetAsync("/api/ops/logs/context");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OpsEndpoints_ReturnsPostmanCollection()
    {
        var client = _factory.CreateOpsClient();

        var response = await client.GetAsync("/api/ops/endpoints");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("endpoint");
    }
}
