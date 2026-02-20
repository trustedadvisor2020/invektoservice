using Invekto.Shared.Logging;

namespace Invekto.Shared.Data;

/// <summary>
/// Abstract base class for tenant-scoped PostgreSQL repositories.
/// Provides PostgresConnectionFactory and JsonLinesLogger to subclasses.
///
/// CRITICAL RULE: tenantId is ALWAYS a method parameter, NEVER stored in a field.
/// Each request is tenant-scoped — storing tenantId would risk cross-tenant data
/// leakage between concurrent requests on the same singleton instance.
///
/// Excluded by design:
/// - KnowledgeRepository (uses KnowledgeConnectionFactory for pgvector)
/// - WhatsAppAnalytics/AnalyticsRepository (uses AnalyticsConnectionFactory)
/// - TenantRegistryRepository (queries tenant_registry itself, not tenant-scoped)
///
/// Register concrete subclasses as singleton in DI (thread-safe via pooled factory).
/// </summary>
public abstract class TenantRepositoryBase
{
    protected readonly PostgresConnectionFactory _db;
    protected readonly JsonLinesLogger _logger;

    protected TenantRepositoryBase(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
