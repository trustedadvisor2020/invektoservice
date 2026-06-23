namespace Chatinbox.Shared.Auth;

/// <summary>
/// Extracted identity info from a validated inma (Main App) JWT token.
/// Only JWT-sourced fields live here — <b>no DB-resolved tenant_id</b>. Callers
/// must translate <see cref="CompanyCode"/> (opaque string) to an INSE integer
/// tenant_id via <c>TenantRegistryRepository.ResolveOrCreateByInmaCodeAsync</c>.
///
/// WHY string CompanyCode: inma's CompanyCode is an opaque identifier — tenant
/// 5050 happens to be numeric ("5050") but DentAdavista is "dentadavista".
/// The earlier int.TryParse pattern silently worked only for numeric codes.
/// </summary>
public sealed class InmaTokenContext
{
    /// <summary>
    /// inma 'CompanyCode' claim (fallback: 'CompanyId'). Opaque string identifier
    /// — do NOT assume numeric. Examples: "5050", "dentadavista", "DEMO".
    /// Validated non-empty, trimmed, max length 100 chars.
    /// </summary>
    public required string CompanyCode { get; init; }

    /// <summary>inma user id — from 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'. Integer (user ids stay numeric per INMA contract).</summary>
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
