using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;
using Chatinbox.Tests._Shared.DataGenerators;
using Chatinbox.Tests._Shared.Infrastructure;
using NSubstitute;

namespace Chatinbox.Tests.E2E;

/// <summary>
/// E2E: Agent suggest flow — JWT auth → Knowledge search → Claude generation → DB log → response
/// </summary>
public class AgentSuggestWithKnowledgeE2ETests : IClassFixture<AgentAITestFactory>
{
    private readonly AgentAITestFactory _factory;
    private readonly HttpClient _client;

    public AgentSuggestWithKnowledgeE2ETests(AgentAITestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task FullSuggestFlow_WithKnowledge_ReturnsEnrichedSuggestion()
    {
        // Setup: Knowledge returns FAQ results
        _factory.MockKnowledge.Clear();
        var knowledgeResponse = """
        {
            "results": [
                {"sourceType": "faq", "faqId": 10, "question": "Kargo suresi ne kadar?", "answer": "2-3 is gunu.", "score": 0.92, "method": "keyword"},
                {"sourceType": "faq", "faqId": 11, "question": "Kargo ucreti ne kadar?", "answer": "100 TL ustu ucretsiz.", "score": 0.78, "method": "keyword"}
            ],
            "method": "keyword",
            "durationMs": 15
        }
        """;
        _factory.MockKnowledge.WhenUrl("/search", HttpStatusCode.OK, knowledgeResponse);

        // Setup: Claude returns suggestion enriched with knowledge
        _factory.MockClaude.Clear();
        _factory.MockClaude.WhenUrl("api.anthropic.com", HttpStatusCode.OK,
            """{"content": [{"type": "text", "text": "{\"suggested_reply\":\"Kargo suresi 2-3 is gunudur ve 100 TL ustu siparislerde ucretsizdir.\",\"intent\":\"shipping_info\",\"confidence\":0.95,\"detected_language\":\"tr\"}"}],"model": "test-model","usage": {"input_tokens": 200, "output_tokens": 80}}""");

        // Setup: DB log succeeds
        _factory.FakeRepo.LogSuggestionAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<double?>(), Arg.Any<string?>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("logged");

        // Act: Authenticated suggest request
        var client = _client;
        var request = new
        {
            chat_id = 500,
            message_text = "Kargo ne zaman gelir?",
            customer_name = "Mehmet Demir",
            channel = "whatsapp",
            language = "tr",
            conversation_history = new[]
            {
                new { source = "CUSTOMER", text = "Merhaba, siparis verdim" },
                new { source = "AGENT", text = "Merhaba, yardimci olayim" },
                new { source = "CUSTOMER", text = "Kargo ne zaman gelir?" }
            }
        };

        var response = await client.PostAsJsonAsync("/api/v1/suggest", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("suggested_reply");
        content.Should().Contain("shipping_info");
    }

    [Fact]
    public async Task FullSuggestFlow_KnowledgeDown_GracefulDegradation()
    {
        // Knowledge is down
        _factory.MockKnowledge.Clear();
        _factory.MockKnowledge.WhenAny(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        // Claude still works
        _factory.MockClaude.Clear();
        _factory.MockClaude.WhenUrl("api.anthropic.com", HttpStatusCode.OK,
            """{"content": [{"type": "text", "text": "{\"suggested_reply\":\"Size yardimci olabilirim.\",\"intent\":\"general\",\"confidence\":0.8}"}],"model": "test-model","usage": {"input_tokens": 100, "output_tokens": 40}}""");

        _factory.FakeRepo.LogSuggestionAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<double?>(), Arg.Any<string?>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("logged");

        var client = _client;
        var request = new
        {
            chat_id = 501,
            message_text = "Soru sormak istiyorum",
            channel = "whatsapp",
            conversation_history = new[]
            {
                new { source = "CUSTOMER", text = "Soru sormak istiyorum" }
            }
        };

        var response = await client.PostAsJsonAsync("/api/v1/suggest", request);

        // Should still succeed — Knowledge unavailability is non-blocking
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FullSuggestFlow_ThenFeedback_CompleteCycle()
    {
        // Setup suggest
        _factory.MockClaude.Clear();
        _factory.MockClaude.WhenUrl("api.anthropic.com", HttpStatusCode.OK,
            """{"content": [{"type": "text", "text": "{\"suggested_reply\":\"Test reply\",\"intent\":\"test\",\"confidence\":0.9}"}],"model": "test-model","usage": {"input_tokens": 50, "output_tokens": 20}}""");

        _factory.MockKnowledge.Clear();
        _factory.MockKnowledge.WhenUrl("/search", HttpStatusCode.OK,
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

        var client = _client;

        // Step 1: Get suggestion
        var suggestRequest = new
        {
            chat_id = 502,
            message_text = "Test",
            channel = "whatsapp",
            conversation_history = new[] { new { source = "CUSTOMER", text = "Test" } }
        };
        var suggestResponse = await client.PostAsJsonAsync("/api/v1/suggest", suggestRequest);
        suggestResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var suggestContent = await suggestResponse.Content.ReadAsStringAsync();
        suggestContent.Should().Contain("suggestion_id");

        // Step 2: Send feedback for the suggestion
        _factory.FakeRepo.UpdateFeedbackAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Is("accepted"),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Extract suggestion_id from response (parse JSON)
        var json = System.Text.Json.JsonDocument.Parse(suggestContent);
        var suggestionId = json.RootElement.GetProperty("suggestion_id").GetString();

        var feedbackRequest = new
        {
            suggestion_id = suggestionId,
            agent_action = "accepted",
            final_reply_text = "Test reply used as-is"
        };

        var feedbackResponse = await client.PostAsJsonAsync("/api/v1/feedback", feedbackRequest);
        feedbackResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
