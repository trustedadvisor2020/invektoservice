using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Automation.Services;

/// <summary>
/// HFM-2: loads and caches per-locale AiIntent prompt resources from embedded JSON.
///
/// Resource lookup order:
///   1. Chatinbox.Automation.Resources.IntentPrompts.{locale}.json
///   2. Chatinbox.Automation.Resources.IntentPrompts.tr.json (pre-HFM-2 default)
///
/// Missing resource or malformed JSON logs INV-AT-065 and falls back to 'tr'.
/// Locale failure never throws — caller always receives a valid (or empty) string.
///
/// Thread-safe, register as singleton.
/// </summary>
public sealed class IntentPromptLoader
{
    private const string DefaultLocale = "tr";
    private const string ResourcePrefix = "Chatinbox.Automation.Resources.IntentPrompts.";

    private readonly Assembly _assembly;
    private readonly JsonLinesLogger _logger;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _cache = new();

    public IntentPromptLoader(JsonLinesLogger logger)
        : this(typeof(IntentPromptLoader).Assembly, logger) { }

    /// <summary>Test-friendly constructor; callers can supply a different assembly for resource lookup.</summary>
    public IntentPromptLoader(Assembly assembly, JsonLinesLogger logger)
    {
        _assembly = assembly;
        _logger = logger;
    }

    /// <summary>
    /// Get a prompt by locale + key with {placeholder} substitutions applied.
    /// Unknown locale → falls back to 'tr'. Unknown key → returns empty string (logged).
    /// </summary>
    public string Get(string? locale, string key, IReadOnlyDictionary<string, string>? substitutions = null)
    {
        var resolvedLocale = NormalizeLocale(locale);
        var dict = _cache.GetOrAdd(resolvedLocale, Load);

        if (dict.Count == 0 && resolvedLocale != DefaultLocale)
        {
            // Requested locale empty → try default before giving up
            dict = _cache.GetOrAdd(DefaultLocale, Load);
        }

        if (!dict.TryGetValue(key, out var template) || string.IsNullOrEmpty(template))
            return "";

        return ApplySubstitutions(template, substitutions);
    }

    private static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return DefaultLocale;

        // Strip region ("en-US" → "en"), lowercase.
        var normalized = locale.Trim().ToLowerInvariant();
        var dashIdx = normalized.IndexOf('-');
        if (dashIdx > 0)
            normalized = normalized[..dashIdx];

        return normalized.Length == 2 ? normalized : DefaultLocale;
    }

    private IReadOnlyDictionary<string, string> Load(string locale)
    {
        var resourceName = ResourcePrefix + locale + ".json";
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationIntentPromptResourceMissing}] IntentPrompts resource not found: {resourceName}");
            return new Dictionary<string, string>(0);
        }

        try
        {
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                _logger.SystemWarn($"[{ErrorCodes.AutomationIntentPromptResourceMissing}] IntentPrompts resource root not an object: {resourceName}");
                return new Dictionary<string, string>(0);
            }

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    map[prop.Name] = prop.Value.GetString() ?? "";
            }

            return map;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationIntentPromptResourceMissing}] IntentPrompts JSON parse failed for {resourceName}: {ex.Message}");
            return new Dictionary<string, string>(0);
        }
    }

    private static string ApplySubstitutions(string template, IReadOnlyDictionary<string, string>? substitutions)
    {
        if (substitutions == null || substitutions.Count == 0 || !template.Contains('{'))
            return template;

        // Lightweight {key} substitution — no brace escaping needed for our prompt set.
        var result = template;
        foreach (var (key, value) in substitutions)
            result = result.Replace("{" + key + "}", value ?? "", StringComparison.Ordinal);
        return result;
    }
}
