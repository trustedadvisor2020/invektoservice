using System.Text.RegularExpressions;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Variable substitution and condition evaluation for v2 flow engine.
/// Safety limits: regex 100ms timeout, max 50 variables, max 10KB per value, flat only.
/// IMP-3: Expression Safety.
/// </summary>
public sealed class ExpressionEvaluator
{
    private static readonly Regex VariablePattern = new(
        @"\{\{(\w+)\}\}",
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
    /// Missing variables become empty string. Errors return original template with warning log.
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
                if (variables.TryGetValue(varName, out var value))
                {
                    // Safety: truncate oversized values
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
