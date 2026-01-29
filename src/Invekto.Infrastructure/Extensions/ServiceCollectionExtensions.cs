using Microsoft.Extensions.DependencyInjection;

namespace Invekto.Infrastructure.Extensions;

/// <summary>
/// Extension methods for IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Invekto infrastructure services to the service collection.
    /// </summary>
    public static IServiceCollection AddInvektoInfrastructure(this IServiceCollection services)
    {
        // Phase 2'de eklenecek: Polly policies, health checks
        // Phase 3'te eklenecek: RabbitMQ, Redis services

        return services;
    }
}
