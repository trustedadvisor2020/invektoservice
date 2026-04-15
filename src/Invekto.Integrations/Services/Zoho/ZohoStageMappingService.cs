// Adim 3 Paket 1: thin service over ZohoStageMappingRepository.
// Validates lifecycle event whitelist (same as DB CHECK) and projects rows to Shared DTOs.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Integrations.Data;
using Invekto.Shared.Contracts.Zoho;

namespace Invekto.Integrations.Services.Zoho;

public sealed class ZohoStageMappingService : IZohoStageMappingService
{
    // Must stay in sync with migration 013-zoho-stage-mappings.sql CHECK constraint.
    private static readonly HashSet<string> AllowedEvents = new(StringComparer.Ordinal)
    {
        "welcome_sent","engaged","qualified","offer_sent","closed_won","deposit_paid","closed_lost"
    };

    private readonly ZohoStageMappingRepository _repo;

    public ZohoStageMappingService(ZohoStageMappingRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ZohoStageMappingDto>> ListAsync(int tenantId, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(tenantId, ct).ConfigureAwait(false);
        var list = new List<ZohoStageMappingDto>(rows.Count);
        foreach (var r in rows)
        {
            list.Add(new ZohoStageMappingDto
            {
                ZohoEvent         = r.ZohoEvent,
                ZohoTransitionId   = r.ZohoTransitionId,
                ZohoTransitionName = r.ZohoTransitionName,
                UpdatedAt          = r.UpdatedAt.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(r.UpdatedAt.Value, DateTimeKind.Utc))
                    : null
            });
        }
        return list;
    }

    public async Task<string?> ResolveTransitionIdAsync(
        int tenantId, string zohoEvent, CancellationToken ct = default)
    {
        if (!AllowedEvents.Contains(zohoEvent))
            throw new ArgumentException(
                $"INV-GEN-003: Unknown Zoho event '{zohoEvent}'. Allowed: {string.Join(",", AllowedEvents)}",
                nameof(zohoEvent));

        var row = await _repo.FindAsync(tenantId, zohoEvent, ct).ConfigureAwait(false);
        return row?.ZohoTransitionId;
    }

    public Task ReplaceAsync(int tenantId, ZohoStageMappingUpsertRequest request, CancellationToken ct = default)
    {
        if (tenantId <= 0) throw new ArgumentException("INV-GEN-003: tenantId required", nameof(tenantId));
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (request.Mappings is null) throw new ArgumentException("INV-GEN-003: Mappings required", nameof(request));

        foreach (var m in request.Mappings)
        {
            if (!AllowedEvents.Contains(m.ZohoEvent))
                throw new ArgumentException(
                    $"INV-GEN-003: Unknown Zoho event '{m.ZohoEvent}'. Allowed: {string.Join(",", AllowedEvents)}",
                    nameof(request));
            if (string.IsNullOrWhiteSpace(m.ZohoTransitionId))
                throw new ArgumentException(
                    $"INV-GEN-003: zoho_transition_id is required for '{m.ZohoEvent}'",
                    nameof(request));
        }

        // Reject duplicate zoho_event keys early (DB unique constraint would surface as opaque 23505).
        var dup = request.Mappings
            .GroupBy(e => e.ZohoEvent, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (dup is not null)
            throw new ArgumentException(
                $"INV-GEN-003: Duplicate Zoho event '{dup.Key}' in upsert payload.",
                nameof(request));

        var entries = request.Mappings
            .Select(m => (m.ZohoEvent, m.ZohoTransitionId, m.ZohoTransitionName))
            .ToList();

        return _repo.ReplaceAllAsync(tenantId, entries, ct);
    }
}
