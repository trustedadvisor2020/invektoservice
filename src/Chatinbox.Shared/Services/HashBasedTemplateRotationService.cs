namespace Chatinbox.Shared.Services;

/// <summary>
/// Default <see cref="ITemplateRotationService"/> implementation using FNV-1a 32-bit hash.
/// FNV-1a chosen over <c>string.GetHashCode()</c> because the latter is randomized per
/// process on .NET Core — which would break determinism across restarts and node instances.
/// </summary>
public sealed class HashBasedTemplateRotationService : ITemplateRotationService
{
    private const uint FnvOffset = 2166136261u;
    private const uint FnvPrime = 16777619u;

    public int PickVariantIndex(string? contactKey, string nodeId, int variantCount)
    {
        if (variantCount <= 1)
            return 0;

        // Build deterministic key: "{contactKey}|{nodeId}" — when contactKey is empty,
        // all contacts still map to the same index (documented in interface).
        var hash = FnvOffset;
        if (!string.IsNullOrEmpty(contactKey))
            hash = FnvHash(hash, contactKey);
        hash = FnvHash(hash, "|");
        hash = FnvHash(hash, nodeId);

        return (int)(hash % (uint)variantCount);
    }

    private static uint FnvHash(uint seed, string value)
    {
        var hash = seed;
        for (var i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= FnvPrime;
        }
        return hash;
    }
}
