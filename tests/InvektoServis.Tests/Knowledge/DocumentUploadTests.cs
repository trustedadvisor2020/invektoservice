using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using InvektoServis.Tests._Shared.Factories;
using InvektoServis.Tests._Shared.Infrastructure;
using Invekto.Knowledge.Data;
using NSubstitute;

namespace InvektoServis.Tests.Knowledge;

public class DocumentUploadTests : IClassFixture<KnowledgeTestFactory>
{
    private readonly KnowledgeTestFactory _factory;
    private readonly HttpClient _client;
    private const int TenantId = TestJwtTokenHelper.DefaultTenantId;

    public DocumentUploadTests(KnowledgeTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task ListDocuments_ReturnsOk()
    {
        _factory.FakeKnowledgeRepo.ListDocumentsAsync(
            Arg.Is(TenantId), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns((new List<DocumentDto>(), 0));

        var response = await _client.GetAsync($"/api/v1/knowledge/{TenantId}/documents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Upload_WithoutFormData_Returns400()
    {
        var response = await _client.PostAsync(
            $"/api/v1/knowledge/{TenantId}/documents/upload",
            new StringContent("test"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_NonPdfExtension_Returns400()
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 0x00 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "test.txt");

        var response = await _client.PostAsync(
            $"/api/v1/knowledge/{TenantId}/documents/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_WrongContentType_Returns400()
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 0x00 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "test.pdf");

        var response = await _client.PostAsync(
            $"/api/v1/knowledge/{TenantId}/documents/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_InvalidPdfMagicBytes_Returns400()
    {
        var content = new MultipartFormDataContent();
        // Valid extension and content-type but invalid PDF magic bytes
        var fakeBytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };
        var fileContent = new ByteArrayContent(fakeBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "test.pdf");

        var response = await _client.PostAsync(
            $"/api/v1/knowledge/{TenantId}/documents/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
