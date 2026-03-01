using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using InvektoServis.Tests._Shared.DataGenerators;
using NSubstitute;

namespace InvektoServis.Tests.Outbound;

public class DataDeletionTests : IClassFixture<OutboundTestFactory>
{
    private readonly OutboundTestFactory _factory;
    private readonly HttpClient _client;

    public DataDeletionTests(OutboundTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task DataDeletion_ValidRequest_ReturnsOk()
    {
        var (phone, _, _) = TurkishCustomerGenerator.GenerateCustomer();

        _factory.FakeRepo.CreateDeletionRequestAsync(
            Arg.Any<int>(), Arg.Is(phone), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(1);

        _factory.FakeRepo.ExecuteDataDeletionAsync(
            Arg.Any<int>(), Arg.Is(phone), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "messages", "opt_outs", "consent_records" });

        _factory.FakeRepo.UpdateDeletionRequestAsync(
            Arg.Any<int>(), Arg.Is(1), Arg.Is("completed"),
            Arg.Any<List<string>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new
        {
            customer_phone = phone,
            requested_by = "customer_request"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/data-deletion", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("completed");
    }

    [Fact]
    public async Task DataDeletion_MissingPhone_Returns400()
    {
        var request = new { customer_phone = "" };

        var response = await _client.PostAsJsonAsync("/api/v1/data-deletion", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DataDeletion_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var request = new { customer_phone = "+905551234567" };

        var response = await client.PostAsJsonAsync("/api/v1/data-deletion", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DataDeletion_ResponseContainsServicesList()
    {
        _factory.FakeRepo.CreateDeletionRequestAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(2);

        _factory.FakeRepo.ExecuteDataDeletionAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "messages", "opt_outs" });

        _factory.FakeRepo.UpdateDeletionRequestAsync(
            Arg.Any<int>(), Arg.Is(2), Arg.Any<string>(),
            Arg.Any<List<string>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new { customer_phone = "+905559876543" };

        var response = await _client.PostAsJsonAsync("/api/v1/data-deletion", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("services_cleaned");
    }
}
