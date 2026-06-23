using System.Text.Json;
using System.Text.RegularExpressions;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Automation.Services;

/// <summary>
/// Variable substitution and condition evaluation for v2 flow engine.
/// Safety limits: regex 100ms timeout, max 50 variables, max 10KB per value.
/// Supports dot-path JSON traversal: {{var.prop.0.nested}} resolves into stored JSON.
/// IMP-3: Expression Safety.
/// </summary>
public sealed class ExpressionEvaluator
{
    // Supports dot-path: {{var}}, {{var.prop}}, {{var.prop.0.nested}}
    private static readonly Regex VariablePattern = new(
        @"\{\{([\w.]+)\}\}",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private const int MaxVariableCount = 50;
    private const int MaxValueBytes = 10_240; // 10KB

    private readonly JsonLinesLogger _logger;

    public ExpressionEvaluator(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Replace {{variable}} placeholders in a template string with session variable values.
    /// Supports dot-path: {{api_response.results.0.name}} navigates into JSON stored in variables.
    /// Missing variables/paths become empty string. Errors return original template with warning log.
    /// </summary>
    public string Substitute(string template, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        try
        {
            return VariablePattern.Replace(template, match =>
            {
                var varName = match.Groups[1].Value;

                // Dot-path: resolve into JSON
                if (varName.Contains('.'))
                    return ResolveJsonPath(varName, variables);

                // Flat variable lookup
                if (variables.TryGetValue(varName, out var value))
                {
                    if (value.Length > MaxValueBytes)
                    {
                        _logger.SystemWarn($"Variable '{varName}' exceeds {MaxValueBytes}B limit, truncated");
                        return value[..MaxValueBytes];
                    }
                    return value;
                }
                return ""; // Missing variable -> empty string
            });
        }
        catch (RegexMatchTimeoutException)
        {
            _logger.SystemWarn($"Expression substitution timeout (100ms) on template length={template.Length}");
            return template; // Graceful fallback
        }
    }

    /// <summary>
    /// Resolve a dot-path like "geo_data.results.0.latitude" by:
    /// 1. Looking up the root variable (geo_data) in session variables
    /// 2. Parsing as JSON
    /// 3. Navigating the path (results → [0] → latitude)
    /// Returns empty string on any failure (missing var, invalid JSON, path not found).
    /// </summary>
    private string ResolveJsonPath(string path, IReadOnlyDictionary<string, string> variables)
    {
        var segments = path.Split('.');
        var rootVar = segments[0];

        if (!variables.TryGetValue(rootVar, out var jsonValue) || string.IsNullOrEmpty(jsonValue))
            return "";

        try
        {
            using var doc = JsonDocument.Parse(jsonValue);
            var current = doc.RootElement;

            for (var i = 1; i < segments.Length; i++)
            {
                var segment = segments[i];

                if (current.ValueKind == JsonValueKind.Object &&
                    current.TryGetProperty(segment, out var next))
                {
                    current = next;
                }
                else if (current.ValueKind == JsonValueKind.Array &&
                         int.TryParse(segment, out var index) &&
                         index >= 0 && index < current.GetArrayLength())
                {
                    current = current[index];
                }
                else
                {
                    return ""; // Path segment not found
                }
            }

            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString() ?? "",
                JsonValueKind.Number => current.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "",
                _ => current.GetRawText() // Object/Array → raw JSON
            };
        }
        catch (JsonException)
        {
            return ""; // Stored value is not valid JSON
        }
    }

    /// <summary>
    /// Evaluate a condition: variable {operator} value.
    /// Returns true/false. Errors return false with warning log.
    /// </summary>
    public bool EvaluateCondition(
        string variableName, string operatorType, string compareValue,
        IReadOnlyDictionary<string, string> variables)
    {
        var actualValue = variables.TryGetValue(variableName, out var v) ? v : "";

        try
        {
            return operatorType switch
            {
                "equals" => string.Equals(actualValue, compareValue, StringComparison.OrdinalIgnoreCase),
                "contains" => actualValue.Contains(compareValue, StringComparison.OrdinalIgnoreCase),
                "starts_with" => actualValue.StartsWith(compareValue, StringComparison.OrdinalIgnoreCase),
                "greater_than" => double.TryParse(actualValue, out var a) && double.TryParse(compareValue, out var b) && a > b,
                "less_than" => double.TryParse(actualValue, out var x) && double.TryParse(compareValue, out var y) && x < y,
                "is_empty" => string.IsNullOrWhiteSpace(actualValue),
                "regex" => EvaluateRegex(actualValue, compareValue),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"Condition evaluation error: var={variableName}, op={operatorType}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validate that session variables don't exceed safety limits.
    /// Returns true if safe, false if limits exceeded.
    /// </summary>
    public bool ValidateVariables(IReadOnlyDictionary<string, string> variables)
    {
        if (variables.Count > MaxVariableCount)
        {
            _logger.SystemWarn($"Variable count {variables.Count} exceeds limit {MaxVariableCount}");
            return false;
        }

        foreach (var (key, value) in variables)
        {
            if (value.Length > MaxValueBytes)
            {
                _logger.SystemWarn($"Variable '{key}' value size {value.Length}B exceeds limit {MaxValueBytes}B");
                return false;
            }
        }

        return true;
    }

    private bool EvaluateRegex(string input, string pattern)
    {
        try
        {
            var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            return regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            _logger.SystemWarn($"Regex condition timeout (100ms) on pattern length={pattern.Length}");
            return false;
        }
        catch (ArgumentException ex)
        {
            _logger.SystemWarn($"Invalid regex pattern: {ex.Message}");
            return false;
        }
    }
}
