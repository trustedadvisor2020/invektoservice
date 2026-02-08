using Npgsql;

namespace Invekto.Shared.Data;

/// <summary>
/// Thread-safe PostgreSQL connection factory with built-in pooling.
/// GR-1.9: Foundation for all new services' DB access.
/// Npgsql handles connection pooling internally via the connection string.
/// Register as singleton in DI.
/// </summary>
public sealed class PostgresConnectionFactory
{
    private readonly string _connectionString;
    private readonly NpgsqlDataSource _dataSource;

    public PostgresConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("PostgreSQL connection string is required.");

        _connectionString = connectionString;

        // NpgsqlDataSource manages connection pooling, is thread-safe
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = dataSourceBuilder.Build();
    }

    /// <summary>
    /// Create a new connection from the pool. Caller must dispose.
    /// Usage: await using var conn = await factory.OpenConnectionAsync();
    /// </summary>
    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = _dataSource.CreateConnection();
        await connection.OpenAsync(ct);
        return connection;
    }

    /// <summary>
    /// Test database connectivity. Returns (success, errorMessage).
    /// </summary>
    public async Task<(bool Success, string? Error)> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
