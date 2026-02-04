using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Invekto.ChatAnalysis.Tests.Fixtures;

public class ChatAnalysisWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override config with test values
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WapCrm:SecretKey"] = "test-secret-key",
                ["Claude:ApiKey"] = "test-api-key",
                ["Service:ListenPort"] = "0" // Random available port
            });
        });
    }
}
