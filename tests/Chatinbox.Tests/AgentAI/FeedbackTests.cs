using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;
using NSubstitute;

namespace Chatinbox.Tests.AgentAI;

public class FeedbackTests : IClassFixture<AgentAITestFactory>
{
    private readonly AgentAITestFactory _factory;
    private readonly HttpClient _client;

    public FeedbackTests(AgentAITestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Feedback_ValidPayload_Returns202()
    {
        var suggestionId = Guid.NewGuid();
        _factory.FakeRepo.UpdateFeedbackAsync(
            Arg.Is(suggestionId), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var request = new
        {
            suggestion_id = suggestionId.ToString(),
            agent_action = "accepted",
            final_reply_text = "Merhaba, size yardimci olabilirim."
        };

        var response = await _client.PostAsJsonAsync("/api/v1/feedback", request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Feedback_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var request = new { suggestion_id = Guid.NewGuid().ToString(), agent_action = "accepted" };

        var response = await client.PostAsJsonAsync("/api/v1/feedback", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Feedback_MissingAgentAction_Returns400()
    {
        var request = new { suggestion_id = Guid.NewGuid().ToString(), agent_action = "" };

        var response = await _client.PostAsJsonAsync("/api/v1/feedback", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Feedback_InvalidSuggestionId_Returns400()
    {
        var request = new { suggestion_id = "not-a-guid", agent_action = "accepted" };

        var response = await _client.PostAsJsonAsync("/api/v1/feedback", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Feedback_UnknownSuggestionId_Returns404()
    {
        var unknownId = Guid.NewGuid();
        _factory.FakeRepo.UpdateFeedbackAsync(
            Arg.Is(unknownId), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var request = new
        {
            suggestion_id = unknownId.ToString(),
            agent_action = "rejected"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/feedback", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
