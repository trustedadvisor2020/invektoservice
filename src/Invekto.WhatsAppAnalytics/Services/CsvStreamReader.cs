using System.Text;

namespace Invekto.WhatsAppAnalytics.Services;

/// <summary>
/// Streaming CSV parser that reads files in configurable chunks.
/// C# port of Python csv_parser.py stream_csv().
/// Handles UTF-8 BOM, configurable delimiter, and large files (300MB+).
/// </summary>
public sealed class CsvStreamReader
{
    /// <summary>
    /// Expected CSV column order (WA export format).
    /// </summary>
    public static readonly string[] ExpectedColumns = new[]
    {
        "business_phone", "date", "time", "conversation_id",
        "message_text", "sender_type", "agent_name"
    };

    /// <summary>
    /// Stream CSV file in chunks. Yields List of string[] rows (without header).
    /// </summary>
    public async IAsyncEnumerable<List<string[]>> StreamChunksAsync(
        string filePath,
        char delimiter = ';',
        int chunkSize = 100_000)
    {
        // Auto-detect BOM encoding
        var encoding = DetectEncoding(filePath);

        using var reader = new StreamReader(filePath, encoding);

        // Read and validate header
        var headerLine = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(headerLine))
            throw new InvalidOperationException("CSV file is empty");

        var chunk = new List<string[]>(chunkSize);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseCsvLine(line, delimiter);
            if (fields.Length >= ExpectedColumns.Length)
            {
                chunk.Add(fields);
            }

            if (chunk.Count >= chunkSize)
            {
                yield return chunk;
                chunk = new List<string[]>(chunkSize);
            }
        }

        if (chunk.Count > 0)
            yield return chunk;
    }

    /// <summary>
    /// Count total lines in file (for progress tracking).
    /// Fast binary scan, memory-efficient.
    /// </summary>
    public long CountLines(string filePath)
    {
        long count = 0;
        var buffer = new byte[64 * 1024]; // 64KB buffer

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length);
        int bytesRead;
        while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == (byte)'\n') count++;
            }
        }

        return count; // includes header, subtract 1 for data count
    }

    /// <summary>
    /// Validate CSV header matches expected columns.
    /// </summary>
    public string[] ValidateHeader(string filePath, char delimiter = ';')
    {
        var encoding = DetectEncoding(filePath);
        using var reader = new StreamReader(filePath, encoding);
        var headerLine = reader.ReadLine();

        if (string.IsNullOrEmpty(headerLine))
            throw new InvalidOperationException("CSV file is empty");

        var headers = ParseCsvLine(headerLine, delimiter)
            .Select(h => h.Trim().ToLowerInvariant().Replace(" ", "_"))
            .ToArray();

        // Check minimum required columns
        var missing = ExpectedColumns.Where(ec => !headers.Contains(ec)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Missing required columns: {string.Join(", ", missing)}. Found: {string.Join(", ", headers)}");

        return headers;
    }

    /// <summary>
    /// Detect file encoding (UTF-8 BOM vs plain UTF-8).
    /// </summary>
    private static Encoding DetectEncoding(string filePath)
    {
        var bom = new byte[3];
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var read = fs.Read(bom, 0, 3);

        // UTF-8 BOM: EF BB BF
        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            return new UTF8Encoding(true); // UTF-8 with BOM

        return new UTF8Encoding(false); // UTF-8 without BOM
    }

    /// <summary>
    /// Parse a CSV line respecting quoted fields.
    /// Handles fields like: value;"quoted;value";another
    /// </summary>
    private static string[] ParseCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote ""
                    current.Append('"');
                    i++; // skip next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == delimiter && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
