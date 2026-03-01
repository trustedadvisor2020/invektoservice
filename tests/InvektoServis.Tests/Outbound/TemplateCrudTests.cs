using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using Invekto.Shared.DTOs.Outbound;
using NSubstitute;

namespace InvektoServis.Tests.Outbound;

public class TemplateCrudTests : IClassFixture<OutboundTestFactory>
{
    private readonly OutboundTestFactory _factory;
    private readonly HttpClient _client;

    public TemplateCrudTests(OutboundTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task CreateTemplate_ValidData_ReturnsCreated()
    {
        _factory.FakeRepo.CreateTemplateAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var request = new
        {
            name = "Welcome Template",
            trigger_event = "new_lead",
            message_template = "Merhaba {{customer_name}}, hosgeldiniz!",
            lang = "tr"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/templates", request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
    }

    [Fact]
    public async Task ListTemplates_ReturnsActiveTemplates()
    {
        _factory.FakeRepo.GetActiveTemplatesAsync(
            Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<TemplateDto>
            {
                new() { Id = 1, Name = "Welcome", TriggerEvent = "new_lead",
                         MessageTemplate = "Merhaba!", IsActive = true, Lang = "tr" }
            });

        var response = await _client.GetAsync("/api/v1/templates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Welcome");
    }

    [Fact]
    public async Task ListTemplates_WithLangFilter_Filters()
    {
        _factory.FakeRepo.GetActiveTemplatesAsync(
            Arg.Any<int>(), Arg.Is("en"), Arg.Any<CancellationToken>())
            .Returns(new List<TemplateDto>());

        var response = await _client.GetAsync("/api/v1/templates?lang=en");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteTemplate_Existing_Deactivates()
    {
        _factory.FakeRepo.DeactivateTemplateAsync(
            Arg.Any<int>(), Arg.Is(1), Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync("/api/v1/templates/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteTemplate_NonExistent_Returns404()
    {
        _factory.FakeRepo.DeactivateTemplateAsync(
            Arg.Any<int>(), Arg.Is(999), Arg.Any<CancellationToken>())
            .Returns(false);

        var response = await _client.DeleteAsync("/api/v1/templates/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Templates_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/templates");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
