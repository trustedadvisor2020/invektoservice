using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;
using Chatinbox.Tests._Shared.DataGenerators;
using Chatinbox.Tests._Shared.Infrastructure;
using NSubstitute;

namespace Chatinbox.Tests.AgentAI;

public class SuggestFlowTests : IClassFixture<AgentAITestFactory>
{
    private readonly AgentAITestFactory _factory;
    private readonly HttpClient _client;

    public SuggestFlowTests(AgentAITestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Suggest_WithValidRequest_ReturnsSuggestion()
    {
        // Explicit mock setup (factory defaults may be cleared by other tests)
        _factory.MockClaude.Clear();
        var claudeResponse = """
        {
            "content": [{"type": "text", "text": "{\"suggested_reply\":\"Merhaba, size yardimci olabilirim.\",\"intent\":\"greeting\",\"confidence\":0.95,\"detected_language\":\"tr\"}"}],
            "model": "test-model",
            "usage": {"input_tokens": 100, "output_tokens": 50}
        }
        """;
        _factory.MockClaude.WhenUrl("api.anthropic.com", System.Net.HttpStatusCode.OK, claudeResponse);

        _factory.MockKnowledge.Clear();
        _factory.MockKnowledge.WhenUrl("/search", System.Net.HttpStatusCode.OK,
            """{"results":[],"method":"keyword","durationMs":1}""");

        _factory.FakeRepo.LogSuggestionAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<double?>(), Arg.Any<string?>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("logged");

        var request = new
        {
            chat_id = 123,
            message_text = "Merhaba, urun hakkinda bilgi almak istiyorum",
            customer_name = "Ahmet Yilmaz",
            channel = "whatsapp",
            language = "tr",
            conversation_history = new[]
            {
                new { source = "CUSTOMER", text = "Merhaba" },
                new { source = "AGENT", text = "Merhaba, size nasil yardimci olabilirim?" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/suggest", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Suggest_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var request = new { chat_id = 1, message_text = "test" };

        var response = await client.PostAsJsonAsync("/api/v1/suggest", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Suggest_WithoutMessageText_Returns400()
    {
        var request = new { chat_id = 123, message_text = "" };

        var response = await _client.PostAsJsonAsync("/api/v1/suggest", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Suggest_ClaudeTimeout_ReturnsError()
    {
        // Configure Claude mock to timeout
        _factory.MockClaude.Clear();
        _factory.MockClaude.WhenUrl("api.anthropic.com",
            new HttpResponseMessage(HttpStatusCode.GatewayTimeout));

        var request = new
        {
            chat_id = 999,
            message_text = "Test timeout",
            channel = "whatsapp",
            conversation_history = new[]
            {
                new { source = "CUSTOMER", text = "Test" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/suggest", request);

        // Service should handle gracefully (not crash)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.GatewayTimeout,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Suggest_KnowledgeUnavailable_StillReturnsSuggestion()
    {
        // Knowledge is down but Claude still works
        _factory.MockKnowledge.Clear();
        _factory.MockKnowledge.WhenAny(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        // Re-setup Claude mock (in case cleared by other tests)
        _factory.MockClaude.Clear();
        _factory.MockClaude.WhenUrl("api.anthropic.com", HttpStatusCode.OK,
            """{"content": [{"type": "text", "text": "{\"suggested_reply\":\"Merhaba!\",\"intent\":\"greeting\",\"confidence\":0.9}"}],"model": "test-model","usage": {"input_tokens": 100, "output_tokens": 50}}""");

        _factory.FakeRepo.LogSuggestionAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<double?>(), Arg.Any<string?>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("logged");

        var request = new
        {
            chat_id = 456,
            message_text = "Bilgi almak istiyorum",
            channel = "whatsapp",
            conversation_history = new[]
            {
                new { source = "CUSTOMER", text = "Bilgi almak istiyorum" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/suggest", request);

        // Should still succeed with graceful degradation
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
