namespace Invekto.Backend.Services.Inma;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C2 config (bound from the "InmaWebhook" section). The exact
/// HMAC encoding lives in a vendor PDF we don't yet have, so the validator is fail-closed
/// AND self-identifying: with <see cref="AcceptEitherEncoding"/> on it tries base64 then hex
/// and logs which matched on the first live 5050 event. Once confirmed, lock it down by
/// setting the encoding explicitly and turning AcceptEitherEncoding off.
/// </summary>
public sealed class InmaWebhookOptions
{
    public const string SectionName = "InmaWebhook";

    /// <summary>Preferred digest encoding of the X-Invekto-Signature value. "base64" or "hex".</summary>
    public string SignatureDigestEncoding { get; set; } = "base64";

    /// <summary>Verify-live mode: if the preferred encoding fails, also try the other and log which matched. Default ON until the format is confirmed.</summary>
    public bool AcceptEitherEncoding { get; set; } = true;

    /// <summary>How the stored secret is encoded. "base64" (INMA shows a base64 32-byte key) decodes to raw key bytes; "utf8" uses the string bytes verbatim.</summary>
    public string KeyEncoding { get; set; } = "base64";

    /// <summary>Optional prefix stripped from the signature header before decoding (e.g. "sha256="). Empty = raw digest.</summary>
    public string SignaturePrefix { get; set; } = "";

    /// <summary>Hard cap on the raw request body read into memory before parse (DoS guard on the shared webhook URL). 1 MB — far above any real INMA message/ack/event payload, so the existing paths are unaffected.</summary>
    public int MaxBodyBytes { get; set; } = 1_048_576;

    /// <summary>Numeric timestamp-freshness/replay check. Default OFF — enabled only after the live timestamp FORMAT is confirmed (a wrong parse would reject all events).</summary>
    public bool EnforceTimestampFreshness { get; set; } = false;

    /// <summary>Max accepted age of X-Invekto-Timestamp when freshness is enforced. Default 9h &gt; INMA's ~7.5h byte-identical retry horizon + skew.</summary>
    public int FreshnessMaxAgeSeconds { get; set; } = 32_400;

    /// <summary>Timestamp wire format for freshness parse: "unixSeconds", "unixMilliseconds", or "iso8601".</summary>
    public string TimestampFormat { get; set; } = "unixSeconds";
}
