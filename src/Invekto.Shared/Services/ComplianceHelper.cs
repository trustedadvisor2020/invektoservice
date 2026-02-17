using System.Text.RegularExpressions;

namespace Invekto.Shared.Services;

/// <summary>
/// Compliance utilities: data masking, retention config (GR-3.29).
/// Builds on KvkkHelper (GR-2.6) — this adds display masking and retention logic.
/// </summary>
public static partial class ComplianceHelper
{
    // TC Kimlik: 11 digits -> show first 3 + ******* + last 1
    // Phone: +90XXXXXXXXXX -> +90XXX***XXXX
    private static readonly Regex TcKimlikPattern = TcKimlikRegex();
    private static readonly Regex PhonePattern = PhoneRegex();

    /// <summary>
    /// Mask TC Kimlik number for display: 12345678901 -> 123*******1
    /// </summary>
    public static string MaskTcKimlik(string tcKimlik)
    {
        if (string.IsNullOrWhiteSpace(tcKimlik) || tcKimlik.Length != 11)
            return tcKimlik;

        if (!TcKimlikPattern.IsMatch(tcKimlik))
            return tcKimlik;

        return string.Concat(tcKimlik.AsSpan(0, 3), "*******", tcKimlik.AsSpan(10, 1));
    }

    /// <summary>
    /// Mask phone number for display: +905321234567 -> +90532***4567
    /// Handles formats: +90XXXXXXXXXX, 0XXXXXXXXXX, 5XXXXXXXXXX
    /// </summary>
    public static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return phone;

        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.Length < 10)
            return phone;

        // Normalize to last 10 digits
        var last10 = digits.Length >= 10 ? digits[^10..] : digits;
        var prefix = phone.StartsWith('+') ? phone[..^last10.Length] : "";

        return $"{prefix}{last10[..3]}***{last10[6..]}";
    }

    /// <summary>
    /// Mask any detected TC or phone patterns in a text string for display/logs.
    /// </summary>
    public static string MaskSensitiveData(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Mask TC kimlik (11 consecutive digits)
        var result = TcKimlikPattern.Replace(text, m => MaskTcKimlik(m.Value));

        // Mask phone numbers
        result = PhonePattern.Replace(result, m => MaskPhone(m.Value));

        return result;
    }

    /// <summary>
    /// Get data retention days for a tenant based on settings.
    /// Health tenants default to 3650 days (10 years), commercial defaults to 365 days.
    /// </summary>
    public static int GetRetentionDays(string? settingsJson, bool isHealthTenant)
    {
        if (!string.IsNullOrEmpty(settingsJson))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(settingsJson);
                if (doc.RootElement.TryGetProperty("compliance", out var compliance) &&
                    compliance.TryGetProperty("data_retention_days", out var retentionProp) &&
                    retentionProp.TryGetInt32(out var customDays))
                {
                    return customDays;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "ComplianceHelper.GetRetentionDays: malformed settings_json, using defaults");
            }
        }

        return isHealthTenant ? 3650 : 365;
    }

    [GeneratedRegex(@"\b\d{11}\b", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex TcKimlikRegex();

    [GeneratedRegex(@"(\+90|0)?5\d{9}", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex PhoneRegex();
}
