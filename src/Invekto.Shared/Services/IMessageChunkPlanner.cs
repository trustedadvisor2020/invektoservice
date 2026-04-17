namespace Invekto.Shared.Services;

/// <summary>
/// HFM-1: plans per-chunk pre-delays for human-feel message dispatch.
/// PURE: no I/O, thread-safe, register as singleton.
/// Implementations choose their own jitter source; callers get a deterministic
/// schedule per invocation (chunks + computed delays).
/// </summary>
public interface IMessageChunkPlanner
{
    /// <summary>
    /// Plan a chunk schedule. Returns an ordered list where each entry carries the
    /// chunk text and the pre-dispatch delay (ms) the orchestrator must honor
    /// BEFORE sending that chunk.
    /// </summary>
    /// <param name="chunks">Ordered chunk strings (must be non-empty; planner trims/skips empty entries).</param>
    /// <returns>Ordered schedule. Empty list if <paramref name="chunks"/> is null/empty.</returns>
    IReadOnlyList<ChunkStep> Plan(IReadOnlyList<string> chunks);
}

/// <summary>
/// One emission in a chunked message plan.
/// PreDelayMs is the pause orchestrator should wait BEFORE dispatching this chunk.
/// </summary>
public sealed record ChunkStep(string Text, int PreDelayMs);
