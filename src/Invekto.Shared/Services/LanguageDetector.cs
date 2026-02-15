namespace Invekto.Shared.Services;

/// <summary>
/// GR-2.3: Lightweight language detection using character analysis.
/// Used by Outbound for pre-Claude language hints. AgentAI uses Claude inline detection.
/// Supports: Turkish (tr), English (en) -- more languages can be added.
/// </summary>
public static class LanguageDetector
{
    private static readonly HashSet<char> TurkishSpecificChars = new()
    {
        '\u00e7', '\u00c7', // ç, Ç
        '\u011f', '\u011e', // ğ, Ğ
        '\u0131', '\u0130', // ı, İ
        '\u00f6', '\u00d6', // ö, Ö
        '\u015f', '\u015e', // ş, Ş
        '\u00fc', '\u00dc'  // ü, Ü
    };

    /// <summary>
    /// Detect language from text using character frequency analysis.
    /// Returns ISO 639-1 code (e.g., "tr", "en").
    /// </summary>
    public static string Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "tr"; // default

        int turkishCharCount = 0;
        int letterCount = 0;

        foreach (char c in text)
        {
            if (char.IsLetter(c))
            {
                letterCount++;
                if (TurkishSpecificChars.Contains(c))
                    turkishCharCount++;
            }
        }

        if (letterCount == 0)
            return "tr"; // default for non-letter content

        // If >2% of letters are Turkish-specific characters, it's likely Turkish
        double turkishRatio = (double)turkishCharCount / letterCount;
        return turkishRatio > 0.02 ? "tr" : "en";
    }

    /// <summary>
    /// Validate and normalize a language code.
    /// Returns null if invalid.
    /// </summary>
    public static string? Normalize(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
            return null;

        string normalized = lang.Trim().ToLowerInvariant();
        return normalized switch
        {
            "tr" or "tur" => "tr",
            "en" or "eng" => "en",
            "de" or "deu" => "de",
            "ar" or "ara" => "ar",
            "ru" or "rus" => "ru",
            "fr" or "fra" => "fr",
            _ => normalized.Length == 2 ? normalized : null
        };
    }
}
