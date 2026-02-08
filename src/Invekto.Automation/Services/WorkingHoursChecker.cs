using System.Text.Json;
using Invekto.Automation.Data;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Checks if current time is within tenant's working hours.
/// Working hours stored in tenant_registry.settings_json.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class WorkingHoursChecker
{
    private readonly AutomationRepository _repo;
    private readonly JsonLinesLogger _logger;

    private const string DefaultTimezone = "Europe/Istanbul";
    private const string DefaultStart = "09:00";
    private const string DefaultEnd = "18:00";

    public WorkingHoursChecker(AutomationRepository repo, JsonLinesLogger logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// Check if the current time is within the tenant's working hours.
    /// Returns (isWithinHours, offHoursMessage).
    /// If no working hours configured, assumes always open (returns true).
    /// </summary>
    public async Task<(bool IsWithinHours, string? OffHoursMessage)> CheckAsync(int tenantId, CancellationToken ct = default)
    {
        try
        {
            var settingsJson = await _repo.GetTenantSettingsJsonAsync(tenantId, ct);
            if (string.IsNullOrEmpty(settingsJson))
                return (true, null); // No settings = always open

            using var doc = JsonDocument.Parse(settingsJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("working_hours", out var whElement))
                return (true, null); // No working_hours defined = always open

            var timezone = whElement.TryGetProperty("timezone", out var tz) ? tz.GetString() ?? DefaultTimezone : DefaultTimezone;
            var startStr = whElement.TryGetProperty("start", out var s) ? s.GetString() ?? DefaultStart : DefaultStart;
            var endStr = whElement.TryGetProperty("end", out var e) ? e.GetString() ?? DefaultEnd : DefaultEnd;

            // Parse days_off (e.g., ["Sunday", "Saturday"])
            var daysOff = new HashSet<DayOfWeek>();
            if (whElement.TryGetProperty("days_off", out var daysElement) && daysElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var day in daysElement.EnumerateArray())
                {
                    if (Enum.TryParse<DayOfWeek>(day.GetString(), ignoreCase: true, out var dow))
                        daysOff.Add(dow);
                }
            }

            var tzi = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzi);

            // Check day off
            if (daysOff.Contains(now.DayOfWeek))
            {
                var offMsg = whElement.TryGetProperty("off_message", out var msg) ? msg.GetString() : null;
                return (false, offMsg);
            }

            // Check time range
            if (TimeOnly.TryParse(startStr, out var start) && TimeOnly.TryParse(endStr, out var end))
            {
                var currentTime = TimeOnly.FromDateTime(now);
                var isOpen = currentTime >= start && currentTime <= end;
                if (!isOpen)
                {
                    var offMsg = whElement.TryGetProperty("off_message", out var msg) ? msg.GetString() : null;
                    return (false, offMsg);
                }
            }

            return (true, null);
        }
        catch (TimeZoneNotFoundException ex)
        {
            _logger.SystemWarn($"Invalid timezone for tenant {tenantId}: {ex.Message}, treating as open");
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"Working hours check failed for tenant {tenantId}: {ex.Message}, treating as open");
            return (true, null);
        }
    }
}
