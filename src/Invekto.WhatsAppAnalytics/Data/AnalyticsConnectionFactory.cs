using Npgsql;

namespace Invekto.WhatsAppAnalytics.Data;

/// <summary>
/// PostgreSQL connection factory for WhatsApp Analytics service.
/// Follows shared pattern from Invekto.Knowledge.
/// </summary>
public sealed class AnalyticsConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;

    public AnalyticsConnectionFactory(string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = dataSourceBuilder.Build();
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = _dataSource.CreateConnection();
        await connection.OpenAsync(ct);
        return connection;
    }
}
