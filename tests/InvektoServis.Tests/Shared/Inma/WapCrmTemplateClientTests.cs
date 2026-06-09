using System.Net;
using System.Text;
using FluentAssertions;
using Invekto.Shared.Contracts.Inma;
using Invekto.Shared.Contracts.Inma.Dtos;
using InvektoServis.Tests._Shared.Infrastructure;

namespace InvektoServis.Tests.Shared.Inma;

/// <summary>
/// FEAT-PROJELER / cxapi — PKT-14 S3. Parse-contract tests for
/// <see cref="WapCrmTemplateClient"/> against the REAL cxapi
/// <c>POST /api/templates</c> wire shape.
///
/// Regression anchor (2026-06-09): cxapi <c>preview</c> is a STRUCTURED OBJECT
/// (header/body/footer/buttons), but the DTO originally declared it <c>string?</c>.
/// That mismatch threw a JsonException → the client returned <c>TransportError</c>
/// → the Backend endpoint returned 502 (tenant 5050 / instance 6570). The body
/// below is a verbatim subset captured from the live response — it MUST parse to
/// <see cref="WapCrmTemplateListOutcome.Success"/>.
/// </summary>
public sealed class WapCrmTemplateClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_responder(request));
    }

    private static WapCrmTemplateClient Build(Func<HttpRequestMessage, HttpResponseMessage> responder, int timeoutMs = 5_000)
    {
        var http = new HttpClient(new StubHandler(responder))
        {
            BaseAddress = new Uri("https://cxapi.wapcrm.net/"),
            Timeout = Timeout.InfiniteTimeSpan // per-attempt timeout is enforced inside the client
        };
        var opts = new WapCrmTemplateOptions { BaseUrl = "https://cxapi.wapcrm.net/", TimeoutMs = timeoutMs };
        return new WapCrmTemplateClient(http, opts, FakeLoggerFactory.Create("WapCrmTemplate.Test"));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    // Verbatim subset of the live cxapi /api/templates response (instance 6570, 2026-06-09):
    // a text-param template with a TEXT header + empty buttons, a media (IMAGE) header template
    // with header.text=null + a QUICK_REPLY button, and a header:null template with a URL button.
    private const string RealBody =
        "{\"data\":[" +
        "{\"templateId\":\"1664033448202974\",\"name\":\"text\",\"language\":\"en\",\"category\":\"MARKETING\",\"paramFormat\":\"named\"," +
        "\"preview\":{\"header\":{\"type\":\"TEXT\",\"text\":\"Test {{hname}}\"},\"body\":\"Hello {{name}}.\",\"footer\":\"Test footer\",\"buttons\":[]}," +
        "\"requiredInputs\":[{\"kind\":\"text\",\"location\":\"BODY\",\"paramKey\":\"name\",\"mediaType\":null,\"note\":null}],\"fixedNote\":null}," +
        "{\"templateId\":\"1295898792661115\",\"name\":\"test_picture\",\"language\":\"en\",\"category\":\"MARKETING\",\"paramFormat\":\"named\"," +
        "\"preview\":{\"header\":{\"type\":\"IMAGE\",\"text\":null},\"body\":\"Hello {{name}}, picture.\",\"footer\":\"Test footer\",\"buttons\":[{\"type\":\"QUICK_REPLY\",\"text\":\"OK\"}]}," +
        "\"requiredInputs\":[{\"kind\":\"media\",\"location\":\"HEADER\",\"paramKey\":null,\"mediaType\":\"image\",\"note\":\"public image URL\"}],\"fixedNote\":null}," +
        "{\"templateId\":\"1534420881400963\",\"name\":\"dinamik_button_url\",\"language\":\"tr\",\"category\":\"MARKETING\",\"paramFormat\":\"named\"," +
        "\"preview\":{\"header\":null,\"body\":\"dinamik button test\",\"footer\":null,\"buttons\":[{\"type\":\"URL\",\"text\":\"View your account\"}]}," +
        "\"requiredInputs\":[],\"fixedNote\":null}" +
        "],\"message\":\"Success\",\"requestID\":\"req-123\",\"status\":true,\"statusCode\":\"\"}";

    [Fact]
    public async Task RealCxapiShape_Parses_To_Success_With_StructuredPreview()
    {
        var client = Build(_ => Json(HttpStatusCode.OK, RealBody));

        var result = await client.ListTemplatesAsync("secret-1", 6570, 5050);

        // Regression: an OBJECT preview must NOT collapse to TransportError/502.
        result.Outcome.Should().Be(WapCrmTemplateListOutcome.Success);
        result.Templates.Should().HaveCount(3);

        // Text template: TEXT header + body/footer + empty buttons (full structural assert, no null-forgiving).
        var text = result.Templates[0];
        text.TemplateId.Should().Be("1664033448202974");
        text.Name.Should().Be("text");
        text.Language.Should().Be("en");
        text.Category.Should().Be("MARKETING");
        text.ParamFormat.Should().Be("named");
        text.Preview.Should().BeEquivalentTo(new WapCrmTemplatePreviewDto
        {
            Header = new WapCrmTemplatePreviewHeaderDto { Type = "TEXT", Text = "Test {{hname}}" },
            Body = "Hello {{name}}.",
            Footer = "Test footer",
            Buttons = new List<WapCrmTemplateButtonDto>()
        });
        text.RequiredInputs.Should().ContainSingle().Which.ParamKey.Should().Be("name");

        // Media header → header.text null; a QUICK_REPLY button present.
        var media = result.Templates[1];
        media.Preview.Should().BeEquivalentTo(new WapCrmTemplatePreviewDto
        {
            Header = new WapCrmTemplatePreviewHeaderDto { Type = "IMAGE", Text = null },
            Body = "Hello {{name}}, picture.",
            Footer = "Test footer",
            Buttons = new List<WapCrmTemplateButtonDto> { new() { Type = "QUICK_REPLY", Text = "OK" } }
        });

        // header:null must be tolerated (no throw) + URL button.
        var nullHeader = result.Templates[2];
        nullHeader.Preview.Should().BeEquivalentTo(new WapCrmTemplatePreviewDto
        {
            Header = null,
            Body = "dinamik button test",
            Footer = null,
            Buttons = new List<WapCrmTemplateButtonDto> { new() { Type = "URL", Text = "View your account" } }
        });
    }

    [Fact]
    public async Task EmptyData_Is_Success_With_EmptyList()
    {
        var client = Build(_ => Json(HttpStatusCode.OK,
            "{\"data\":[],\"message\":\"Success\",\"requestID\":\"r\",\"status\":true,\"statusCode\":\"\"}"));

        var result = await client.ListTemplatesAsync("secret-1", 6570, 5050);

        result.Outcome.Should().Be(WapCrmTemplateListOutcome.Success);
        result.Templates.Should().BeEmpty();
    }

    [Fact]
    public async Task EnvelopeStatusFalse_Is_ProviderRejected()
    {
        var client = Build(_ => Json(HttpStatusCode.OK,
            "{\"data\":null,\"message\":\"Invalid api key\",\"requestID\":\"r\",\"status\":false,\"statusCode\":\"400\"}"));

        var result = await client.ListTemplatesAsync("secret-1", 6570, 5050);

        result.Outcome.Should().Be(WapCrmTemplateListOutcome.ProviderRejected);
        result.ProviderStatusCode.Should().Be("400");
        result.Templates.Should().BeEmpty();
    }

    [Fact]
    public async Task NonJsonBody_Is_TransportError()
    {
        var client = Build(_ => Json(HttpStatusCode.OK, "<html>maintenance</html>"));

        var result = await client.ListTemplatesAsync("secret-1", 6570, 5050);

        result.Outcome.Should().Be(WapCrmTemplateListOutcome.TransportError);
    }
}
