using System.Text.RegularExpressions;

namespace Invekto.Shared.Services;

/// <summary>
/// Shared {{variable}} substitution utility.
/// Used by Outbound (broadcast templates) and AgentAI (reply templates).
/// Thread-safe static methods.
/// </summary>
public static class TemplateSubstitution
{
    private static readonly Regex VariablePattern = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    /// <summary>
    /// Substitute {{variable}} placeholders in template text.
    /// Returns (result, missingVars). Missing variables are left as-is.
    /// </summary>
    public static (string result, List<string> missingVars) Substitute(
        string template, Dictionary<string, string>? variables)
    {
        var missingVars = new List<string>();

        if (string.IsNullOrEmpty(template))
            return (template, missingVars);

        var result = VariablePattern.Replace(template, match =>
        {
            var varName = match.Groups[1].Value;
            if (variables != null && variables.TryGetValue(varName, out var value))
                return value;

            missingVars.Add(varName);
            return match.Value;
        });

        return (result, missingVars);
    }

    /// <summary>
    /// Extract all variable names from a template string.
    /// </summary>
    public static List<string> ExtractVariables(string template)
    {
        if (string.IsNullOrEmpty(template))
            return new List<string>();

        return VariablePattern.Matches(template)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }
}
