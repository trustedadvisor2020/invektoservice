using System.Text.RegularExpressions;
using Invekto.Shared.Logging;

namespace Invekto.AgentAI.Services;

public sealed partial class TemplateEngine
{
    private readonly JsonLinesLogger _logger;

    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.Compiled)]
    private static partial Regex VariablePattern();

    public TemplateEngine(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Replaces {{variable}} placeholders in text with provided values.
    /// Missing variables are left as-is (not removed, not crashed).
    /// </summary>
    public string Substitute(string text, Dictionary<string, string>? variables)
    {
        if (string.IsNullOrEmpty(text) || variables == null || variables.Count == 0)
            return text;

        return VariablePattern().Replace(text, match =>
        {
            var varName = match.Groups[1].Value;
            if (variables.TryGetValue(varName, out var value))
                return SanitizeValue(value) ?? match.Value;

            // Missing variable -- leave placeholder, log warning
            _logger.SystemWarn($"Template variable '{{{{{varName}}}}}' not found in provided variables");
            return match.Value;
        });
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
