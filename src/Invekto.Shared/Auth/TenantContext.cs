namespace Invekto.Shared.Auth;

/// <summary>
/// Extracted tenant/user info from a validated JWT token.
/// GR-1.9: Passed through the request pipeline after JWT validation.
/// tenant_id is int (matching Main App SQL Server int identity).
/// </summary>
public sealed class TenantContext
{
    /// <summary>Tenant ID (int) from JWT claim "tenant_id"</summary>
    public required int TenantId { get; init; }

    /// <summary>User ID (int) from JWT claim "user_id" or "sub"</summary>
    public required int UserId { get; init; }

    /// <summary>User role from JWT claim "role" (e.g., admin, agent, system)</summary>
    public required string Role { get; init; }

    /// <summary>Whether this is a service-level call (no specific user)</summary>
    public bool IsServiceCall => Role == "service";
}
