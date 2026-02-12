using Invekto.Shared.Logging;
using Invekto.Shared.Services;

namespace Invekto.AgentAI.Services;

/// <summary>
/// AgentAI-specific template engine.
/// Delegates core {{variable}} substitution to shared TemplateSubstitution.
/// Adds HTML sanitization for agent-facing content safety.
/// </summary>
public sealed class TemplateEngine
{
    private readonly JsonLinesLogger _logger;

    public TemplateEngine(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Replaces {{variable}} placeholders in text with provided values.
    /// Values are HTML-sanitized. Missing variables are left as-is.
    /// </summary>
    public string Substitute(string text, Dictionary<string, string>? variables)
    {
        if (string.IsNullOrEmpty(text) || variables == null || variables.Count == 0)
            return text;

        // Pre-sanitize values for HTML safety before substitution
        var sanitized = new Dictionary<string, string>(variables.Count);
        foreach (var kvp in variables)
        {
            sanitized[kvp.Key] = SanitizeValue(kvp.Value) ?? kvp.Value;
        }

        var (result, missingVars) = TemplateSubstitution.Substitute(text, sanitized);

        foreach (var varName in missingVars)
            _logger.SystemWarn($"Template variable '{{{{{varName}}}}}' not found in provided variables");

        return result;
    }

    /// <summary>
    /// Finds the best matching template from available templates based on intent.
    /// Returns null if no templates provided or no match found.
    /// </summary>
    public string? FindBestTemplate(
        List<Invekto.Shared.DTOs.AgentAI.ReplyTemplate>? templates,
        string? intent,
        Dictionary<string, string>? variables)
    {
        if (templates == null || templates.Count == 0)
            return null;

        // Simple: return first template with variable substitution
        // Phase 2+: intent-based matching, scoring
        var template = templates.FirstOrDefault();
        if (template?.Text == null)
            return null;

        return Substitute(template.Text, variables);
    }

    /// <summary>
    /// Sanitize user-provided values to prevent injection (HTML/script tags).
    /// </summary>
    private static string? SanitizeValue(string? value)
    {
        if (value == null) return null;
        return value.Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
