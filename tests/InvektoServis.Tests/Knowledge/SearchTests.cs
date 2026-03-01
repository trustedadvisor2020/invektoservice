using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using InvektoServis.Tests._Shared.Infrastructure;
using Invekto.Knowledge.Data;
using NSubstitute;

namespace InvektoServis.Tests.Knowledge;

public class SearchTests : IClassFixture<KnowledgeTestFactory>
{
    private readonly KnowledgeTestFactory _factory;
    private readonly HttpClient _client;
    private const int TenantId = TestJwtTokenHelper.DefaultTenantId;

    public SearchTests(KnowledgeTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Search_ValidQuery_ReturnsResults()
    {
        // RetrievalService calls KeywordSearchAsync + KeywordSearchChunksAsync as fallback
        _factory.FakeKnowledgeRepo.KeywordSearchAsync(
            Arg.Is(TenantId), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SearchResultDto>
            {
                new() { SourceType = "faq", FaqId = 1, Question = "Iade nasil yapilir?",
                         Answer = "Musteri hizmetlerini arayin.", Score = 0.85, Method = "keyword" }
            });

        _factory.FakeKnowledgeRepo.KeywordSearchChunksAsync(
            Arg.Is(TenantId), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<ChunkSearchResultDto>());

        // Also mock semantic paths
        _factory.FakeKnowledgeRepo.SemanticSearchAsync(
            Arg.Is(TenantId), Arg.Any<Pgvector.Vector>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SearchResultDto>());

        _factory.FakeKnowledgeRepo.SemanticSearchChunksAsync(
            Arg.Is(TenantId), Arg.Any<Pgvector.Vector>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<ChunkSearchResultDto>());

        var request = new { query = "iade", topK = 5 };

        var response = await _client.PostAsJsonAsync($"/api/v1/knowledge/{TenantId}/search", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("results");
    }

    [Fact]
    public async Task Search_EmptyQuery_Returns400()
    {
        var request = new { query = "", topK = 5 };

        var response = await _client.PostAsJsonAsync($"/api/v1/knowledge/{TenantId}/search", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_InvalidTopK_Returns400()
    {
        var request = new { query = "test", topK = 100 };

        var response = await _client.PostAsJsonAsync($"/api/v1/knowledge/{TenantId}/search", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_WrongTenant_Returns403()
    {
        var request = new { query = "test", topK = 5 };

        var response = await _client.PostAsJsonAsync("/api/v1/knowledge/9999/search", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
