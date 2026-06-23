using Chatinbox.Tests._Shared.Infrastructure;
using Chatinbox.Backend.Data;
using Chatinbox.Backend.Services;
using Chatinbox.Shared.Data;
using Chatinbox.Shared.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Chatinbox.Tests._Shared.Factories;

public class BackendTestFactory : WebApplicationFactory<Chatinbox.Backend.Program>
{
    static BackendTestFactory()
    {
        // Env vars must be set before Program.cs conditional registrations run (before Build())
        Environment.SetEnvironmentVariable("Jwt__SecretKey", TestJwtTokenHelper.TestSecretKey);
        Environment.SetEnvironmentVariable("ConnectionStrings__PostgreSQL", "Host=localhost;Port=5432;Database=test;Username=test;Password=test");
        Environment.SetEnvironmentVariable("InmaAuth__SecretKey", "test-inma-secret-key-for-testing-only-32bytes!!");
    }

    public LeadRepository FakeLeadRepo { get; private set; } = null!;
    public InstanceRepository FakeInstanceRepo { get; private set; } = null!;
    public AnalyticsRepository FakeAnalyticsRepo { get; private set; } = null!;
    public AttributionRepository FakeAttributionRepo { get; private set; } = null!;
    public TenantRegistryRepository FakeTenantRegistryRepo { get; private set; } = null!;
    public MessageLogRepository FakeMessageLogRepo { get; private set; } = null!;

    public MockHttpMessageHandler MockChatAnalysis { get; } = new();
    public MockHttpMessageHandler MockAgentAI { get; } = new();
    public MockHttpMessageHandler MockKnowledge { get; } = new();
    public MockHttpMessageHandler MockOutbound { get; } = new();
    public MockHttpMessageHandler MockAppointments { get; } = new();
    public MockHttpMessageHandler MockAutomation { get; } = new();
    public MockHttpMessageHandler MockIntegrations { get; } = new();
    public MockHttpMessageHandler MockMarketing { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Service:ListenPort"] = "0",
                ["Jwt:SecretKey"] = TestJwtTokenHelper.TestSecretKey,
                ["Ops:Username"] = "admin",
                ["Ops:Password"] = "admin123",
                ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Port=5432;Database=test;Username=test;Password=test",
                ["Microservice:ChatAnalysis:Url"] = "http://localhost:17101",
                ["Microservice:AgentAI:Url"] = "http://localhost:17105",
                ["Microservice:Knowledge:Url"] = "http://localhost:17104",
                ["Microservice:Outbound:Url"] = "http://localhost:17107",
                ["Microservice:Appointments:Url"] = "http://localhost:17102",
                ["Microservice:Automation:Url"] = "http://localhost:17108",
                ["Microservice:Integrations:Url"] = "http://localhost:17106",
                ["Microservice:Marketing:Url"] = "http://localhost:17112",
                ["Microservice:InternalApiKey"] = "test-internal-key",
                ["InmaAuth:SecretKey"] = "test-inma-secret-key-for-testing-only-32bytes!!",
                ["InmaAuth:LoginUrl"] = "http://localhost:19000/login",
                ["InmaAuth:ApiBaseUrl"] = "http://localhost:19000",
                ["Claude:ApiKey"] = "test-claude-api-key",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Create fake DB factory (constructor succeeds, OpenConnectionAsync would fail — but we mock repos)
            var fakeDb = new PostgresConnectionFactory("Host=localhost;Port=5432;Database=test;Username=test;Password=test");
            var fakeLogger = FakeLoggerFactory.Create("Backend.Test");

            // Create NSubstitute proxies for repositories
            FakeLeadRepo = Substitute.For<LeadRepository>(fakeDb, fakeLogger);
            FakeInstanceRepo = Substitute.For<InstanceRepository>(fakeDb, fakeLogger);
            FakeAnalyticsRepo = Substitute.For<AnalyticsRepository>(fakeDb, fakeLogger);
            FakeAttributionRepo = Substitute.For<AttributionRepository>(fakeDb, fakeLogger);
            FakeTenantRegistryRepo = Substitute.For<TenantRegistryRepository>(fakeDb, fakeLogger);
            FakeMessageLogRepo = Substitute.For<MessageLogRepository>(fakeDb, fakeLogger);

            // Replace repository registrations
            services.RemoveAll<LeadRepository>();
            services.RemoveAll<InstanceRepository>();
            services.RemoveAll<AnalyticsRepository>();
            services.RemoveAll<AttributionRepository>();
            services.RemoveAll<TenantRegistryRepository>();
            services.RemoveAll<MessageLogRepository>();

            services.AddSingleton(FakeLeadRepo);
            services.AddSingleton(FakeInstanceRepo);
            services.AddSingleton(FakeAnalyticsRepo);
            services.AddSingleton(FakeAttributionRepo);
            services.AddSingleton(FakeTenantRegistryRepo);
            services.AddSingleton(FakeMessageLogRepo);

            // Replace logger
            services.RemoveAll<JsonLinesLogger>();
            services.AddSingleton(fakeLogger);

            // Replace HTTP clients with mock handlers
            services.AddHttpClient("ChatAnalysisClient")
                .ConfigurePrimaryHttpMessageHandler(() => MockChatAnalysis);
            services.AddHttpClient("AgentAIClient")
                .ConfigurePrimaryHttpMessageHandler(() => MockAgentAI);
            services.AddHttpClient("OutboundClient")
                .ConfigurePrimaryHttpMessageHandler(() => MockOutbound);
            services.AddHttpClient("KnowledgeClient")
                .ConfigurePrimaryHttpMessageHandler(() => MockKnowledge);
            services.AddHttpClient("AppointmentsClient")
                .ConfigurePrimaryHttpMessageHandler(() => MockAppointments);
            services.AddHttpClient("AutomationClient")
                .ConfigurePrimaryHttpMessageHandler(() => MockAutomation);
            services.AddHttpClient("IntegrationsClient")
                .ConfigurePrimaryHttpMessageHandler(() => MockIntegrations);
            services.AddHttpClient("MarketingClient")
                .ConfigurePrimaryHttpMessageHandler(() => MockMarketing);

            // Remove hosted services to prevent background tasks
            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
        });
    }

    public HttpClient CreateAuthenticatedClient(int tenantId = TestJwtTokenHelper.DefaultTenantId)
    {
        var client = CreateClient();
        TestJwtTokenHelper.AddAuthHeader(client, tenantId);
        return client;
    }

    public HttpClient CreateOpsClient()
    {
        var client = CreateClient();
        TestJwtTokenHelper.AddOpsAuthHeader(client);
        return client;
    }
}
