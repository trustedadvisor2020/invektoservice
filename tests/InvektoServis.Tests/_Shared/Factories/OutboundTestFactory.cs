using InvektoServis.Tests._Shared.Infrastructure;
using Invekto.Outbound.Data;
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

public class OutboundTestFactory : WebApplicationFactory<Invekto.Outbound.Program>
{
    public OutboundRepository FakeRepo { get; private set; } = null!;
    public MockHttpMessageHandler MockCallback { get; } = new();

    static OutboundTestFactory()
    {
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
                ["Callback:DefaultCallbackUrl"] = "http://localhost:15000/callback",
                ["Callback:MaxRetries"] = "1",
                ["Callback:BaseDelayMs"] = "100",
                ["Callback:TimeoutMs"] = "5000",
                ["RateLimit:DefaultMessagesPerMinute"] = "100",
                ["RateLimit:SenderIntervalMs"] = "100",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var fakeDb = new PostgresConnectionFactory("Host=localhost;Port=5432;Database=test;Username=test;Password=test");
            var fakeLogger = FakeLoggerFactory.Create("Outbound.Test");

            FakeRepo = Substitute.For<OutboundRepository>(fakeDb, fakeLogger);

            services.RemoveAll<OutboundRepository>();
            services.AddSingleton(FakeRepo);

            services.RemoveAll<JsonLinesLogger>();
            services.AddSingleton(fakeLogger);

            // Mock callback to Backend
            MockCallback.WhenAny(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK));

            services.AddHttpClient("MainAppCallbackClient")
                .ConfigurePrimaryHttpMessageHandler(() => MockCallback);

            // Remove MessageSenderService background worker
            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();

            // Default repo mocks (prevent NRE from null returns on list/collection methods)
            FakeRepo.GetConsentRecordsAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new List<(int Id, string ConsentType, string Channel, bool OptedIn, DateTime? OptedInAt, DateTime? OptedOutAt)>());
        });
    }

    public HttpClient CreateAuthenticatedClient(int tenantId = TestJwtTokenHelper.DefaultTenantId)
    {
        var client = CreateClient();
        TestJwtTokenHelper.AddAuthHeader(client, tenantId);
        return client;
    }
}
