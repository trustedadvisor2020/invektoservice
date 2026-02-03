using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Invekto.ChatAnalysis.Tests.Fixtures;
using Invekto.Shared.DTOs.ChatAnalysis;

namespace Invekto.ChatAnalysis.Tests.IntegrationTests;

public class AnalyzeApiTests : IClassFixture<ChatAnalysisWebApplicationFactory>
{
    private readonly ChatAnalysisWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AnalyzeApiTests(ChatAnalysisWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Analyze_WithoutBody_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsync("/api/v1/analyze", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Analyze_WithMissingRequestID_ReturnsBadRequest()
    {
        // Arrange
        var request = new ChatAnalysisRequest
        {
            ChatID = 1,
            InstanceID = 1,
            UserID = 1,
            RequestID = "",
            ChatServerURL = "http://localhost:8080/callback"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/analyze", request);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INV-301");
    }

    [Fact]
    public async Task Analyze_WithMissingChatServerURL_ReturnsBadRequest()
    {
        // Arrange
        var request = new ChatAnalysisRequest
        {
            ChatID = 1,
            InstanceID = 1,
            UserID = 1,
            RequestID = "test-123",
            ChatServerURL = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/analyze", request);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INV-301");
    }

    [Fact]
    public async Task Analyze_WithValidRequest_ReturnsAccepted()
    {
        // Arrange
        var request = new ChatAnalysisRequest
        {
            ChatID = 123,
            InstanceID = 456,
            UserID = 789,
            RequestID = "test-request-123",
            ChatServerURL = "http://localhost:8080/callback",
            MessageListObject = new List<MessageItem>
            {
                new() { Source = "CUSTOMER", Message = "Merhaba, ürün hakkında bilgi almak istiyorum" },
                new() { Source = "AGENT", Message = "Tabii, size nasıl yardımcı olabilirim?" }
            },
            LabelSearchText = "İlgili,Potansiyel Müşteri,Satış"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/analyze", request);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert - should return 200 OK with Processing status
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("RequestID").GetString().Should().Be("test-request-123");
        json.RootElement.GetProperty("Status").GetString().Should().Be("Processing");
    }

    [Fact]
    public async Task Analyze_WithEmptyMessages_ReturnsAccepted()
    {
        // Arrange - empty messages should still be accepted, error sent via callback
        var request = new ChatAnalysisRequest
        {
            ChatID = 123,
            InstanceID = 456,
            UserID = 789,
            RequestID = "test-empty-messages",
            ChatServerURL = "http://localhost:8080/callback",
            MessageListObject = new List<MessageItem>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/analyze", request);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert - still returns 200 OK, error goes via callback
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("RequestID").GetString().Should().Be("test-empty-messages");
        json.RootElement.GetProperty("Status").GetString().Should().Be("Processing");
    }
}
