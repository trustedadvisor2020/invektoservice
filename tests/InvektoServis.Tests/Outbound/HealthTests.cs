using System.Net;
using System.Text.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;

namespace InvektoServis.Tests.Outbound;

public class HealthTests : IClassFixture<OutboundTestFactory>
{
    private readonly HttpClient _client;

    public HealthTests(OutboundTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
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
}
