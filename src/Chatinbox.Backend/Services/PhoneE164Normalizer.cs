using PhoneNumbers;

namespace Chatinbox.Backend.Services;

/// <summary>
/// FEAT-LIW: Parse + validate a landing-form phone string and emit E.164
/// (+CCnnnnnnn, no spaces/hyphens/parens). Wraps libphonenumber-csharp so the
/// LeadIntakeService never touches the third-party API directly.
/// Thread-safe singleton; <see cref="PhoneNumberUtil"/> is immutable after init.
/// </summary>
public sealed class PhoneE164Normalizer
{
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    // Default candidate regions when the tenant map omits phone.country_hint. IE first
    // (Dent Adavista pilot = Ireland landing pages), TR second (bulk of Chatinbox base).
    private static readonly string[] DefaultRegions = { "IE", "TR" };

    /// <summary>
    /// Returns the E.164 form (e.g. "+353871234567") on success, or null when the
    /// number cannot be parsed/validated under the supplied hint or the fallback
    /// region list. Callers translate null to INV-BE-104.
    /// </summary>
    public string? Normalize(string? raw, string? countryHint)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // 1. Country hint path: trust the tenant's field map entry first.
        if (!string.IsNullOrWhiteSpace(countryHint))
        {
            var e164 = TryParseRegion(raw, countryHint.Trim().ToUpperInvariant());
            if (e164 != null) return e164;
        }

        // 2. If the value already looks international (+CC...) libphonenumber parses
        //    without a region; keep this ahead of the default region loop so global
        //    numbers don't get mis-classified by the first default.
        if (raw.TrimStart().StartsWith("+"))
        {
            var e164 = TryParseRegion(raw, region: null);
            if (e164 != null) return e164;
        }

        // 3. Fall back to the default region list (IE, TR).
        foreach (var region in DefaultRegions)
        {
            var e164 = TryParseRegion(raw, region);
            if (e164 != null) return e164;
        }

        return null;
    }

    private static string? TryParseRegion(string raw, string? region)
    {
        try
        {
            var parsed = Util.Parse(raw, region);
            if (!Util.IsValidNumber(parsed)) return null;
            return Util.Format(parsed, PhoneNumberFormat.E164);
        }
        catch (NumberParseException)
        {
            return null;
        }
    }
}
