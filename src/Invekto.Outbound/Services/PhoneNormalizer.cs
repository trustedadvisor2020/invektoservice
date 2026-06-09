using System.Text;
using System.Text.RegularExpressions;

namespace Invekto.Outbound.Services;

/// <summary>
/// Lenient E.164 phone normalizer for bulk recipient lists.
///
/// INTL-SAFE by design: dent/hair pilot clinics message foreign patients, so we
/// must NOT hard-drop non-TR numbers (Codex §4B). Strategy:
///   * scientific-notation artifact ("9.05332E+11") -> rejected (Excel display
///     text whose digits are already lost; never a real phone)
///   * strip all non-digits (keep a leading '+')
///   * 00-prefix  -> international, drop the 00
///   * 0XXXXXXXXXX (11 digits, TR domestic) -> +90 + last 10
///   * 5XXXXXXXXX (10 digits, bare TR mobile) -> +90 + 10
///   * 90XXXXXXXXXX (12 digits) -> already TR country code
///   * anything else with a country code (leading + or >=11 digits) -> keep as-is
/// Final guard: 8..15 digits total (E.164 max 15). Output is "+" + digits.
/// Returns null for unrecoverable input so the caller counts it as invalid.
/// Thread-safe, stateless — register as singleton.
/// </summary>
public sealed class PhoneNormalizer
{
    // Excel renders 12+ digit numbers as scientific notation; if such display text
    // reaches us as the "phone", digit-stripping would turn "9.05332E+11" into a
    // wrong-but-plausible 8-digit number that PASSES the 8..15 guard and gets stored
    // as sendable. Reject before stripping so the row surfaces as invalid instead.
    // Must stay mechanically identical to the SPA's SCI_NOTATION_RE (DataImportPage).
    private static readonly Regex ScientificNotationRegex =
        new(@"\d[eE][+-]?\d", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        raw = raw.Trim();
        if (ScientificNotationRegex.IsMatch(raw)) return null;
        var hadPlus = raw.StartsWith('+');

        // Keep digits only
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
            if (c >= '0' && c <= '9') sb.Append(c);
        var digits = sb.ToString();

        if (digits.Length == 0) return null;

        // 00-international prefix -> strip to bare country+number
        if (!hadPlus && digits.StartsWith("00"))
            digits = digits[2..];

        if (digits.Length == 0) return null;

        if (hadPlus)
        {
            // Caller already gave a country code; trust the digits.
            return Guard(digits);
        }

        // TR heuristics for plus-less domestic input
        if (digits.Length == 12 && digits.StartsWith("90"))
            return Guard(digits);                          // 90 5XX XXX XX XX
        if (digits.Length == 11 && digits.StartsWith('0'))
            return Guard("90" + digits[1..]);              // 0 5XX ... -> 90 5XX ...
        if (digits.Length == 10 && digits.StartsWith('5'))
            return Guard("90" + digits);                   // bare TR mobile

        // Otherwise assume the digits already carry a country code
        // (e.g. 4477..., 19175...). Keep as international.
        return Guard(digits);
    }

    private static string? Guard(string digits)
    {
        // E.164: max 15 digits; require a plausible minimum.
        if (digits.Length < 8 || digits.Length > 15) return null;
        return "+" + digits;
    }
}
