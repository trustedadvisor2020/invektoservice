using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;

namespace InvektoServis.Tests.AgentAI;

public class EscalationNoteTests : IClassFixture<AgentAITestFactory>
{
    private readonly AgentAITestFactory _factory;
    private readonly HttpClient _client;

    public EscalationNoteTests(AgentAITestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task EscalationNote_ValidPayload_ReturnsNote()
    {
        var payload = new
        {
            intent = "complaint",
            sentiment = "negative",
            messages = new[]
            {
                new { role = "customer", text = "Siparisim 3 gundur gelmedi!" },
                new { role = "agent", text = "Kontrol ediyorum." }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/ecom/escalation-note", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("summary");
    }

    [Fact]
    public async Task EscalationNote_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var payload = new { intent = "complaint", sentiment = "negative" };

        var response = await client.PostAsJsonAsync("/api/v1/ecom/escalation-note", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EscalationNote_InvalidJson_Returns400()
    {
        var content = new StringContent("not json", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/ecom/escalation-note", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
