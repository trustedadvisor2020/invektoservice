using System.Net;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;
using Chatinbox.Tests._Shared.Infrastructure;

namespace Chatinbox.Tests.Backend;

public class AuthTests : IClassFixture<BackendTestFactory>
{
    private readonly BackendTestFactory _factory;

    public AuthTests(BackendTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task JwtProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/settings/instances");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JwtProtectedEndpoint_WithExpiredToken_Returns401()
    {
        var client = _factory.CreateClient();
        var expiredToken = TestJwtTokenHelper.GenerateToken(expiry: TimeSpan.FromSeconds(-60));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync("/api/v1/settings/instances");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JwtProtectedEndpoint_WithValidToken_DoesNotReturn401()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/settings/instances");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OpsEndpoint_WithoutBasicAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/ops");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OpsEndpoint_WithValidBasicAuth_Returns200()
    {
        var client = _factory.CreateOpsClient();

        var response = await client.GetAsync("/ops");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
