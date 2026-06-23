using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Chatinbox.Tests._Shared.Factories;
using Chatinbox.Tests._Shared.DataGenerators;
using Chatinbox.Shared.DTOs.Outbound;
using NSubstitute;

namespace Chatinbox.Tests.Outbound;

public class ConsentTests : IClassFixture<OutboundTestFactory>
{
    private readonly OutboundTestFactory _factory;
    private readonly HttpClient _client;

    public ConsentTests(OutboundTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task UpsertConsent_ValidData_Returns200()
    {
        var (phone, _, _) = TurkishCustomerGenerator.GenerateCustomer();

        _factory.FakeRepo.UpsertConsentAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _factory.FakeRepo.InsertAuditTrailAsync(
            Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new
        {
            customer_phone = phone,
            consent_type = "marketing",
            channel = "whatsapp",
            opted_in = true
        };

        var response = await _client.PostAsJsonAsync("/api/v1/consent", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpsertConsent_MissingPhone_Returns400()
    {
        var request = new
        {
            customer_phone = "",
            consent_type = "marketing",
            channel = "whatsapp"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/consent", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpsertConsent_InvalidConsentType_Returns400()
    {
        var request = new
        {
            customer_phone = "+905551234567",
            consent_type = "invalid_type",
            channel = "whatsapp"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/consent", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpsertConsent_MissingChannel_Returns400()
    {
        var request = new
        {
            customer_phone = "+905551234567",
            consent_type = "marketing",
            channel = ""
        };

        var response = await _client.PostAsJsonAsync("/api/v1/consent", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CheckConsent_ReturnsOk()
    {
        _factory.FakeRepo.GetConsentRecordsAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int Id, string ConsentType, string Channel, bool OptedIn, DateTime? OptedInAt, DateTime? OptedOutAt)>
            {
                (1, "marketing", "whatsapp", true, DateTime.UtcNow.AddDays(-30), null)
            });

        _factory.FakeRepo.HasMarketingConsentAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.GetAsync("/api/v1/consent/check/+905551234567");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("has_marketing_consent");
    }

    [Fact]
    public async Task Consent_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/consent/check/+905551234567");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
