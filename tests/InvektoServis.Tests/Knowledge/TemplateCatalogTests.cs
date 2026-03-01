using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using InvektoServis.Tests._Shared.Infrastructure;
using Invekto.Shared.DTOs.Templates;
using NSubstitute;

namespace InvektoServis.Tests.Knowledge;

public class TemplateCatalogTests : IClassFixture<KnowledgeTestFactory>
{
    private readonly KnowledgeTestFactory _factory;

    public TemplateCatalogTests(KnowledgeTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListCatalog_WithoutSuperadmin_Returns403()
    {
        // Regular tenant user (1001) cannot access superadmin-only endpoints
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/templates/catalog");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListCatalog_WithSuperadmin_ReturnsOk()
    {
        // Superadmin has tenantId=0
        var client = _factory.CreateClient();
        TestJwtTokenHelper.AddAuthHeader(client, tenantId: 0);

        _factory.FakeTemplateRepo.ListAsync(
            Arg.Any<TemplateCatalogFilter>(), Arg.Any<CancellationToken>())
            .Returns((new List<TemplateCatalogDto>(), 0));

        var response = await client.GetAsync("/api/v1/templates/catalog");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Catalog_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/templates/catalog");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
