using System.Text;
using System.Text.Json;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Models;
using Npgsql;

namespace Invekto.WhatsAppAnalytics.Data;

/// <summary>
/// Repository for RI Faz 4 sector template tables:
/// wa_sector_intents, wa_sector_faqs, wa_sector_flows,
/// wa_sector_objection_handlers, wa_sector_followup_templates, wa_sector_onboarding_steps.
/// </summary>
public sealed class TemplateRepository
{
    private readonly AnalyticsConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public TemplateRepository(AnalyticsConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // wa_sector_intents (RI-4.1)
    // ============================================================

    public async Task DeleteIntentsBySectorAsync(string sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM wa_sector_intents WHERE sector = @s";
        cmd.Parameters.AddWithValue("s", sector);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertIntentsAsync(List<SectorIntentRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;
        await using var conn = await _db.OpenConnectionAsync(ct);

        foreach (var batch in records.Chunk(50))
        {
            var sb = new StringBuilder();
            sb.Append(@"INSERT INTO wa_sector_intents
                (sector, intent_name, description, category, examples, keywords, priority, frequency, conversion_rate, source_count, is_active)
                VALUES ");

            var pIdx = 0;
            var cmd = conn.CreateCommand();

            for (var i = 0; i < batch.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"(@p{pIdx},@p{pIdx + 1},@p{pIdx + 2},@p{pIdx + 3},@p{pIdx + 4}::jsonb,@p{pIdx + 5}::jsonb,@p{pIdx + 6},@p{pIdx + 7},@p{pIdx + 8},@p{pIdx + 9},@p{pIdx + 10})");
                var r = batch[i];
                cmd.Parameters.AddWithValue($"p{pIdx}", r.Sector);
                cmd.Parameters.AddWithValue($"p{pIdx + 1}", r.IntentName);
                cmd.Parameters.AddWithValue($"p{pIdx + 2}", (object?)r.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 3}", (object?)r.Category ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 4}", (object?)r.ExamplesJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 5}", (object?)r.KeywordsJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 6}", r.Priority);
                cmd.Parameters.AddWithValue($"p{pIdx + 7}", r.Frequency);
                cmd.Parameters.AddWithValue($"p{pIdx + 8}", r.ConversionRate.HasValue ? (object)r.ConversionRate.Value : DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 9}", r.SourceCount);
                cmd.Parameters.AddWithValue($"p{pIdx + 10}", r.IsActive);
                pIdx += 11;
            }

            sb.Append(@" ON CONFLICT (sector, intent_name) DO UPDATE SET
                description = EXCLUDED.description,
                category = EXCLUDED.category,
                examples = EXCLUDED.examples,
                keywords = EXCLUDED.keywords,
                priority = EXCLUDED.priority,
                frequency = EXCLUDED.frequency,
                conversion_rate = EXCLUDED.conversion_rate,
                source_count = EXCLUDED.source_count,
                is_active = EXCLUDED.is_active,
                updated_at = NOW()");

            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
            await cmd.DisposeAsync();
        }
    }

    public async Task<List<SectorIntentRecord>> GetIntentsBySectorAsync(string sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, sector, intent_name, description, category, examples::TEXT, keywords::TEXT,
                   priority, frequency, conversion_rate, source_count, is_active
            FROM wa_sector_intents
            WHERE sector = @s AND is_active = TRUE
            ORDER BY priority DESC, frequency DESC";
        cmd.Parameters.AddWithValue("s", sector);

        var results = new List<SectorIntentRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SectorIntentRecord
            {
                Id = reader.GetInt64(0),
                Sector = reader.GetString(1),
                IntentName = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                Category = reader.IsDBNull(4) ? null : reader.GetString(4),
                ExamplesJson = reader.IsDBNull(5) ? null : reader.GetString(5),
                KeywordsJson = reader.IsDBNull(6) ? null : reader.GetString(6),
                Priority = reader.GetInt32(7),
                Frequency = reader.GetInt32(8),
                ConversionRate = reader.IsDBNull(9) ? null : reader.GetFloat(9),
                SourceCount = reader.GetInt32(10),
                IsActive = reader.GetBoolean(11)
            });
        }
        return results;
    }

    // ============================================================
    // wa_sector_faqs (RI-4.2)
    // ============================================================

    public async Task DeleteFaqsBySectorAsync(string sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM wa_sector_faqs WHERE sector = @s";
        cmd.Parameters.AddWithValue("s", sector);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertFaqsAsync(List<SectorFaqRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;
        await using var conn = await _db.OpenConnectionAsync(ct);

        foreach (var batch in records.Chunk(50))
        {
            var sb = new StringBuilder();
            sb.Append(@"INSERT INTO wa_sector_faqs
                (sector, question, answer, category, keywords, effectiveness_score, source_count, is_active)
                VALUES ");

            var pIdx = 0;
            var cmd = conn.CreateCommand();

            for (var i = 0; i < batch.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"(@p{pIdx},@p{pIdx + 1},@p{pIdx + 2},@p{pIdx + 3},@p{pIdx + 4}::jsonb,@p{pIdx + 5},@p{pIdx + 6},@p{pIdx + 7})");
                var r = batch[i];
                cmd.Parameters.AddWithValue($"p{pIdx}", r.Sector);
                cmd.Parameters.AddWithValue($"p{pIdx + 1}", r.Question);
                cmd.Parameters.AddWithValue($"p{pIdx + 2}", r.Answer);
                cmd.Parameters.AddWithValue($"p{pIdx + 3}", (object?)r.Category ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 4}", (object?)r.KeywordsJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 5}", r.EffectivenessScore);
                cmd.Parameters.AddWithValue($"p{pIdx + 6}", r.SourceCount);
                cmd.Parameters.AddWithValue($"p{pIdx + 7}", r.IsActive);
                pIdx += 8;
            }

            // FAQs don't have unique constraint on question, just insert
            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
            await cmd.DisposeAsync();
        }
    }

    public async Task<List<SectorFaqRecord>> GetFaqsBySectorAsync(string sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, sector, question, answer, category, keywords::TEXT,
                   effectiveness_score, source_count, is_active
            FROM wa_sector_faqs
            WHERE sector = @s AND is_active = TRUE
            ORDER BY effectiveness_score DESC";
        cmd.Parameters.AddWithValue("s", sector);

        var results = new List<SectorFaqRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SectorFaqRecord
            {
                Id = reader.GetInt64(0),
                Sector = reader.GetString(1),
                Question = reader.GetString(2),
                Answer = reader.GetString(3),
                Category = reader.IsDBNull(4) ? null : reader.GetString(4),
                KeywordsJson = reader.IsDBNull(5) ? null : reader.GetString(5),
                EffectivenessScore = reader.GetFloat(6),
                SourceCount = reader.GetInt32(7),
                IsActive = reader.GetBoolean(8)
            });
        }
        return results;
    }

    // ============================================================
    // wa_sector_flows (RI-4.3)
    // ============================================================

    public async Task DeleteFlowsBySectorAsync(string sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM wa_sector_flows WHERE sector = @s";
        cmd.Parameters.AddWithValue("s", sector);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertFlowsAsync(List<SectorFlowRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;
        await using var conn = await _db.OpenConnectionAsync(ct);

        foreach (var batch in records.Chunk(50))
        {
            var sb = new StringBuilder();
            sb.Append(@"INSERT INTO wa_sector_flows
                (sector, flow_name, flow_type, description, stages, drop_off_analysis, conversion_rate, source_count, is_active)
                VALUES ");

            var pIdx = 0;
            var cmd = conn.CreateCommand();

            for (var i = 0; i < batch.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"(@p{pIdx},@p{pIdx + 1},@p{pIdx + 2},@p{pIdx + 3},@p{pIdx + 4}::jsonb,@p{pIdx + 5}::jsonb,@p{pIdx + 6},@p{pIdx + 7},@p{pIdx + 8})");
                var r = batch[i];
                cmd.Parameters.AddWithValue($"p{pIdx}", r.Sector);
                cmd.Parameters.AddWithValue($"p{pIdx + 1}", r.FlowName);
                cmd.Parameters.AddWithValue($"p{pIdx + 2}", r.FlowType);
                cmd.Parameters.AddWithValue($"p{pIdx + 3}", (object?)r.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 4}", r.StagesJson);
                cmd.Parameters.AddWithValue($"p{pIdx + 5}", (object?)r.DropOffAnalysisJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 6}", r.ConversionRate.HasValue ? (object)r.ConversionRate.Value : DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 7}", r.SourceCount);
                cmd.Parameters.AddWithValue($"p{pIdx + 8}", r.IsActive);
                pIdx += 9;
            }

            sb.Append(@" ON CONFLICT (sector, flow_name) DO UPDATE SET
                flow_type = EXCLUDED.flow_type,
                description = EXCLUDED.description,
                stages = EXCLUDED.stages,
                drop_off_analysis = EXCLUDED.drop_off_analysis,
                conversion_rate = EXCLUDED.conversion_rate,
                source_count = EXCLUDED.source_count,
                is_active = EXCLUDED.is_active,
                updated_at = NOW()");

            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
            await cmd.DisposeAsync();
        }
    }

    public async Task<List<SectorFlowRecord>> GetFlowsBySectorAsync(string sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, sector, flow_name, flow_type, description, stages::TEXT,
                   drop_off_analysis::TEXT, conversion_rate, source_count, is_active
            FROM wa_sector_flows
            WHERE sector = @s AND is_active = TRUE
            ORDER BY flow_type, flow_name";
        cmd.Parameters.AddWithValue("s", sector);

        var results = new List<SectorFlowRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SectorFlowRecord
            {
                Id = reader.GetInt64(0),
                Sector = reader.GetString(1),
                FlowName = reader.GetString(2),
                FlowType = reader.GetString(3),
                Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                StagesJson = reader.GetString(5),
                DropOffAnalysisJson = reader.IsDBNull(6) ? null : reader.GetString(6),
                ConversionRate = reader.IsDBNull(7) ? null : reader.GetFloat(7),
                SourceCount = reader.GetInt32(8),
                IsActive = reader.GetBoolean(9)
            });
        }
        return results;
    }

    // ============================================================
    // wa_sector_objection_handlers (RI-4.4)
    // ============================================================

    public async Task DeleteObjectionHandlersBySectorAsync(string sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM wa_sector_objection_handlers WHERE sector = @s";
        cmd.Parameters.AddWithValue("s", sector);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertObjectionHandlersAsync(List<SectorObjectionHandlerRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;
        await using var conn = await _db.OpenConnectionAsync(ct);

        foreach (var batch in records.Chunk(50))
        {
            var sb = new StringBuilder();
            sb.Append(@"INSERT INTO wa_sector_objection_handlers
                (sector, objection_type, objection_label, response_templates, total_occurrences, rescue_rate, is_active)
                VALUES ");

            var pIdx = 0;
            var cmd = conn.CreateCommand();

            for (var i = 0; i < batch.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"(@p{pIdx},@p{pIdx + 1},@p{pIdx + 2},@p{pIdx + 3}::jsonb,@p{pIdx + 4},@p{pIdx + 5},@p{pIdx + 6})");
                var r = batch[i];
                cmd.Parameters.AddWithValue($"p{pIdx}", r.Sector);
                cmd.Parameters.AddWithValue($"p{pIdx + 1}", r.ObjectionType);
                cmd.Parameters.AddWithValue($"p{pIdx + 2}", r.ObjectionLabel);
                cmd.Parameters.AddWithValue($"p{pIdx + 3}", r.ResponseTemplatesJson);
                cmd.Parameters.AddWithValue($"p{pIdx + 4}", r.TotalOccurrences);
                cmd.Parameters.AddWithValue($"p{pIdx + 5}", r.RescueRate);
                cmd.Parameters.AddWithValue($"p{pIdx + 6}", r.IsActive);
                pIdx += 7;
            }

            sb.Append(@" ON CONFLICT (sector, objection_type) DO UPDATE SET
                objection_label = EXCLUDED.objection_label,
                response_templates = EXCLUDED.response_templates,
                total_occurrences = EXCLUDED.total_occurrences,
                rescue_rate = EXCLUDED.rescue_rate,
                is_active = EXCLUDED.is_active,
                updated_at = NOW()");

            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
            await cmd.DisposeAsync();
        }
    }

    public async Task<List<SectorObjectionHandlerRecord>> GetObjectionHandlersBySectorAsync(
        string sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, sector, objection_type, objection_label, response_templates::TEXT,
                   total_occurrences, rescue_rate, is_active
            FROM wa_sector_objection_handlers
            WHERE sector = @s AND is_active = TRUE
            ORDER BY total_occurrences DESC";
        cmd.Parameters.AddWithValue("s", sector);

        var results = new List<SectorObjectionHandlerRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SectorObjectionHandlerRecord
            {
                Id = reader.GetInt64(0),
                Sector = reader.GetString(1),
                ObjectionType = reader.GetString(2),
                ObjectionLabel = reader.GetString(3),
                ResponseTemplatesJson = reader.GetString(4),
                TotalOccurrences = reader.GetInt32(5),
                RescueRate = reader.GetFloat(6),
                IsActive = reader.GetBoolean(7)
            });
        }
        return results;
    }

    // ============================================================
    // wa_sector_followup_templates (RI-4.5)
    // ============================================================

    public async Task DeleteFollowupTemplatesBySectorAsync(string sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM wa_sector_followup_templates WHERE sector = @s";
        cmd.Parameters.AddWithValue("s", sector);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertFollowupTemplatesAsync(List<SectorFollowupTemplateRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;
        await using var conn = await _db.OpenConnectionAsync(ct);

        foreach (var batch in records.Chunk(50))
        {
            var sb = new StringBuilder();
            sb.Append(@"INSERT INTO wa_sector_followup_templates
                (sector, template_type, template_label, message_template, optimal_delay_hours, rescue_rate, source_count, is_active)
                VALUES ");

            var pIdx = 0;
            var cmd = conn.CreateCommand();

            for (var i = 0; i < batch.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"(@p{pIdx},@p{pIdx + 1},@p{pIdx + 2},@p{pIdx + 3},@p{pIdx + 4},@p{pIdx + 5},@p{pIdx + 6},@p{pIdx + 7})");
                var r = batch[i];
                cmd.Parameters.AddWithValue($"p{pIdx}", r.Sector);
                cmd.Parameters.AddWithValue($"p{pIdx + 1}", r.TemplateType);
                cmd.Parameters.AddWithValue($"p{pIdx + 2}", r.TemplateLabel);
                cmd.Parameters.AddWithValue($"p{pIdx + 3}", r.MessageTemplate);
                cmd.Parameters.AddWithValue($"p{pIdx + 4}", r.OptimalDelayHours);
                cmd.Parameters.AddWithValue($"p{pIdx + 5}", r.RescueRate);
                cmd.Parameters.AddWithValue($"p{pIdx + 6}", r.SourceCount);
                cmd.Parameters.AddWithValue($"p{pIdx + 7}", r.IsActive);
                pIdx += 8;
            }

            sb.Append(@" ON CONFLICT (sector, template_type) DO UPDATE SET
                template_label = EXCLUDED.template_label,
                message_template = EXCLUDED.message_template,
                optimal_delay_hours = EXCLUDED.optimal_delay_hours,
                rescue_rate = EXCLUDED.rescue_rate,
                source_count = EXCLUDED.source_count,
                is_active = EXCLUDED.is_active,
                updated_at = NOW()");

            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
            await cmd.DisposeAsync();
        }
    }

    public async Task<List<SectorFollowupTemplateRecord>> GetFollowupTemplatesBySectorAsync(
        string sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, sector, template_type, template_label, message_template,
                   optimal_delay_hours, rescue_rate, source_count, is_active
            FROM wa_sector_followup_templates
            WHERE sector = @s AND is_active = TRUE
            ORDER BY optimal_delay_hours";
        cmd.Parameters.AddWithValue("s", sector);

        var results = new List<SectorFollowupTemplateRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SectorFollowupTemplateRecord
            {
                Id = reader.GetInt64(0),
                Sector = reader.GetString(1),
                TemplateType = reader.GetString(2),
                TemplateLabel = reader.GetString(3),
                MessageTemplate = reader.GetString(4),
                OptimalDelayHours = reader.GetInt32(5),
                RescueRate = reader.GetFloat(6),
                SourceCount = reader.GetInt32(7),
                IsActive = reader.GetBoolean(8)
            });
        }
        return results;
    }

    // ============================================================
    // wa_sector_onboarding_steps (RI-4.6)
    // ============================================================

    public async Task DeleteOnboardingStepsBySectorAsync(string sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM wa_sector_onboarding_steps WHERE sector = @s";
        cmd.Parameters.AddWithValue("s", sector);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertOnboardingStepsAsync(List<SectorOnboardingStepRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;
        await using var conn = await _db.OpenConnectionAsync(ct);

        foreach (var batch in records.Chunk(50))
        {
            var sb = new StringBuilder();
            sb.Append(@"INSERT INTO wa_sector_onboarding_steps
                (sector, step_number, action, description, expected_impact, day_range, is_active)
                VALUES ");

            var pIdx = 0;
            var cmd = conn.CreateCommand();

            for (var i = 0; i < batch.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"(@p{pIdx},@p{pIdx + 1},@p{pIdx + 2},@p{pIdx + 3},@p{pIdx + 4},@p{pIdx + 5},@p{pIdx + 6})");
                var r = batch[i];
                cmd.Parameters.AddWithValue($"p{pIdx}", r.Sector);
                cmd.Parameters.AddWithValue($"p{pIdx + 1}", r.StepNumber);
                cmd.Parameters.AddWithValue($"p{pIdx + 2}", r.Action);
                cmd.Parameters.AddWithValue($"p{pIdx + 3}", (object?)r.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 4}", (object?)r.ExpectedImpact ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 5}", (object?)r.DayRange ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"p{pIdx + 6}", r.IsActive);
                pIdx += 7;
            }

            sb.Append(@" ON CONFLICT (sector, step_number) DO UPDATE SET
                action = EXCLUDED.action,
                description = EXCLUDED.description,
                expected_impact = EXCLUDED.expected_impact,
                day_range = EXCLUDED.day_range,
                is_active = EXCLUDED.is_active,
                updated_at = NOW()");

            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
            await cmd.DisposeAsync();
        }
    }

    public async Task<List<SectorOnboardingStepRecord>> GetOnboardingStepsBySectorAsync(
        string sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, sector, step_number, action, description, expected_impact, day_range, is_active
            FROM wa_sector_onboarding_steps
            WHERE sector = @s AND is_active = TRUE
            ORDER BY step_number";
        cmd.Parameters.AddWithValue("s", sector);

        var results = new List<SectorOnboardingStepRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SectorOnboardingStepRecord
            {
                Id = reader.GetInt64(0),
                Sector = reader.GetString(1),
                StepNumber = reader.GetInt32(2),
                Action = reader.GetString(3),
                Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                ExpectedImpact = reader.IsDBNull(5) ? null : reader.GetString(5),
                DayRange = reader.IsDBNull(6) ? null : reader.GetString(6),
                IsActive = reader.GetBoolean(7)
            });
        }
        return results;
    }

    // ============================================================
    // Aggregated: Get all templates for a sector
    // ============================================================

    public async Task<SectorTemplatesResponse> GetAllTemplatesBySectorAsync(
        string sector, CancellationToken ct = default)
    {
        return new SectorTemplatesResponse
        {
            Sector = sector,
            Intents = await GetIntentsBySectorAsync(sector, ct),
            Faqs = await GetFaqsBySectorAsync(sector, ct),
            Flows = await GetFlowsBySectorAsync(sector, ct),
            ObjectionHandlers = await GetObjectionHandlersBySectorAsync(sector, ct),
            FollowupTemplates = await GetFollowupTemplatesBySectorAsync(sector, ct),
            OnboardingSteps = await GetOnboardingStepsBySectorAsync(sector, ct)
        };
    }

    // ============================================================
    // Toggle template active/inactive (RI Faz 6)
    // ============================================================

    private static readonly Dictionary<string, string> TemplateTableMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["intent"] = "wa_sector_intents",
        ["faq"] = "wa_sector_faqs",
        ["flow"] = "wa_sector_flows",
        ["objection"] = "wa_sector_objection_handlers",
        ["followup"] = "wa_sector_followup_templates",
        ["onboarding"] = "wa_sector_onboarding_steps"
    };

    public async Task ToggleTemplateActiveAsync(string type, long id, bool isActive, CancellationToken ct = default)
    {
        if (!TemplateTableMap.TryGetValue(type, out var table))
            throw new ArgumentException($"Unknown template type '{type}'. Use: {string.Join(", ", TemplateTableMap.Keys)}");

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        // Table name is from a fixed whitelist — safe from injection
        cmd.CommandText = $"UPDATE {table} SET is_active = @active, updated_at = NOW() WHERE id = @id";
        cmd.Parameters.AddWithValue("active", isActive);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows == 0)
            throw new ArgumentException($"Template {type}/{id} not found");
    }
}
