using Chatinbox.Tests._Shared.Infrastructure;
using Chatinbox.Knowledge.Data;
using Chatinbox.Shared.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Chatinbox.Tests._Shared.Factories;

public class KnowledgeTestFactory : WebApplicationFactory<Chatinbox.Knowledge.Program>
{
    public KnowledgeRepository FakeKnowledgeRepo { get; private set; } = null!;
    public TemplateRepository FakeTemplateRepo { get; private set; } = null!;
    public MockHttpMessageHandler MockOpenAI { get; } = new();

    static KnowledgeTestFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__SecretKey", TestJwtTokenHelper.TestSecretKey);
        Environment.SetEnvironmentVariable("ConnectionStrings__PostgreSQL", "Host=localhost;Port=5432;Database=test;Username=test;Password=test");
        Environment.SetEnvironmentVariable("OpenAI__ApiKey", "test-openai-api-key");
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
                ["OpenAI:ApiKey"] = "test-openai-api-key",
                ["OpenAI:EmbeddingModel"] = "text-embedding-3-large",
                ["OpenAI:EmbeddingDimensions"] = "3072",
                ["OpenAI:TimeoutMs"] = "10000",
                ["OpenAI:MaxRetries"] = "1",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var fakeKnowledgeDb = new KnowledgeConnectionFactory("Host=localhost;Port=5432;Database=test;Username=test;Password=test");
            var fakeLogger = FakeLoggerFactory.Create("Knowledge.Test");

            FakeKnowledgeRepo = Substitute.For<KnowledgeRepository>(fakeKnowledgeDb, fakeLogger);
            FakeTemplateRepo = Substitute.For<TemplateRepository>(fakeKnowledgeDb, fakeLogger);

            services.RemoveAll<KnowledgeRepository>();
            services.RemoveAll<TemplateRepository>();
            services.AddSingleton(FakeKnowledgeRepo);
            services.AddSingleton(FakeTemplateRepo);

            services.RemoveAll<JsonLinesLogger>();
            services.AddSingleton(fakeLogger);

            // Mock OpenAI embeddings API with fake vector
            SetupDefaultOpenAIMock();

            services.AddHttpClient("EmbeddingService")
                .ConfigurePrimaryHttpMessageHandler(() => MockOpenAI);

            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
        });
    }

    private void SetupDefaultOpenAIMock()
    {
        // Return a fake 3072-dim embedding (all zeros for test)
        var embeddingJson = """
        {
            "data": [{"embedding": [0.1, 0.2, 0.3], "index": 0}],
            "model": "text-embedding-3-large",
            "usage": {"prompt_tokens": 10, "total_tokens": 10}
        }
        """;
        MockOpenAI.WhenUrl("api.openai.com", new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(embeddingJson, System.Text.Encoding.UTF8, "application/json")
        });
    }

    public HttpClient CreateAuthenticatedClient(int tenantId = TestJwtTokenHelper.DefaultTenantId)
    {
        var client = CreateClient();
        TestJwtTokenHelper.AddAuthHeader(client, tenantId);
        return client;
    }
}
