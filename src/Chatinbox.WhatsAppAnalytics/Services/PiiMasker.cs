using System.Text.RegularExpressions;

namespace Chatinbox.WhatsAppAnalytics.Services;

/// <summary>
/// Regex-based PII redaction for WhatsApp conversation text before sending to LLMs.
/// Masks phone numbers, emails, TCKN, IBANs, and URLs.
/// Preserves prices, dates, medical info, and general locations (needed for outcome classification).
/// </summary>
public sealed class PiiMasker
{
    // Phone: international format or local 10-15 digits
    private static readonly Regex PhoneRegex = new(
        @"\+?\d[\d\s\-]{8,14}\d",
        RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    // Email
    private static readonly Regex EmailRegex = new(
        @"[\w.+\-]+@[\w\-]+\.[\w.]+",
        RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    // Turkish TCKN: 11 digits starting with non-zero (standalone word)
    private static readonly Regex TcknRegex = new(
        @"\b[1-9]\d{10}\b",
        RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    // IBAN: 2 letters + 2 digits + 12-30 alphanumeric
    private static readonly Regex IbanRegex = new(
        @"\b[A-Z]{2}\d{2}\s?[\dA-Z]{4}[\s\dA-Z]{8,26}\b",
        RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    // URLs (with or without scheme)
    private static readonly Regex UrlRegex = new(
        @"(?:https?://|www\.)\S+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Mask PII in text. Returns redacted version safe for LLM consumption.
    /// </summary>
    public string Mask(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Order matters: IBAN before phone (IBAN contains digits that phone regex might match)
        text = IbanRegex.Replace(text, "[IBAN]");
        text = TcknRegex.Replace(text, "[TCKN]");
        text = EmailRegex.Replace(text, "[EMAIL]");
        text = UrlRegex.Replace(text, "[LINK]");
        text = PhoneRegex.Replace(text, "[PHONE]");

        return text;
    }
}
