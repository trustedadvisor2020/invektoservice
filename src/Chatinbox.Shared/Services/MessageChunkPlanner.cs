namespace Chatinbox.Shared.Services;

/// <summary>
/// HFM-1: default IMessageChunkPlanner implementation.
///
/// Formulas (from research doc, human-feel-multilang-research.md §3.2):
///   preThinkMs       = clamp(600  + 300 * Ln(firstChunk.Length + 1), 600,  1800)
///   interChunkMs[i]  = clamp(1200 +  80 * chunks[i].Length,          1200, 3500)
///   jitter           = uniformly distributed in [-15%, +15%]
///   totalCap         = 8000 ms (WA typing_on 25s timeout margin + UX ceiling)
///
/// When the summed schedule exceeds <see cref="TotalCapMs"/> each step is
/// scaled proportionally so the total stays within the cap. Order is preserved.
/// The jitter function is pluggable for deterministic unit tests.
/// </summary>
public sealed class MessageChunkPlanner : IMessageChunkPlanner
{
    public const int PreThinkFloorMs = 600;
    public const int PreThinkCeilMs = 1800;
    public const int InterChunkFloorMs = 1200;
    public const int InterChunkCeilMs = 3500;
    public const int TotalCapMs = 8000;
    public const double JitterAmplitude = 0.15;

    private readonly Func<double> _jitter;

    public MessageChunkPlanner() : this(DefaultJitter) { }

    /// <summary>
    /// Test-friendly constructor. <paramref name="jitter"/> must return values in
    /// the range [-1, 1]; the planner scales by <see cref="JitterAmplitude"/>.
    /// </summary>
    public MessageChunkPlanner(Func<double> jitter)
    {
        _jitter = jitter ?? DefaultJitter;
    }

    public IReadOnlyList<ChunkStep> Plan(IReadOnlyList<string> chunks)
    {
        if (chunks == null || chunks.Count == 0)
            return Array.Empty<ChunkStep>();

        var trimmed = new List<string>(chunks.Count);
        foreach (var c in chunks)
        {
            if (!string.IsNullOrWhiteSpace(c))
                trimmed.Add(c);
        }

        if (trimmed.Count == 0)
            return Array.Empty<ChunkStep>();

        // Single-chunk case: apply only a think delay (pre-dispatch pause).
        if (trimmed.Count == 1)
        {
            var delay = Jitter(ComputePreThink(trimmed[0].Length));
            delay = Math.Min(delay, TotalCapMs);
            return new[] { new ChunkStep(trimmed[0], delay) };
        }

        var delays = new int[trimmed.Count];
        delays[0] = Jitter(ComputePreThink(trimmed[0].Length));
        for (var i = 1; i < trimmed.Count; i++)
            delays[i] = Jitter(ComputeInterChunk(trimmed[i].Length));

        // Proportional cap: keep sequence shape but bound total wait time.
        var total = 0L;
        for (var i = 0; i < delays.Length; i++) total += delays[i];
        if (total > TotalCapMs && total > 0)
        {
            var scale = (double)TotalCapMs / total;
            for (var i = 0; i < delays.Length; i++)
                delays[i] = (int)Math.Floor(delays[i] * scale);
        }

        var plan = new ChunkStep[trimmed.Count];
        for (var i = 0; i < trimmed.Count; i++)
            plan[i] = new ChunkStep(trimmed[i], Math.Max(0, delays[i]));

        return plan;
    }

    private static int ComputePreThink(int charCount)
    {
        var ms = 600 + 300 * Math.Log(charCount + 1);
        return (int)Math.Clamp(ms, PreThinkFloorMs, PreThinkCeilMs);
    }

    private static int ComputeInterChunk(int charCount)
    {
        var ms = 1200 + 80 * charCount;
        return (int)Math.Clamp(ms, InterChunkFloorMs, InterChunkCeilMs);
    }

    private int Jitter(int baseMs)
    {
        // _jitter() returns [-1, 1]; multiply by amplitude for ±15%.
        var factor = 1.0 + _jitter() * JitterAmplitude;
        return Math.Max(0, (int)Math.Round(baseMs * factor));
    }

    private static double DefaultJitter()
    {
        // Random.Shared is thread-safe; returns [-1, 1).
        return Random.Shared.NextDouble() * 2.0 - 1.0;
    }
}
