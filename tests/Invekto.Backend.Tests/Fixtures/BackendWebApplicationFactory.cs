using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Invekto.Backend.Tests.Fixtures;

public class BackendWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Override any services for testing here if needed
            // e.g., mock external dependencies
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        // Add Basic Auth header (admin:admin123 - Stage-0 default)
        var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin:admin123"));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        return client;
    }
}
