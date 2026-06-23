using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Inma.Dtos;

/// <summary>
/// FEAT-J2: Data payload nested inside WapCrmApiResponse&lt;InmaOptOutData&gt;
/// returned by INMA /api/optout and /api/optin
/// (wapcrm-marketing-api.md section 5.1 and 5.2).
/// Wire format uses PascalCase property names to match INMA.
/// </summary>
public sealed class InmaOptOutData
{
    [JsonPropertyName("IdentifierType")]
    public string? IdentifierType { get; init; }

    [JsonPropertyName("Identifier")]
    public string? Identifier { get; init; }

    [JsonPropertyName("Scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("IsMarketingBlocked")]
    public bool IsMarketingBlocked { get; init; }

    [JsonPropertyName("MarketingBlockedAt")]
    public DateTime? MarketingBlockedAt { get; init; }

    [JsonPropertyName("MarketingUnblockedAt")]
    public DateTime? MarketingUnblockedAt { get; init; }

    [JsonPropertyName("MarketingBlockReason")]
    public string? MarketingBlockReason { get; init; }

    [JsonPropertyName("MarketingBlockSource")]
    public string? MarketingBlockSource { get; init; }

    [JsonPropertyName("AffectedChatCount")]
    public int AffectedChatCount { get; init; }

    [JsonPropertyName("AlreadyOptedOut")]
    public bool AlreadyOptedOut { get; init; }

    [JsonPropertyName("WasOptedOut")]
    public bool WasOptedOut { get; init; }
}
