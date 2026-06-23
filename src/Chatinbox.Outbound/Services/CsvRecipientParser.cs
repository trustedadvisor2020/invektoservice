using Chatinbox.Shared.DTOs.Outbound;

namespace Chatinbox.Outbound.Services;

/// <summary>
/// Parses a bulk recipient list from raw CSV/pasted text into normalized,
/// deduplicated recipients. Column 1 is always the phone; columns 2..N map to
/// template variables via the supplied header names.
///
/// Codex §4B safety: dedup on normalized phone (first occurrence wins —
/// deterministic), invalid rows tracked as counts, no silent drops.
/// Stateless, thread-safe — register as singleton.
/// </summary>
public sealed class CsvRecipientParser
{
    private readonly PhoneNormalizer _normalizer;

    public CsvRecipientParser(PhoneNormalizer normalizer)
    {
        _normalizer = normalizer;
    }

    public sealed class ParseResult
    {
        public List<BroadcastRecipient> Valid { get; } = new();
        public int TotalInput { get; set; }
        public int Duplicate { get; set; }
        public int Invalid { get; set; }
        public List<string> InvalidSamples { get; } = new();
        /// <summary>True when raw input exceeded maxRows — caller rejects (no silent drop).</summary>
        public bool LimitExceeded { get; set; }
    }

    private const int MaxInvalidSamples = 20;

    /// <summary>
    /// Parse raw CSV text. Lines are split on \n; cells on comma OR semicolon OR tab.
    /// Rejects (LimitExceeded) if non-empty row count exceeds maxRows.
    /// </summary>
    public ParseResult ParseCsv(string csvText, IReadOnlyList<string>? variableColumns, int maxRows)
    {
        var lines = csvText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var rows = new List<string[]>(Math.Min(lines.Length, maxRows + 1));
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (rows.Count >= maxRows)
                return new ParseResult { TotalInput = rows.Count, LimitExceeded = true };
            rows.Add(line.Split(',', ';', '\t'));
        }
        return BuildFromRows(rows, variableColumns);
    }

    private ParseResult BuildFromRows(List<string[]> rows, IReadOnlyList<string>? variableColumns)
    {
        var result = new ParseResult { TotalInput = rows.Count };
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var cells in rows)
        {
            var rawPhone = cells.Length > 0 ? cells[0].Trim() : "";
            var normalized = _normalizer.Normalize(rawPhone);
            if (normalized == null)
            {
                result.Invalid++;
                AddInvalidSample(result, rawPhone);
                continue;
            }
            if (!seen.Add(normalized))
            {
                result.Duplicate++;
                continue;
            }

            Dictionary<string, string>? variables = null;
            if (variableColumns is { Count: > 0 })
            {
                variables = new Dictionary<string, string>(variableColumns.Count);
                for (var i = 0; i < variableColumns.Count; i++)
                {
                    var colIndex = i + 1; // column 0 is phone
                    var value = colIndex < cells.Length ? cells[colIndex].Trim() : "";
                    variables[variableColumns[i]] = value;
                }
            }

            result.Valid.Add(new BroadcastRecipient { Phone = normalized, Variables = variables });
        }
        return result;
    }

    private static void AddInvalidSample(ParseResult result, string raw)
    {
        if (result.InvalidSamples.Count < MaxInvalidSamples && !string.IsNullOrWhiteSpace(raw))
            result.InvalidSamples.Add(raw);
    }
}
