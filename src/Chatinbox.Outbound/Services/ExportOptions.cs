namespace Chatinbox.Outbound.Services;

/// <summary>
/// FEAT-OBI Phase 1A Plan B gating + bounds for the Export Manager. Bound from
/// appsettings "Export" section. Production-safe default: DISABLED.
///
/// A DEDICATED flag (NOT reused from ContactListOptions): PII-egress (export of
/// raw phone lists + campaign delivery status) is a distinct risk surface from
/// import, so ops can open one without the other. Explicit AllowAllTenants flag
/// (not "empty allowlist = all") so a misconfiguration can never silently open
/// the feature.
/// </summary>
public sealed class ExportOptions
{
    public const string SectionName = "Export";

    /// <summary>Master kill-switch. Default false = export endpoints return INV-OB-056.</summary>
    public bool Enabled { get; set; }

    /// <summary>Pilot tenant allowlist. Empty = no tenant may export (unless <see cref="AllowAllTenants"/>).</summary>
    public List<int> AllowedTenantIds { get; set; } = new();

    /// <summary>When true, every tenant may export (allowlist ignored). Prod default per Q's interview.</summary>
    public bool AllowAllTenants { get; set; }

    /// <summary>
    /// Hard cap on rows a single XLSX export may build. Over-cap is rejected (INV-OB-058)
    /// BEFORE the workbook is constructed (ClosedXML builds in-memory). CSV streams row-by-row
    /// and is not bound by this. Matches the contact-list 50k ceiling.
    /// </summary>
    public int MaxXlsxRows { get; set; } = 50_000;

    /// <summary>
    /// Cap on the recipient table embedded in the PDF report-data. The full set stays
    /// available via CSV/XLSX; the PDF shows the first N with a visible truncation note.
    /// </summary>
    public int PdfRecipientTableLimit { get; set; } = 2_000;

    /// <summary>
    /// FEAT-OBI Phase 2: cap on the on-screen "Telefon Numarası Ara" history table. The screen
    /// shows the most recent N rows with a visible "truncated → use CSV" marker; the CSV/PDF
    /// download exports the full set (a single number's history is naturally bounded).
    /// </summary>
    public int PhoneHistoryScreenLimit { get; set; } = 1_000;

    public bool IsTenantAllowed(int tenantId) =>
        Enabled && (AllowAllTenants || AllowedTenantIds.Contains(tenantId));
}
