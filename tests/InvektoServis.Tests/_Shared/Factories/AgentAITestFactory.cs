using InvektoServis.Tests._Shared.Infrastructure;
using Invekto.AgentAI.Data;
using Invekto.Shared.Data;
using Invekto.Shared.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace InvektoServis.Tests._Shared.Factories;

public class AgentAITestFactory : WebApplicationFactory<Invekto.AgentAI.Program>
{
    public AgentAIRepository FakeRepo { get; private set; } = null!;
    public MockHttpMessageHandler MockClaude { get; } = new();
    public MockHttpMessageHandler MockKnowledge { get; } = new();
    public MockHttpMessageHandler MockIntegrations { get; } = new();

    static AgentAITestFactory()
    {
        // Env vars must be set before Program.cs validation runs (before Build())
        Environment.SetEnvironmentVariable("Claude__ApiKey", "test-claude-api-key");
        Environment.SetEnvironmentVariable("Jwt__SecretKey", TestJwtTokenHelper.TestSecretKey);
        Environment.SetEnvironmentVariable("ConnectionStrings__PostgreSQL", "Host=localhost;Port=5432;Database=test;Username=test;Password=test");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Service:ListenPort"] = "0",
                ["Jwt:SecretKey"] = TestJwtTokenHelper.TestSecretKey,
                ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Port=5432;Database=test;Username=test;Password=test",
                ["Claude:ApiKey"] = "test-claude-api-key",
                ["Claude:Model"] = "test-model",
                ["Claude:TimeoutSeconds"] = "30",
                ["Claude:MaxHistoryMessages"] = "20",
                ["Knowledge:BaseUrl"] = "http://localhost:17104",
                ["Knowledge:TimeoutMs"] = "5000",
                ["Knowledge:TopK"] = "5",
                ["Integrations:BaseUrl"] = "http://localhost:17106",
                ["Integrations:TimeoutMs"] = "3000",
                ["Summarizer:SummaryThreshold"] = "15",
                ["Summarizer:RecentMessageCount"] = "5",
                ["AgentProfile:MaxFeedbackHistory"] = "20",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var fakeDb = new PostgresConnectionFactory("Host=localhost;Port=5432;Database=test;Username=test;Password=test");
            var fakeLogger = FakeLoggerFactory.Create("AgentAI.Test");

            FakeRepo = Substitute.For<AgentAIRepository>(fakeDb, fakeLogger);

            services.RemoveAll<AgentAIRepository>();
            services.AddSingleton(FakeRepo);

            services.RemoveAll<JsonLinesLogger>();
            services.AddSingleton(fakeLogger);

            // Setup default Claude mock response
            SetupDefaultClaudeMock();
            SetupDefaultKnowledgeMock();

            // Replace typed HttpClient handlers
            services.AddHttpClient("ReplyGenerator")
                .ConfigurePrimaryHttpMessageHandler(() => MockClaude);
            services.AddHttpClient("ConversationSummarizer")
                .ConfigurePrimaryHttpMessageHandler(() => MockClaude);
            services.AddHttpClient("KnowledgeHttpClient")
                .ConfigurePrimaryHttpMessageHandler(() => MockKnowledge);
            services.AddHttpClient("OrderCardService")
                .ConfigurePrimaryHttpMessageHandler(() => MockIntegrations);

            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();

            // Default repo mocks (prevent NRE from null returns)
            FakeRepo.GetRecentFeedbackAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new List<Invekto.AgentAI.Data.FeedbackRecord>());
        });
    }

    private void SetupDefaultClaudeMock()
    {
        var claudeResponse = """
        {
            "content": [{"type": "text", "text": "{\"suggested_reply\":\"Merhaba, size yardimci olabilirim.\",\"intent\":\"greeting\",\"confidence\":0.95,\"suggested_followup\":null,\"detected_language\":\"tr\"}"}],
            "model": "test-model",
            "usage": {"input_tokens": 100, "output_tokens": 50}
        }
        """;
        MockClaude.WhenUrl("api.anthropic.com", new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(claudeResponse, System.Text.Encoding.UTF8, "application/json")
        });
    }

    private void SetupDefaultKnowledgeMock()
    {
        var knowledgeResponse = """
        {
            "results": [
                {"sourceType": "faq", "faqId": 1, "question": "Iade nasil yapilir?", "answer": "Iade icin musteri hizmetlerini arayabilirsiniz.", "score": 0.85, "method": "keyword"}
            ],
            "method": "keyword",
            "durationMs": 12
        }
        """;
        MockKnowledge.WhenUrl("/search", new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(knowledgeResponse, System.Text.Encoding.UTF8, "application/json")
        });
    }

    public HttpClient CreateAuthenticatedClient(int tenantId = TestJwtTokenHelper.DefaultTenantId)
    {
        var client = CreateClient();
        TestJwtTokenHelper.AddAuthHeader(client, tenantId);
        return client;
    }
}
