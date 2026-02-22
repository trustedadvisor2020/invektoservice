namespace Invekto.Shared.Auth;

/// <summary>
/// Extracted identity info from a validated inma (Main App) JWT token.
/// inma JWT uses different claim names than inse internal tokens.
/// CompanyCode → TenantId (CompanyId is inma internal), nameidentifier → UserId, ChatRole → Role.
/// </summary>
public sealed class InmaTokenContext
{
    /// <summary>inse tenant_id — mapped from inma 'CompanyCode' claim (fallback: 'CompanyId')</summary>
    public required int TenantId { get; init; }

    /// <summary>inma user id — from 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'</summary>
    public required int UserId { get; init; }

    /// <summary>inse role — mapped from inma 'ChatRole': "1"=agent, "2"=admin. Default: agent.</summary>
    public required string Role { get; init; }

    /// <summary>Display name from inma 'FullName' claim. Empty string if missing.</summary>
    public required string FullName { get; init; }

    /// <summary>UI language from inma 'Lang' claim. Default: "tr".</summary>
    public required string Lang { get; init; }

    /// <summary>
    /// Licensed inse features from inma 'InseFeatures' claim.
    /// Format: JSON array of strings e.g. ["FlowBuilder","Knowledge","Outbound"].
    /// Empty array = no licensed features.
    /// </summary>
    public required string[] InseFeatures { get; init; }
}
