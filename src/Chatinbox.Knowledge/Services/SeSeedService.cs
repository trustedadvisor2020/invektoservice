using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chatinbox.Knowledge.Data;
using Chatinbox.Shared.DTOs.Templates;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Knowledge.Services;

/// <summary>
/// Seeds template_catalog from SE (Sales Engineering) scenario JSON files.
/// Extracts scenario, FAQ, and intent templates per sector.
/// Idempotent: ON CONFLICT DO NOTHING via BatchInsertTemplatesAsync.
/// </summary>
public sealed class SeSeedService
{
    private readonly TemplateRepository _templateRepo;
    private readonly JsonLinesLogger _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── Niche → DB Sector Mapping ──────────────────────────────
    private static readonly Dictionary<string, (string? Sector, string Scope)> NicheMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ecommerce"]   = ("eticaret",   "sector"),
        ["dental"]      = ("dis_klinik", "sector"),
        ["aesthetic"]   = ("estetik",    "sector"),
        ["health"]      = ("saglik",     "sector"),
        ["hotel"]       = ("otel",       "sector"),
        ["beauty"]      = ("guzellik",   "sector"),
        ["education"]   = ("egitim",     "sector"),
        ["mobile"]      = ("mobil",      "sector"),
        ["crossSector"] = (null,         "platform"),
        ["universal"]   = (null,         "platform"),
    };

    // ── Turkish Stopwords ──────────────────────────────────────
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "bir", "bu", "ve", "ile", "de", "da", "mi", "mu", "mı", "mü",
        "için", "ne", "gibi", "var", "yok", "ben", "sen", "o", "biz",
        "siz", "onlar", "ama", "fakat", "ya", "veya", "ki", "daha",
        "en", "cok", "az", "her", "tum", "bazi", "hangi", "nasil",
        "neden", "nereye", "nereden", "iste", "simdi", "sonra", "once"
    };

    private const int BatchSize = 50;

    public SeSeedService(TemplateRepository templateRepo, JsonLinesLogger logger)
    {
        _templateRepo = templateRepo;
        _logger = logger;
    }

    /// <summary>
    /// Reads SE scenario JSONs, extracts templates, and seeds template_catalog.
    /// </summary>
    public async Task<SeSeedResult> SeedAsync(string dataPath, bool dryRun, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var result = new SeSeedResult { DryRun = dryRun };

        // ── 1. Read all JSON files ─────────────────────────────
        if (!Directory.Exists(dataPath))
        {
            _logger.SystemWarn($"SE data path not found: {dataPath}");
            result.Errors = 1;
            return result;
        }

        var jsonFiles = Directory.GetFiles(dataPath, "*.json")
            .Where(f => !Path.GetFileName(f).Equals("field-scenarios.json", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        _logger.SystemInfo($"SE seed: found {jsonFiles.Length} scenario files in {dataPath}");

        // ── 2. Parse scenarios ─────────────────────────────────
        var scenarios = new List<SeScenario>();
        foreach (var file in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var scenario = JsonSerializer.Deserialize<SeScenario>(json, JsonOpts);
                if (scenario?.Id != null && scenario.Title != null && scenario.Niche != null)
                {
                    scenarios.Add(scenario);
                }
                else
                {
                    _logger.SystemWarn($"SE seed: skipped {Path.GetFileName(file)} — missing id/title/niche");
                    result.Skipped++;
                }
            }
            catch (JsonException ex)
            {
                _logger.SystemWarn($"SE seed: JSON parse error in {Path.GetFileName(file)}: {ex.Message}");
                result.Errors++;
            }
            catch (IOException ex)
            {
                _logger.SystemWarn($"SE seed: file read error {Path.GetFileName(file)}: {ex.Message}");
                result.Errors++;
            }
        }

        _logger.SystemInfo($"SE seed: parsed {scenarios.Count} scenarios ({result.Skipped} skipped, {result.Errors} errors)");

        // ── 3. Extract templates ───────────────────────────────
        var allTemplates = new List<TemplateCreateRequest>();

        // Intent accumulator for cross-scenario dedup within same sector
        var intentAccumulator = new Dictionary<string, IntentAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var scenario in scenarios)
        {
            var mapping = MapNiche(scenario.Niche);
            if (mapping == null)
            {
                _logger.SystemWarn($"SE seed: unknown niche '{scenario.Niche}' in {scenario.Id}");
                result.Skipped++;
                continue;
            }

            var (sector, scope) = mapping.Value;

            // 3a. Scenario template
            var scenarioTemplate = ExtractScenarioTemplate(scenario, sector, scope);
            allTemplates.Add(scenarioTemplate);
            result.Scenarios++;
            IncrementSector(result.BySector, sector ?? "platform");

            // 3b. FAQ templates from user→bot step pairs
            var faqTemplates = ExtractFaqTemplates(scenario, sector, scope);
            allTemplates.AddRange(faqTemplates);
            result.Faqs += faqTemplates.Count;
            foreach (var _ in faqTemplates)
                IncrementSector(result.BySector, sector ?? "platform");

            // 3c. Intent accumulation (dedup within sector)
            AccumulateIntents(scenario, sector, scope, intentAccumulator);
        }

        // 3d. Finalize intents from accumulator
        foreach (var kvp in intentAccumulator)
        {
            var acc = kvp.Value;
            var intentTemplate = FinalizeIntentTemplate(acc);
            allTemplates.Add(intentTemplate);
            result.Intents++;
            IncrementSector(result.BySector, acc.Sector ?? "platform");
        }

        result.TotalInserted = allTemplates.Count;
        _logger.SystemInfo($"SE seed: extracted {allTemplates.Count} templates (scenario={result.Scenarios}, faq={result.Faqs}, intent={result.Intents})");

        // ── 4. Insert into DB ──────────────────────────────────
        if (!dryRun)
        {
            var inserted = 0;
            for (var i = 0; i < allTemplates.Count; i += BatchSize)
            {
                var batch = allTemplates.Skip(i).Take(BatchSize).ToList();
                try
                {
                    inserted += await _templateRepo.BatchInsertTemplatesAsync(batch, ct);
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    _logger.SystemWarn($"SE seed: DB batch insert error at offset {i}: {ex.Message}");
                    result.Errors++;
                }
            }

            // BatchInsertTemplatesAsync returns total commands, not rows inserted (ON CONFLICT DO NOTHING)
            // Actual new inserts = total - skipped (already existing)
            _logger.SystemInfo($"SE seed: DB batch result = {inserted} commands executed for {allTemplates.Count} templates");
        }

        sw.Stop();
        result.DurationMs = sw.ElapsedMilliseconds;
        _logger.SystemInfo($"SE seed: completed in {result.DurationMs}ms (dry_run={dryRun})");

        return result;
    }

    // ═══════════════════════════════════════════════════════════
    // ── Extraction Methods ─────────────────────────────────────
    // ═══════════════════════════════════════════════════════════

    private TemplateCreateRequest ExtractScenarioTemplate(SeScenario s, string? sector, string scope)
    {
        var capabilities = ExtractCapabilities(s);
        var flowSummaries = s.Flows?.Select(f => new
        {
            id = f.Id,
            title = f.Title,
            step_count = f.Steps?.Count ?? 0
        }).ToArray() ?? [];

        var contentJson = new
        {
            se_id = s.Id,
            subtitle = s.Subtitle,
            phase = s.Phase,
            category = s.Category,
            description = s.Description,
            flow_count = s.Flows?.Count ?? 0,
            flow_summaries = flowSummaries,
            capabilities,
            impact_title = s.Impact?.Title,
            impact_formula = s.Impact?.Formula
        };

        return new TemplateCreateRequest
        {
            TemplateType = "scenario",
            Scope = scope,
            Sector = sector,
            Slug = BuildSlug(sector, "scenario", s.Title ?? ""),
            Name = s.Title ?? "",
            Description = s.Description,
            Lang = "tr",
            Tags = BuildTags("se-scenario", s.Niche ?? "", s.Phase),
            ContentJson = contentJson,
            CreatedBy = "migration"
        };
    }

    private List<TemplateCreateRequest> ExtractFaqTemplates(SeScenario s, string? sector, string scope)
    {
        var templates = new List<TemplateCreateRequest>();
        if (s.Flows == null) return templates;

        foreach (var flow in s.Flows)
        {
            if (flow.Steps == null || flow.Steps.Count < 2) continue;

            var pairIndex = 0;
            for (var i = 0; i < flow.Steps.Count - 1; i++)
            {
                var userStep = flow.Steps[i];
                var botStep = flow.Steps[i + 1];

                if (!IsValidFaqPair(userStep, botStep)) continue;

                var question = (userStep.Content ?? "").Trim();
                var answer = (botStep.Content ?? "").Trim();
                var category = NormalizeSlug(flow.Title ?? s.Category ?? "genel");
                var keywords = ExtractKeywords(question);

                var contentJson = new
                {
                    question,
                    answer,
                    category,
                    keywords,
                    source_scenario = s.Id,
                    source_flow = flow.Id
                };

                var slug = $"{sector ?? "platform"}.faq.{s.Id}.{flow.Id}.{pairIndex}".ToLowerInvariant();

                templates.Add(new TemplateCreateRequest
                {
                    TemplateType = "faq",
                    Scope = scope,
                    Sector = sector,
                    Slug = slug,
                    Name = Truncate(question, 280),
                    Description = $"FAQ: {Truncate(question, 100)} — Kaynak: {s.Title}",
                    Lang = "tr",
                    Tags = BuildTags("se-faq", s.Niche ?? "", null),
                    ContentJson = contentJson,
                    CreatedBy = "migration"
                });

                pairIndex++;
            }
        }

        return templates;
    }

    private void AccumulateIntents(SeScenario s, string? sector, string scope, Dictionary<string, IntentAccumulator> accumulator)
    {
        if (s.Flows == null) return;

        foreach (var flow in s.Flows)
        {
            // Find C8 (Agent Assist) provider requirements
            var c8Requirements = flow.Requirements?.Provider?
                .Where(r => string.Equals(r.Capability, "C8", StringComparison.OrdinalIgnoreCase))
                .ToList() ?? [];

            if (c8Requirements.Count == 0) continue;

            // Collect user messages from this flow as sample messages
            var userMessages = flow.Steps?
                .Where(step => string.Equals(step.Role, "user", StringComparison.OrdinalIgnoreCase)
                               && !string.IsNullOrWhiteSpace(step.Content)
                               && step.Content.Length >= 5)
                .Select(step => (step.Content ?? "").Trim())
                .ToList() ?? [];

            foreach (var req in c8Requirements)
            {
                if (string.IsNullOrWhiteSpace(req.Text)) continue;

                var intentName = NormalizeIntentName(req.Text);
                var key = $"{sector ?? "platform"}:{intentName}";

                if (!accumulator.TryGetValue(key, out var acc))
                {
                    acc = new IntentAccumulator
                    {
                        IntentName = intentName,
                        Sector = sector,
                        Scope = scope,
                        Niche = s.Niche ?? "",
                        Keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        SampleMessages = new List<string>(),
                        SourceScenarios = new List<string>()
                    };
                    accumulator[key] = acc;
                }

                // Merge keywords from requirement hint + user messages
                if (!string.IsNullOrWhiteSpace(req.Hint))
                {
                    foreach (var kw in ExtractKeywords(req.Hint))
                        acc.Keywords.Add(kw);
                }

                foreach (var msg in userMessages)
                {
                    if (!acc.SampleMessages.Contains(msg, StringComparer.OrdinalIgnoreCase) && acc.SampleMessages.Count < 10)
                        acc.SampleMessages.Add(msg);

                    foreach (var kw in ExtractKeywords(msg))
                        acc.Keywords.Add(kw);
                }

                var scenarioId = s.Id ?? "";
                if (!acc.SourceScenarios.Contains(scenarioId))
                    acc.SourceScenarios.Add(scenarioId);
            }
        }
    }

    private TemplateCreateRequest FinalizeIntentTemplate(IntentAccumulator acc)
    {
        var contentJson = new
        {
            intent_name = acc.IntentName,
            keywords = acc.Keywords.Take(20).ToArray(),
            sample_messages = acc.SampleMessages.Take(10).ToArray(),
            source_scenarios = acc.SourceScenarios.ToArray()
        };

        return new TemplateCreateRequest
        {
            TemplateType = "intent",
            Scope = acc.Scope,
            Sector = acc.Sector,
            Slug = BuildSlug(acc.Sector, "intent", acc.IntentName),
            Name = acc.IntentName,
            Description = $"Intent: {acc.IntentName} — {acc.SampleMessages.Count} sample, {acc.Keywords.Count} keyword",
            Lang = "tr",
            Tags = BuildTags("se-intent", acc.Niche, "c8"),
            ContentJson = contentJson,
            CreatedBy = "migration"
        };
    }

    // ═══════════════════════════════════════════════════════════
    // ── Helper Methods (instance, not static — logger access) ──
    // ═══════════════════════════════════════════════════════════

    private (string? Sector, string Scope)? MapNiche(string? niche)
    {
        if (string.IsNullOrWhiteSpace(niche)) return null;
        return NicheMap.TryGetValue(niche, out var mapping) ? mapping : null;
    }

    private bool IsValidFaqPair(SeStep userStep, SeStep botStep)
    {
        if (!string.Equals(userStep.Role, "user", StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(botStep.Role, "bot", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrWhiteSpace(userStep.Content) || userStep.Content.Length < 5) return false;
        if (string.IsNullOrWhiteSpace(botStep.Content) || botStep.Content.Length < 30) return false;
        if (botStep.Content.StartsWith("Otomatik cozum:", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private string[] ExtractKeywords(string text)
    {
        var words = text
            .Split([' ', ',', '.', '?', '!', ':', ';', '\n', '\r', '\t', '(', ')', '[', ']', '{', '}', '"', '\''],
                   StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length >= 2 && !Stopwords.Contains(w))
            .Select(w => NormalizeTurkish(w.ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .ToArray();

        return words;
    }

    private string NormalizeIntentName(string text)
    {
        // "Kargo intent tespiti" → "kargo_tespiti"
        // "Randevu intent tespiti" → "randevu_tespiti"
        var cleaned = text
            .Replace("intent", "", StringComparison.OrdinalIgnoreCase)
            .Replace("tespiti", "", StringComparison.OrdinalIgnoreCase)
            .Replace("tespit", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = text;

        var normalized = NormalizeTurkish(cleaned.ToLowerInvariant());
        var parts = normalized.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join("_", parts);
    }

    private string[] ExtractCapabilities(SeScenario s)
    {
        var caps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (s.Flows == null) return [];

        foreach (var flow in s.Flows)
        {
            if (flow.Requirements?.Client != null)
                foreach (var r in flow.Requirements.Client)
                    if (!string.IsNullOrWhiteSpace(r.Capability))
                        caps.Add(r.Capability);

            if (flow.Requirements?.Provider != null)
                foreach (var r in flow.Requirements.Provider)
                    if (!string.IsNullOrWhiteSpace(r.Capability))
                        caps.Add(r.Capability);
        }

        return [.. caps.OrderBy(c => c)];
    }

    private string BuildSlug(string? sector, string type, string name)
    {
        var prefix = sector ?? "platform";
        var normalizedName = NormalizeSlug(name);
        var slug = $"{prefix}.{type}.{normalizedName}";
        return Truncate(slug, 190);
    }

    private string NormalizeSlug(string input)
    {
        var lower = NormalizeTurkish(input.ToLowerInvariant());
        var sb = new StringBuilder(lower.Length);

        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else
                sb.Append('-');
        }

        // Collapse consecutive hyphens
        var result = sb.ToString();
        while (result.Contains("--"))
            result = result.Replace("--", "-");

        return result.Trim('-');
    }

    private string NormalizeTurkish(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            sb.Append(c switch
            {
                'ı' => 'i',
                'İ' => 'i',
                'ğ' => 'g',
                'Ğ' => 'g',
                'ü' => 'u',
                'Ü' => 'u',
                'ş' => 's',
                'Ş' => 's',
                'ö' => 'o',
                'Ö' => 'o',
                'ç' => 'c',
                'Ç' => 'c',
                _ => c
            });
        }
        return sb.ToString();
    }

    private static string[] BuildTags(string prefix, string niche, string? extra)
    {
        var tags = new List<string> { prefix, niche.ToLowerInvariant() };
        if (!string.IsNullOrWhiteSpace(extra))
            tags.Add(extra.ToLowerInvariant().Replace(" ", "-"));
        return tags.ToArray();
    }

    private static string Truncate(string input, int maxLength)
        => input.Length <= maxLength ? input : input[..maxLength];

    private static void IncrementSector(Dictionary<string, int> dict, string key)
    {
        if (!dict.TryAdd(key, 1))
            dict[key]++;
    }

    // ═══════════════════════════════════════════════════════════
    // ── Private Record Types (SE JSON deserialization) ─────────
    // ═══════════════════════════════════════════════════════════

    private sealed class SeScenario
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("subtitle")]
        public string? Subtitle { get; set; }

        [JsonPropertyName("phase")]
        public string? Phase { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("niche")]
        public string? Niche { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("flows")]
        public List<SeFlow>? Flows { get; set; }

        [JsonPropertyName("impact")]
        public SeImpact? Impact { get; set; }
    }

    private sealed class SeFlow
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("requirements")]
        public SeRequirements? Requirements { get; set; }

        [JsonPropertyName("steps")]
        public List<SeStep>? Steps { get; set; }
    }

    private sealed class SeRequirements
    {
        [JsonPropertyName("client")]
        public List<SeRequirement>? Client { get; set; }

        [JsonPropertyName("provider")]
        public List<SeRequirement>? Provider { get; set; }
    }

    private sealed class SeRequirement
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("service")]
        public string? Service { get; set; }

        [JsonPropertyName("capability")]
        public string? Capability { get; set; }

        [JsonPropertyName("hint")]
        public string? Hint { get; set; }
    }

    private sealed class SeStep
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class SeImpact
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("formula")]
        public string? Formula { get; set; }
    }

    /// <summary>
    /// Accumulator for cross-scenario intent deduplication within same sector.
    /// </summary>
    private sealed class IntentAccumulator
    {
        public string IntentName { get; set; } = "";
        public string? Sector { get; set; }
        public string Scope { get; set; } = "sector";
        public string Niche { get; set; } = "";
        public HashSet<string> Keywords { get; set; } = new();
        public List<string> SampleMessages { get; set; } = new();
        public List<string> SourceScenarios { get; set; } = new();
    }
}
