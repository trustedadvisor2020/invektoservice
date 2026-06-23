using System.Net;
using System.Text.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;

namespace Chatinbox.Tests.Knowledge;

public class HealthTests : IClassFixture<KnowledgeTestFactory>
{
    private readonly HttpClient _client;

    public HealthTests(KnowledgeTestFactory factory)
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
