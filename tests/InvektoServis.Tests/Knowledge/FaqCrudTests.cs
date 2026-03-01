using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using InvektoServis.Tests._Shared.Infrastructure;
using Invekto.Knowledge.Data;
using NSubstitute;

namespace InvektoServis.Tests.Knowledge;

public class FaqCrudTests : IClassFixture<KnowledgeTestFactory>
{
    private readonly KnowledgeTestFactory _factory;
    private readonly HttpClient _client;
    private const int TenantId = TestJwtTokenHelper.DefaultTenantId;

    public FaqCrudTests(KnowledgeTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task ListFaqs_ReturnsOk()
    {
        _factory.FakeKnowledgeRepo.ListFaqsAsync(
            Arg.Is(TenantId), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new List<FaqDto>
            {
                new() { Id = 1, TenantId = TenantId, Question = "Iade nasil yapilir?",
                         Answer = "Musteri hizmetlerini arayin.", Lang = "tr", Source = "manual",
                         IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            }, 1));

        var response = await _client.GetAsync($"/api/v1/knowledge/{TenantId}/faqs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Iade nasil yapilir?");
    }

    [Fact]
    public async Task ListFaqs_WithLangFilter_Returns()
    {
        _factory.FakeKnowledgeRepo.ListFaqsAsync(
            Arg.Is(TenantId), Arg.Is("en"), Arg.Any<string?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new List<FaqDto>(), 0));

        var response = await _client.GetAsync($"/api/v1/knowledge/{TenantId}/faqs?lang=en");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateFaq_ValidData_Returns201()
    {
        _factory.FakeKnowledgeRepo.InsertFaqAsync(
            Arg.Is(TenantId), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string[]>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new FaqDto
            {
                Id = 42, TenantId = TenantId, Question = "Kargo suresi ne kadar?",
                Answer = "2-3 is gunu.", Lang = "tr", Source = "manual",
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        var request = new
        {
            question = "Kargo suresi ne kadar?",
            answer = "2-3 is gunu.",
            category = "shipping",
            lang = "tr"
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/knowledge/{TenantId}/faqs", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateFaq_MissingQuestion_Returns400()
    {
        var request = new { question = "", answer = "cevap" };

        var response = await _client.PostAsJsonAsync($"/api/v1/knowledge/{TenantId}/faqs", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateFaq_DuplicateQuestion_Returns409()
    {
        _factory.FakeKnowledgeRepo.InsertFaqAsync(
            Arg.Is(TenantId), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string[]>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((FaqDto?)null);

        var request = new { question = "Duplicate?", answer = "Answer." };

        var response = await _client.PostAsJsonAsync($"/api/v1/knowledge/{TenantId}/faqs", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateFaq_Existing_ReturnsOk()
    {
        _factory.FakeKnowledgeRepo.UpdateFaqAsync(
            Arg.Is(TenantId), Arg.Is(1), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string[]?>(),
            Arg.Any<CancellationToken>())
            .Returns(new FaqDto
            {
                Id = 1, TenantId = TenantId, Question = "Guncel soru?",
                Answer = "Guncel cevap.", Lang = "tr", Source = "manual",
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        var request = new { question = "Guncel soru?", answer = "Guncel cevap." };

        var response = await _client.PutAsJsonAsync($"/api/v1/knowledge/{TenantId}/faqs/1", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateFaq_NonExistent_Returns404()
    {
        _factory.FakeKnowledgeRepo.UpdateFaqAsync(
            Arg.Is(TenantId), Arg.Is(999), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string[]?>(),
            Arg.Any<CancellationToken>())
            .Returns((FaqDto?)null);

        var request = new { question = "Not found?" };

        var response = await _client.PutAsJsonAsync($"/api/v1/knowledge/{TenantId}/faqs/999", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteFaq_Existing_ReturnsOk()
    {
        _factory.FakeKnowledgeRepo.DeleteFaqAsync(
            Arg.Is(TenantId), Arg.Is(1), Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync($"/api/v1/knowledge/{TenantId}/faqs/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteFaq_NonExistent_Returns404()
    {
        _factory.FakeKnowledgeRepo.DeleteFaqAsync(
            Arg.Is(TenantId), Arg.Is(999), Arg.Any<CancellationToken>())
            .Returns(false);

        var response = await _client.DeleteAsync($"/api/v1/knowledge/{TenantId}/faqs/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Faqs_WrongTenantId_Returns403()
    {
        // JWT has tenant 1001, but URL has tenant 9999
        var response = await _client.GetAsync("/api/v1/knowledge/9999/faqs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
