using Invekto.Shared.Logging;
using Invekto.Shared.Services;

namespace Invekto.Outbound.Services;

/// <summary>
/// Outbound-specific template engine wrapper.
/// Delegates core {{variable}} substitution to shared TemplateSubstitution.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class TemplateEngine
{
    private readonly JsonLinesLogger _logger;

    public TemplateEngine(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Apply variables to a template string. Returns (result, missingVars).
    /// If any variables are missing, the message is NOT sent (missingVars.Count > 0).
    /// </summary>
    public (string result, List<string> missingVars) Substitute(
        string template, Dictionary<string, string>? variables)
    {
        return TemplateSubstitution.Substitute(template, variables);
    }

    /// <summary>
    /// Extract all variable names from a template string.
    /// </summary>
    public static List<string> ExtractVariables(string template)
    {
        return TemplateSubstitution.ExtractVariables(template);
    }
}
