namespace Chatinbox.Outbound.Services;

/// <summary>
/// FEAT-PROJELER (PKT-14) slice S2 gating + bounds for projects CRUD. Bound from the
/// appsettings "Projects" section. Production-safe default: DISABLED.
///
/// Mirrors <see cref="ContactListOptions"/> (same pilot-allowlist shape so ops keep one
/// mental model). Gating is intentionally SEPARATE from ContactList/BulkSend/CxapiSend:
/// managing a project is metadata-only and never dispatches a message, so opening this
/// does NOT enable any send path — sends stay gated by CxapiSend/BulkSend plus the P0-3
/// live-enablement gate. Explicit <see cref="AllowAllTenants"/> (not "empty = all") so a
/// misconfiguration can never silently open the feature.
/// </summary>
public sealed class ProjectsOptions
{
    public const string SectionName = "Projects";

    /// <summary>Master kill-switch. Default false = endpoints return INV-OB-066.</summary>
    public bool Enabled { get; set; }

    /// <summary>Pilot tenant allowlist. Empty = no tenant (unless <see cref="AllowAllTenants"/>).</summary>
    public List<int> AllowedTenantIds { get; set; } = new();

    /// <summary>
    /// When true, every tenant may manage projects (allowlist ignored). Metadata-only — the
    /// SEND path is still independently gated, so opening this never lets a tenant dispatch.
    /// </summary>
    public bool AllowAllTenants { get; set; }

    /// <summary>Max data_lists a single project may target.</summary>
    public int MaxTargetsPerProject { get; set; } = 50;

    /// <summary>Max project name length (matches projects.name VARCHAR(255)).</summary>
    public int MaxNameLength { get; set; } = 255;

    /// <summary>Max project description length — bound the TEXT column at the app edge.</summary>
    public int MaxDescriptionLength { get; set; } = 2000;

    public bool IsTenantAllowed(int tenantId) =>
        Enabled && (AllowAllTenants || AllowedTenantIds.Contains(tenantId));
}
