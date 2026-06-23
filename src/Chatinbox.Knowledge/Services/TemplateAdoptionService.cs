using System.Diagnostics;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Chatinbox.Knowledge.Data;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.DTOs.Templates;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Knowledge.Services;

/// <summary>
/// Adopts catalog templates into tenant-specific target services.
/// FAQ/Intent → Knowledge internal tables. Message → Outbound HTTP. Flow → Automation HTTP.
/// Scenario → reference only (injected into AgentAI prompts).
/// </summary>
public sealed class TemplateAdoptionService
{
    private readonly TemplateRepository _templateRepo;
    private readonly KnowledgeRepository _knowledgeRepo;
    private readonly KnowledgeConnectionFactory _db;
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;

    public TemplateAdoptionService(
        TemplateRepository templateRepo,
        KnowledgeRepository knowledgeRepo,
        KnowledgeConnectionFactory db,
        HttpClient httpClient,
        JsonLinesLogger logger)
    {
        _templateRepo = templateRepo;
        _knowledgeRepo = knowledgeRepo;
        _db = db;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Adopts a single template for a tenant. Clones content to the appropriate target service.
    /// </summary>
    public async Task<TemplateAdoptionDto?> AdoptAsync(
        int tenantId, int templateId, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct);
        if (template == null || !template.IsPublished || !template.IsActive)
            return null;

        var targetType = MapTemplateTypeToTargetType(template.TemplateType);
        int? targetId = null;

        try
        {
            targetId = template.TemplateType switch
            {
                "faq" => await AdoptFaqAsync(tenantId, template, ct),
                "intent" => await AdoptIntentAsync(tenantId, template, ct),
                "message" => await AdoptMessageAsync(tenantId, template, ct),
                "flow" => await AdoptFlowAsync(tenantId, template, ct),
                "scenario" => null, // Reference only — no clone needed
                _ => null
            };
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] " +
                $"Adopt DB error: template={templateId}, tenant={tenantId}, type={template.TemplateType}: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] " +
                $"Adopt parse error: template={templateId}, tenant={tenantId}, type={template.TemplateType}: {ex.Message}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] " +
                $"Adopt service error: template={templateId}, tenant={tenantId}, type={template.TemplateType}: {ex.Message}");
            return null;
        }

        var adoptionId = await _templateRepo.InsertAdoptionAsync(
            tenantId, templateId, template.Version, targetType, targetId, ct);

        if (adoptionId == 0)
        {
            _logger.SystemInfo($"[TemplateAdoption] Already adopted: template={templateId}, tenant={tenantId}");
            return null;
        }

        return new TemplateAdoptionDto
        {
            Id = adoptionId,
            TenantId = tenantId,
            TemplateId = templateId,
            TemplateName = template.Name,
            TemplateType = template.TemplateType,
            AdoptedVersion = template.Version,
            TargetType = targetType,
            TargetId = targetId,
            Customized = false,
            AdoptedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Onboard: bulk-adopt all published templates for a tenant's sector.
    /// </summary>
    public async Task<TemplateOnboardResult> OnboardAsync(
        int tenantId, string sector, string[]? templateTypes = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new TemplateOnboardResult { TenantId = tenantId, Sector = sector };

        // Get all published sector + platform templates
        var filter = new TemplateCatalogFilter
        {
            Scope = "sector",
            Sector = sector,
            Page = 1,
            Limit = 500
        };
        var (sectorTemplates, _) = await _templateRepo.ListAsync(filter, ct);

        filter.Scope = "platform";
        filter.Sector = null;
        var (platformTemplates, _) = await _templateRepo.ListAsync(filter, ct);

        var allTemplates = sectorTemplates
            .Concat(platformTemplates)
            .Where(t => t.IsPublished)
            .ToList();

        if (templateTypes is { Length: > 0 })
        {
            var typeSet = new HashSet<string>(templateTypes, StringComparer.OrdinalIgnoreCase);
            allTemplates = allTemplates.Where(t => typeSet.Contains(t.TemplateType)).ToList();
        }

        foreach (var template in allTemplates)
        {
            try
            {
                var adoption = await AdoptAsync(tenantId, template.Id, ct);
                if (adoption != null)
                {
                    result.Adoptions.Add(adoption);
                    result.AdoptedCount++;
                }
                else
                {
                    result.SkippedCount++;
                }
            }
            catch (NpgsqlException ex)
            {
                result.FailedCount++;
                _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] " +
                    $"Onboard DB error: template={template.Id}, tenant={tenantId}: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                result.FailedCount++;
                _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] " +
                    $"Onboard service error: template={template.Id}, tenant={tenantId}: {ex.Message}");
            }
            catch (JsonException ex)
            {
                result.FailedCount++;
                _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] " +
                    $"Onboard parse error: template={template.Id}, tenant={tenantId}: {ex.Message}");
            }
        }

        sw.Stop();
        result.DurationMs = sw.ElapsedMilliseconds;

        _logger.SystemInfo($"[TemplateAdoption] Onboard complete: tenant={tenantId}, sector={sector}, " +
            $"adopted={result.AdoptedCount}, skipped={result.SkippedCount}, failed={result.FailedCount}, " +
            $"duration={result.DurationMs}ms");

        return result;
    }

    // ================================================================
    // Type-specific adoption methods
    // ================================================================

    private async Task<int?> AdoptFaqAsync(
        int tenantId, TemplateCatalogDto template, CancellationToken ct)
    {
        var content = DeserializeContent(template.ContentJson);
        if (content == null) return null;

        var question = GetJsonString(content, "question") ?? template.Name;
        var answer = GetJsonString(content, "answer") ?? "";
        var category = GetJsonString(content, "category");
        var keywords = GetJsonStringArray(content, "keywords");

        await _knowledgeRepo.InsertFaqAsync(
            tenantId, question, answer, category, template.Lang,
            keywords, "template_adopt",
            JsonSerializer.Serialize(new { template_id = template.Id }), ct);

        // FAQ ID not reliably returned on upsert — adoption tracks via template_id
        return null;
    }

    private async Task<int?> AdoptIntentAsync(
        int tenantId, TemplateCatalogDto template, CancellationToken ct)
    {
        var content = DeserializeContent(template.ContentJson);
        if (content == null) return null;

        var intentName = GetJsonString(content, "intent_name") ?? template.Slug;
        var keywords = GetJsonStringArray(content, "keywords");
        var sampleMessages = GetJsonStringArray(content, "sample_messages");

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO intent_patterns (tenant_id, intent_name, keywords, sample_count, sample_messages)
            VALUES (@tid, @name, @kw, @cnt, @msgs::jsonb)
            ON CONFLICT (tenant_id, intent_name) DO NOTHING
            RETURNING id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("name", intentName);
        cmd.Parameters.Add(new NpgsqlParameter("kw",
            NpgsqlDbType.Array | NpgsqlDbType.Text)
        { Value = keywords });
        cmd.Parameters.AddWithValue("cnt", sampleMessages.Length);
        cmd.Parameters.AddWithValue("msgs", JsonSerializer.Serialize(sampleMessages));

        var result = await cmd.ExecuteScalarAsync(ct);
        return result != null ? Convert.ToInt32(result) : null;
    }

    private async Task<int?> AdoptMessageAsync(
        int tenantId, TemplateCatalogDto template, CancellationToken ct)
    {
        var url = $"http://localhost:{ServiceConstants.OutboundPort}/api/v1/outbound/{tenantId}/templates";

        var content = DeserializeContent(template.ContentJson);
        if (content == null) return null;

        var payload = new
        {
            name = template.Name,
            template_key = GetJsonString(content, "template_key") ?? template.Slug,
            message_text = GetJsonString(content, "message_text") ?? "",
            variables = GetJsonStringArray(content, "variables"),
            source = "template_adopt",
            source_template_id = template.Id
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload, ct);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (body.TryGetProperty("id", out var idProp))
                    return idProp.GetInt32();
            }
            else
            {
                _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] Outbound adopt failed: {response.StatusCode} for template={template.Id}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] Outbound service unavailable: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] Outbound service timeout: {ex.Message}");
        }

        return null;
    }

    private async Task<int?> AdoptFlowAsync(
        int tenantId, TemplateCatalogDto template, CancellationToken ct)
    {
        var url = $"http://localhost:{ServiceConstants.AutomationPort}/api/v1/automation/{tenantId}/flows";

        var content = DeserializeContent(template.ContentJson);
        if (content == null) return null;

        var payload = new
        {
            name = GetJsonString(content, "flow_name") ?? template.Name,
            flow_config = content.RootElement.TryGetProperty("flow_config", out var fc) ? fc : default,
            source = "template_adopt",
            source_template_id = template.Id
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload, ct);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (body.TryGetProperty("id", out var idProp))
                    return idProp.GetInt32();
            }
            else
            {
                _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] Automation adopt failed: {response.StatusCode} for template={template.Id}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] Automation service unavailable: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] Automation service timeout: {ex.Message}");
        }

        return null;
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static string MapTemplateTypeToTargetType(string templateType) => templateType switch
    {
        "faq" => "faq_entry",
        "intent" => "intent_pattern",
        "message" => "outbound_template",
        "flow" => "chatbot_flow",
        "scenario" => "faq",
        _ => "faq"
    };

    private JsonDocument? DeserializeContent(object? contentJson)
    {
        if (contentJson == null) return null;

        try
        {
            var json = contentJson is JsonElement el
                ? el.GetRawText()
                : JsonSerializer.Serialize(contentJson);
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.TemplateOnboardingFailed}] DeserializeContent failed: {ex.Message}");
            return null;
        }
    }

    private static string? GetJsonString(JsonDocument doc, string propertyName)
    {
        if (doc.RootElement.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    private static string[] GetJsonStringArray(JsonDocument doc, string propertyName)
    {
        if (!doc.RootElement.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var val = item.GetString();
                if (val != null) list.Add(val);
            }
        }
        return list.ToArray();
    }
}
