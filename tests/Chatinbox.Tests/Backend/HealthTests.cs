using System.Net;
using System.Text.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;

namespace Chatinbox.Tests.Backend;

public class HealthTests : IClassFixture<BackendTestFactory>
{
    private readonly HttpClient _client;

    public HealthTests(BackendTestFactory factory)
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
    public async Task Health_ReturnsValidJson_WithServiceName()
    {
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("status").GetString().Should().Be("ok");
        json.RootElement.GetProperty("service").GetString().Should().Be("Chatinbox.Backend");
        json.RootElement.TryGetProperty("timestamp", out _).Should().BeTrue();
    }
}
