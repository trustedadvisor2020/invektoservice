using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Invekto.WhatsAppAnalytics.Services;

/// <summary>
/// Turkish text normalization utilities.
/// C# port of Python normalizer.py + deduplicator.py.
/// </summary>
public sealed class TextNormalizer
{
    // Zero-width characters to remove
    private static readonly Regex ZeroWidthRegex = new(@"[\u200b\u200c\u200d\ufeff]", RegexOptions.Compiled);

    // Collapse multiple whitespace
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

    // Strip surrounding quotes
    private static readonly Regex SurroundingQuotesRegex = new(@"^[""']+|[""']+$", RegexOptions.Compiled);

    // Extract digits only (for phone normalization)
    private static readonly Regex NonDigitRegex = new(@"[^\d]", RegexOptions.Compiled);

    // Remove punctuation (for comparison)
    private static readonly Regex PunctuationRegex = new(@"[^\w\s]", RegexOptions.Compiled);

    // Turkish character mapping for comparison (uppercase -> lowercase ASCII)
    private static readonly Dictionary<char, char> TurkishCharMap = new()
    {
        { '\u0130', 'i' }, // İ -> i
        { '\u015E', 's' }, // Ş -> s
        { '\u011E', 'g' }, // Ğ -> g
        { '\u00DC', 'u' }, // Ü -> u
        { '\u00D6', 'o' }, // Ö -> o
        { '\u00C7', 'c' }, // Ç -> c
        { '\u0131', 'i' }, // ı -> i
        { '\u015F', 's' }, // ş -> s
        { '\u011F', 'g' }, // ğ -> g
        { '\u00FC', 'u' }, // ü -> u
        { '\u00F6', 'o' }, // ö -> o
        { '\u00E7', 'c' }, // ç -> c
    };

    /// <summary>
    /// Normalize text: Unicode NFC + strip quotes + remove zero-width + collapse spaces.
    /// Port of Python normalize_text().
    /// </summary>
    public string NormalizeText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Unicode NFC normalization
        var normalized = text.Normalize(NormalizationForm.FormC);

        // Strip surrounding quotes
        normalized = SurroundingQuotesRegex.Replace(normalized, "");

        // Remove zero-width characters
        normalized = ZeroWidthRegex.Replace(normalized, "");

        // Collapse multiple spaces
        normalized = MultiSpaceRegex.Replace(normalized, " ");

        return normalized.Trim();
    }

    /// <summary>
    /// Normalize agent name: strip + collapse spaces.
    /// </summary>
    public string NormalizeAgentName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        return MultiSpaceRegex.Replace(name.Trim(), " ");
    }

    /// <summary>
    /// Normalize Turkish phone number: extract digits, add 90 prefix if needed.
    /// Port of Python normalize_phone().
    /// </summary>
    public string NormalizePhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone)) return "";

        var digits = NonDigitRegex.Replace(phone, "");

        // 10 digits starting with 5 -> add "90" prefix
        if (digits.Length == 10 && digits.StartsWith('5'))
            return "90" + digits;

        // 11 digits starting with "05" -> add "9" prefix
        if (digits.Length == 11 && digits.StartsWith("05"))
            return "9" + digits;

        return digits;
    }

    /// <summary>
    /// Transliterate Turkish special chars to ASCII equivalents (lowercase).
    /// Preserves punctuation and spaces. Used for regex matching robustness.
    /// </summary>
    public string TransliterateTurkish(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var lower = text.ToLowerInvariant();
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower)
        {
            sb.Append(TurkishCharMap.TryGetValue(ch, out var mapped) ? mapped : ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Clean text for hash comparison: lowercase + Turkish char map + remove punctuation.
    /// Port of Python clean_for_comparison().
    /// </summary>
    public string CleanForComparison(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var lower = text.ToLowerInvariant();

        // Apply Turkish character mapping
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower)
        {
            sb.Append(TurkishCharMap.TryGetValue(ch, out var mapped) ? mapped : ch);
        }

        // Remove punctuation
        var cleaned = PunctuationRegex.Replace(sb.ToString(), "");

        // Collapse spaces
        return MultiSpaceRegex.Replace(cleaned, " ").Trim();
    }

    /// <summary>
    /// Compute message hash for dedup: SHA256(conversation_id + cleaned_text)[:16].
    /// Port of Python compute_message_hash().
    /// </summary>
    public string ComputeMessageHash(string conversationId, string cleanedText)
    {
        var input = conversationId + cleanedText;
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }

    /// <summary>
    /// Try parse a timestamp from separate date + time strings.
    /// Expected formats: date="YYYY-MM-DD" or "DD.MM.YYYY", time="HH:MM:SS" or "HH:MM".
    /// </summary>
    public DateTime? TryParseTimestamp(string? date, string? time)
    {
        if (string.IsNullOrWhiteSpace(date)) return null;

        var dateStr = date.Trim();
        var timeStr = (time ?? "00:00:00").Trim();

        // Try combined
        var combined = $"{dateStr} {timeStr}";
        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "dd.MM.yyyy HH:mm:ss",
            "dd.MM.yyyy HH:mm",
            "M/d/yyyy HH:mm:ss",
            "M/d/yyyy HH:mm"
        };

        if (DateTime.TryParseExact(combined, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result;

        // Last resort: general parse
        if (DateTime.TryParse(combined, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return result;

        return null;
    }

    /// <summary>
    /// Normalize sender_type to uppercase: ME or CUSTOMER.
    /// </summary>
    public string NormalizeSenderType(string? senderType)
    {
        if (string.IsNullOrWhiteSpace(senderType)) return "";
        var upper = senderType.Trim().ToUpperInvariant();
        return upper == "ME" || upper == "CUSTOMER" ? upper : "";
    }
}
