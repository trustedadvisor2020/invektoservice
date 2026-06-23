using System.Security.Cryptography;
using System.Text;
using Chatinbox.Shared.Data;
using Chatinbox.Shared.Logging;
using Npgsql;

namespace Chatinbox.Backend.Services;

/// <summary>
/// DB-backed translation cache with 7-day TTL.
/// Thread-safe singleton, uses PostgresConnectionFactory for connection pooling.
/// </summary>
public sealed class TranslationCacheRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);

    public TranslationCacheRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Expose connection for stats queries (Ops dashboard).
    /// </summary>
    public Task<NpgsqlConnection> GetConnectionAsync(CancellationToken ct = default)
        => _db.OpenConnectionAsync(ct);

    /// <summary>
    /// SHA256 hash of source text for fast lookup (avoids indexing full text).
    /// </summary>
    public static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Look up a cached translation. Returns null if not found or expired.
    /// </summary>
    public async Task<CachedTranslation?> GetAsync(int tenantId, string sourceHash, string targetLanguage, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT translated_text, source_language
            FROM message_translations
            WHERE tenant_id = @tid AND source_hash = @hash AND target_language = @tgt
              AND expires_at > NOW()
            LIMIT 1";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("hash", sourceHash);
        cmd.Parameters.AddWithValue("tgt", targetLanguage);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new CachedTranslation
            {
                TranslatedText = reader.GetString(0),
                SourceLanguage = reader.IsDBNull(1) ? null : reader.GetString(1)
            };
        }
        return null;
    }

    /// <summary>
    /// Look up multiple cached translations in one query. Returns dict keyed by source_hash.
    /// </summary>
    public async Task<Dictionary<string, CachedTranslation>> GetBatchAsync(
        int tenantId, IReadOnlyList<string> sourceHashes, string targetLanguage, CancellationToken ct = default)
    {
        if (sourceHashes.Count == 0) return new Dictionary<string, CachedTranslation>();

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT source_hash, translated_text, source_language
            FROM message_translations
            WHERE tenant_id = @tid AND target_language = @tgt AND expires_at > NOW()
              AND source_hash = ANY(@hashes)";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("tgt", targetLanguage);
        cmd.Parameters.AddWithValue("hashes", sourceHashes.ToArray());

        var result = new Dictionary<string, CachedTranslation>(sourceHashes.Count);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var hash = reader.GetString(0);
            result[hash] = new CachedTranslation
            {
                TranslatedText = reader.GetString(1),
                SourceLanguage = reader.IsDBNull(2) ? null : reader.GetString(2)
            };
        }
        return result;
    }

    /// <summary>
    /// Save a translation to cache (upsert — refresh TTL on duplicate).
    /// </summary>
    public async Task SaveAsync(int tenantId, string sourceHash, string sourceText,
        string? sourceLanguage, string targetLanguage, string translatedText, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO message_translations (tenant_id, source_hash, source_text, source_language, target_language, translated_text, expires_at)
            VALUES (@tid, @hash, @src, @slang, @tgt, @trans, @exp)
            ON CONFLICT (tenant_id, source_hash, target_language)
            DO UPDATE SET translated_text = @trans, source_language = @slang, expires_at = @exp";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("hash", sourceHash);
        cmd.Parameters.AddWithValue("src", sourceText);
        cmd.Parameters.AddWithValue("slang", (object?)sourceLanguage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tgt", targetLanguage);
        cmd.Parameters.AddWithValue("trans", translatedText);
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.Add(CacheTtl));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Delete expired cache entries. Called by TranslationCleanupService.
    /// Returns number of deleted rows.
    /// NOTE: Intentionally cross-tenant — cleanup removes ALL expired rows regardless of tenant_id.
    /// Expired entries have no business value; per-tenant filtering would require iterating all tenants.
    /// </summary>
    public async Task<int> DeleteExpiredAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM message_translations WHERE expires_at < NOW()";
        return await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>
/// Cache lookup result.
/// </summary>
public sealed class CachedTranslation
{
    public string TranslatedText { get; set; } = "";
    public string? SourceLanguage { get; set; }
}
