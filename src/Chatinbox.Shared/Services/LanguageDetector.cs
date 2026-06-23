namespace Chatinbox.Shared.Services;

/// <summary>
/// GR-2.3: Lightweight language detection using Unicode script + character analysis.
/// Used by Outbound for pre-Claude language hints. AgentAI uses Claude inline detection.
/// Supports: Arabic (ar), Russian (ru), Korean (ko), Japanese (ja), Chinese (zh),
/// Thai (th), Hindi (hi), Georgian (ka), Armenian (hy), Greek (el),
/// Hebrew (he), Turkish (tr), English (en) fallback.
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
    /// Detect language from text using Unicode script + character frequency analysis.
    /// Returns ISO 639-1 code (e.g., "ar", "tr", "en").
    /// </summary>
    public static string Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "tr"; // default

        int turkishCharCount = 0;
        int arabicCharCount = 0;
        int cyrillicCharCount = 0;
        int hangulCharCount = 0;
        int cjkCharCount = 0;
        int hiraganaKatakanaCount = 0;
        int thaiCharCount = 0;
        int devanagariCharCount = 0;
        int georgianCharCount = 0;
        int armenianCharCount = 0;
        int greekCharCount = 0;
        int hebrewCharCount = 0;
        int letterCount = 0;

        foreach (char c in text)
        {
            if (char.IsLetter(c) || IsNonLatinScript(c))
            {
                letterCount++;

                if (TurkishSpecificChars.Contains(c))
                    turkishCharCount++;
                else if (c >= '\u0600' && c <= '\u06FF' || c >= '\u0750' && c <= '\u077F' || c >= '\uFB50' && c <= '\uFDFF' || c >= '\uFE70' && c <= '\uFEFF')
                    arabicCharCount++;
                else if (c >= '\u0400' && c <= '\u04FF')
                    cyrillicCharCount++;
                else if (c >= '\uAC00' && c <= '\uD7AF' || c >= '\u1100' && c <= '\u11FF')
                    hangulCharCount++;
                else if (c >= '\u4E00' && c <= '\u9FFF' || c >= '\u3400' && c <= '\u4DBF')
                    cjkCharCount++;
                else if (c >= '\u3040' && c <= '\u309F' || c >= '\u30A0' && c <= '\u30FF')
                    hiraganaKatakanaCount++;
                else if (c >= '\u0E00' && c <= '\u0E7F')
                    thaiCharCount++;
                else if (c >= '\u0900' && c <= '\u097F')
                    devanagariCharCount++;
                else if (c >= '\u10A0' && c <= '\u10FF')
                    georgianCharCount++;
                else if (c >= '\u0530' && c <= '\u058F')
                    armenianCharCount++;
                else if (c >= '\u0370' && c <= '\u03FF')
                    greekCharCount++;
                else if (c >= '\u0590' && c <= '\u05FF')
                    hebrewCharCount++;
            }
        }

        if (letterCount == 0)
            return "tr"; // default for non-letter content

        // Script-based detection: if dominant script is non-Latin, return that language
        var scripts = new (int count, string lang)[]
        {
            (arabicCharCount, "ar"),
            (cyrillicCharCount, "ru"),
            (hangulCharCount, "ko"),
            (cjkCharCount, "zh"),
            (hiraganaKatakanaCount, "ja"),
            (thaiCharCount, "th"),
            (devanagariCharCount, "hi"),
            (georgianCharCount, "ka"),
            (armenianCharCount, "hy"),
            (greekCharCount, "el"),
            (hebrewCharCount, "he"),
        };

        var dominant = scripts.OrderByDescending(s => s.count).First();
        if (dominant.count > 0 && (double)dominant.count / letterCount > 0.3)
            return dominant.lang;

        // CJK disambiguation: if both CJK and hiragana/katakana present → Japanese
        if (cjkCharCount > 0 && hiraganaKatakanaCount > 0)
            return "ja";

        // Turkish-specific character check
        if (turkishCharCount > 0 && (double)turkishCharCount / letterCount > 0.02)
            return "tr";

        return "en"; // Latin-script fallback
    }

    private static bool IsNonLatinScript(char c) =>
        c >= '\u0600' && c <= '\u06FF' ||  // Arabic
        c >= '\u0750' && c <= '\u077F' ||  // Arabic Supplement
        c >= '\uFB50' && c <= '\uFDFF' ||  // Arabic Presentation Forms-A
        c >= '\uFE70' && c <= '\uFEFF' ||  // Arabic Presentation Forms-B
        c >= '\u0590' && c <= '\u05FF' ||  // Hebrew
        c >= '\u0E00' && c <= '\u0E7F' ||  // Thai
        c >= '\u0900' && c <= '\u097F';    // Devanagari

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
