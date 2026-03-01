using System.Runtime.CompilerServices;
using Invekto.Shared.Logging;
using Microsoft.Data.SqlClient;

namespace Invekto.WhatsAppAnalytics.Services;

/// <summary>
/// Reads customer chat data from INMA MSSQL databases via streaming SqlDataReader.
/// Forward-only, read-only. WITH (NOLOCK) for zero lock impact on production.
/// Drop-in replacement for CsvStreamReader when source_type = "mssql".
/// Yields chunks of string[] rows in the same 7-column order as CSV:
/// [business_phone, date, time, conversation_id, message_text, sender_type, agent_name]
/// </summary>
public sealed class MssqlReaderService
{
    private readonly string _server;
    private readonly int _port;
    private readonly string _user;
    private readonly string _password;
    private readonly JsonLinesLogger _logger;

    public MssqlReaderService(string server, int port, string user, string password, JsonLinesLogger logger)
    {
        _server = server;
        _port = port;
        _user = user;
        _password = password;
        _logger = logger;
    }

    internal string BuildConnectionString(string database) =>
        $"Server={_server},{_port};Database={database};User Id={_user};Password={_password};" +
        "TrustServerCertificate=True;Encrypt=False;Connection Timeout=30;Command Timeout=600;";

    /// <summary>
    /// Create an open SqlConnection to a specific customer database.
    /// Used by insight engines for custom queries.
    /// </summary>
    public async Task<SqlConnection> CreateConnectionAsync(string database, CancellationToken ct = default)
    {
        var conn = new SqlConnection(BuildConnectionString(database));
        await conn.OpenAsync(ct);
        return conn;
    }

    private const string ChatQuery = @"
SELECT
  CASE WHEN C.InstanceType = 1 THEN C.InstancePhoneNumber
       ELSE C.InstanceAccountID END AS business_phone,
  CONVERT(VARCHAR(10), ISNULL(CM.SentTime, CM.CreateDate), 120) AS [date],
  CONVERT(VARCHAR(8), ISNULL(CM.SentTime, CM.CreateDate), 108) AS [time],
  CASE WHEN C.InstanceType = 1 THEN C.CustomerPhoneNumber
       ELSE C.CustomerAccountID END AS conversation_id,
  REPLACE(REPLACE(REPLACE(REPLACE(CM.Body, CHAR(13), ''), CHAR(10), ''), CHAR(9), ''), CHAR(34), '') AS message_text,
  CASE WHEN CM.FromMe = 0 THEN 'CUSTOMER' ELSE 'ME' END AS sender_type,
  ISNULL(U.Name + ' ' + U.Surname, '') AS agent_name
FROM ChatMessages CM WITH (NOLOCK)
INNER JOIN Chats C WITH (NOLOCK) ON CM.ChatID = C.ID
LEFT JOIN Users U WITH (NOLOCK) ON CM.UserID = U.ID
WHERE CM.MessageType = 1
  AND CM.SystemMessageType IS NULL
  AND C.CustomerPhoneNumber IS NOT NULL
  AND C.IsGroup = 0
  AND CM.Body NOT IN (N'Dosya İndirilememiştir', N'Media could not be downloaded')
  AND LEN(CM.Body) > 0
  AND C.InstanceID = @instanceId
ORDER BY C.InstancePhoneNumber, C.CustomerPhoneNumber,
         ISNULL(CM.SentTime, CM.CreateDate)";

    private const string CountQuery = @"
SELECT COUNT_BIG(*)
FROM ChatMessages CM WITH (NOLOCK)
INNER JOIN Chats C WITH (NOLOCK) ON CM.ChatID = C.ID
WHERE CM.MessageType = 1
  AND CM.SystemMessageType IS NULL
  AND C.CustomerPhoneNumber IS NOT NULL
  AND C.IsGroup = 0
  AND CM.Body NOT IN (N'Dosya İndirilememiştir', N'Media could not be downloaded')
  AND LEN(CM.Body) > 0
  AND C.InstanceID = @instanceId";

    /// <summary>
    /// Count total rows that will be exported (for progress reporting).
    /// </summary>
    public async Task<long> CountAsync(string database, int instanceId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(BuildConnectionString(database));
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(CountQuery, conn);
        cmd.CommandTimeout = 120;
        cmd.Parameters.AddWithValue("@instanceId", instanceId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is long l ? l : Convert.ToInt64(result);
    }

    /// <summary>
    /// Stream chat messages in chunks matching the 7-column CSV format.
    /// Uses forward-only SqlDataReader — constant memory regardless of result size.
    /// </summary>
    public async IAsyncEnumerable<List<string[]>> StreamChunksAsync(
        string database, int instanceId, int chunkSize = 100_000,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.SystemInfo($"[MssqlReaderService] Streaming from {database}, instanceId={instanceId}");

        await using var conn = new SqlConnection(BuildConnectionString(database));
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(ChatQuery, conn);
        cmd.CommandTimeout = 600; // 10 min for large datasets
        cmd.Parameters.AddWithValue("@instanceId", instanceId);

        await using var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, ct);

        var chunk = new List<string[]>(chunkSize);

        while (await reader.ReadAsync(ct))
        {
            var row = new string[7];
            row[0] = reader.IsDBNull(0) ? "" : reader.GetString(0); // business_phone
            row[1] = reader.IsDBNull(1) ? "" : reader.GetString(1); // date
            row[2] = reader.IsDBNull(2) ? "" : reader.GetString(2); // time
            row[3] = reader.IsDBNull(3) ? "" : reader.GetString(3); // conversation_id
            row[4] = reader.IsDBNull(4) ? "" : reader.GetString(4); // message_text
            row[5] = reader.IsDBNull(5) ? "" : reader.GetString(5); // sender_type
            row[6] = reader.IsDBNull(6) ? "" : reader.GetString(6); // agent_name

            chunk.Add(row);

            if (chunk.Count >= chunkSize)
            {
                yield return chunk;
                chunk = new List<string[]>(chunkSize);
            }
        }

        if (chunk.Count > 0)
            yield return chunk;

        _logger.SystemInfo($"[MssqlReaderService] Streaming complete for {database}, instanceId={instanceId}");
    }
}
