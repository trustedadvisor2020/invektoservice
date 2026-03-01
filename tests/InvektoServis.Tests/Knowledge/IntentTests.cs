using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using InvektoServis.Tests._Shared.Infrastructure;
using Invekto.Knowledge.Data;
using NSubstitute;

namespace InvektoServis.Tests.Knowledge;

public class IntentTests : IClassFixture<KnowledgeTestFactory>
{
    private readonly KnowledgeTestFactory _factory;
    private readonly HttpClient _client;
    private const int TenantId = TestJwtTokenHelper.DefaultTenantId;

    public IntentTests(KnowledgeTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task ListIntents_ReturnsOk()
    {
        _factory.FakeKnowledgeRepo.GetTenantIntentNamesAsync(
            Arg.Is(TenantId), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "greeting", "complaint", "order_status" });

        var response = await _client.GetAsync($"/api/v1/knowledge/{TenantId}/intents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("greeting");
    }

    [Fact]
    public async Task CreateIntent_ValidData_ReturnsOk()
    {
        _factory.FakeKnowledgeRepo.InsertIntentAsync(
            Arg.Is(TenantId), Arg.Any<string>(), Arg.Any<string[]>(),
            Arg.Any<CancellationToken>())
            .Returns(42);

        var request = new { intent_name = "new_intent", keywords = new[] { "test", "deneme" } };

        var response = await _client.PostAsJsonAsync($"/api/v1/knowledge/{TenantId}/intents", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateIntent_Duplicate_Returns409()
    {
        _factory.FakeKnowledgeRepo.InsertIntentAsync(
            Arg.Is(TenantId), Arg.Any<string>(), Arg.Any<string[]>(),
            Arg.Any<CancellationToken>())
            .Returns(0);

        var request = new { intent_name = "existing_intent" };

        var response = await _client.PostAsJsonAsync($"/api/v1/knowledge/{TenantId}/intents", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteIntent_Existing_ReturnsOk()
    {
        _factory.FakeKnowledgeRepo.DeleteIntentAsync(
            Arg.Is(1), Arg.Is(TenantId), Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync($"/api/v1/knowledge/{TenantId}/intents/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteIntent_NonExistent_Returns404()
    {
        _factory.FakeKnowledgeRepo.DeleteIntentAsync(
            Arg.Is(999), Arg.Is(TenantId), Arg.Any<CancellationToken>())
            .Returns(false);

        var response = await _client.DeleteAsync($"/api/v1/knowledge/{TenantId}/intents/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
