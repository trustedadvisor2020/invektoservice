using Npgsql;
using Pgvector.Npgsql;

namespace Invekto.Knowledge.Data;

/// <summary>
/// PostgreSQL connection factory with pgvector support.
/// Extends shared pattern with UseVector() for embedding operations.
/// </summary>
public sealed class KnowledgeConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;

    public KnowledgeConnectionFactory(string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        _dataSource = dataSourceBuilder.Build();
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = _dataSource.CreateConnection();
        await connection.OpenAsync(ct);
        return connection;
    }
}
