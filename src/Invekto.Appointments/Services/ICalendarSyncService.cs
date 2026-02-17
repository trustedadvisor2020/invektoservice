namespace Invekto.Appointments.Services;

/// <summary>
/// GR-3.19: Calendar sync interface for external calendar integration.
/// Current implementation: MockCalendarSyncService (no real Google Calendar API).
/// Future: GoogleCalendarSyncService with OAuth2.
/// </summary>
public interface ICalendarSyncService
{
    /// <summary>
    /// Sync a booked appointment to external calendar.
    /// Returns external event ID or null if sync is not available.
    /// </summary>
    Task<string?> SyncAppointmentAsync(
        int tenantId, long appointmentId,
        string patientName, DateOnly date, TimeOnly startTime, TimeOnly endTime,
        CancellationToken ct = default);

    /// <summary>
    /// Cancel/remove appointment from external calendar.
    /// </summary>
    Task<bool> CancelSyncAsync(
        int tenantId, long appointmentId, string? externalEventId,
        CancellationToken ct = default);

    /// <summary>
    /// Check if calendar sync is configured and available for this tenant.
    /// </summary>
    Task<bool> IsAvailableAsync(int tenantId, CancellationToken ct = default);
}

/// <summary>
/// Mock implementation - always succeeds with fake event IDs.
/// Swap with GoogleCalendarSyncService when OAuth2 integration is ready.
/// </summary>
public sealed class MockCalendarSyncService : ICalendarSyncService
{
    public Task<string?> SyncAppointmentAsync(
        int tenantId, long appointmentId,
        string patientName, DateOnly date, TimeOnly startTime, TimeOnly endTime,
        CancellationToken ct = default)
    {
        // Mock: return a fake event ID
        return Task.FromResult<string?>($"mock-cal-{tenantId}-{appointmentId}");
    }

    public Task<bool> CancelSyncAsync(
        int tenantId, long appointmentId, string? externalEventId,
        CancellationToken ct = default)
    {
        // Mock: always succeeds
        return Task.FromResult(true);
    }

    public Task<bool> IsAvailableAsync(int tenantId, CancellationToken ct = default)
    {
        // Mock: always returns false (sync not configured)
        return Task.FromResult(false);
    }
}
