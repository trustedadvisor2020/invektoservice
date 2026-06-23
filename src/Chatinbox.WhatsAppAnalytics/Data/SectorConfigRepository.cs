using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Models;

namespace Chatinbox.WhatsAppAnalytics.Data;

/// <summary>
/// Repository for wa_sector_config.
/// </summary>
public sealed class SectorConfigRepository
{
    private readonly AnalyticsConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public SectorConfigRepository(AnalyticsConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<SectorConfig>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, sector_key, display_name, label_overrides::TEXT, custom_prompt,
                   benchmark_f1, is_active, created_at, updated_at
            FROM wa_sector_config
            ORDER BY sector_key";

        var result = new List<SectorConfig>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new SectorConfig
            {
                Id = reader.GetInt32(0),
                SectorKey = reader.GetString(1),
                DisplayName = reader.GetString(2),
                LabelOverrides = reader.IsDBNull(3) ? null : reader.GetString(3),
                CustomPrompt = reader.IsDBNull(4) ? null : reader.GetString(4),
                BenchmarkF1 = reader.IsDBNull(5) ? null : reader.GetFloat(5),
                IsActive = reader.GetBoolean(6),
                CreatedAt = reader.GetDateTime(7),
                UpdatedAt = reader.GetDateTime(8)
            });
        }
        return result;
    }

    public async Task<SectorConfig?> GetByKeyAsync(string sectorKey, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, sector_key, display_name, label_overrides::TEXT, custom_prompt,
                   benchmark_f1, is_active, created_at, updated_at
            FROM wa_sector_config
            WHERE sector_key = @key";
        cmd.Parameters.AddWithValue("key", sectorKey);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new SectorConfig
        {
            Id = reader.GetInt32(0),
            SectorKey = reader.GetString(1),
            DisplayName = reader.GetString(2),
            LabelOverrides = reader.IsDBNull(3) ? null : reader.GetString(3),
            CustomPrompt = reader.IsDBNull(4) ? null : reader.GetString(4),
            BenchmarkF1 = reader.IsDBNull(5) ? null : reader.GetFloat(5),
            IsActive = reader.GetBoolean(6),
            CreatedAt = reader.GetDateTime(7),
            UpdatedAt = reader.GetDateTime(8)
        };
    }

    public async Task UpdateBenchmarkF1Async(string sectorKey, float f1, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE wa_sector_config
            SET benchmark_f1 = @f1, updated_at = NOW()
            WHERE sector_key = @key";
        cmd.Parameters.AddWithValue("key", sectorKey);
        cmd.Parameters.AddWithValue("f1", f1);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
