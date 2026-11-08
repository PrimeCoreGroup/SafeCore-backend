using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeCore.Locations.Application;
using SafeCore.RiskDetection.Application;
using SafeCore.Shared.Data;

namespace SafeCore.RiskDetection.API;

[ApiController]
[Authorize(Roles = "Administrator,Operator")]
[Route("api/analytics")]
public sealed class AnalyticsController(SafeCoreDbContext db, LocationAccessService access) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default)
    {
        if (!TryGetRange(from, to, out var start, out var end, out var error)) return BadRequest(new ProblemDetails { Title = error });
        var deviceIds = await access.DeviceIdsAsync(User, ct);
        var risks = await db.RiskSituations.AsNoTracking()
            .Where(item => item.DetectedAt >= start && item.DetectedAt < end && (item.DeviceId == null || deviceIds.Contains(item.DeviceId.Value)))
            .ToListAsync(ct);
        var availability = await CalculateAvailabilityAsync(deviceIds, start, end, ct);
        var resolved = risks.Where(item => item.Status == "Resolved").ToArray();
        var avgResponseMinutes = resolved.Where(item => item.ResolvedAt.HasValue)
            .Select(item => (double?)(item.ResolvedAt!.Value - item.DetectedAt).TotalMinutes).Average();
        var byType = risks.GroupBy(item => item.RiskType).ToDictionary(group => group.Key, group => group.Count());
        var bySeverity = risks.GroupBy(item => item.Severity).ToDictionary(group => group.Key, group => group.Count());
        var config = await db.SafetyConfigurations.AsNoTracking().SingleOrDefaultAsync(item => item.SingletonKey == 1, ct);
        var percent = risks.Count == 0 ? 0m : Math.Round(resolved.Length * 100m / risks.Count, 2);
        return Ok(new AnalyticsSummaryResponse(risks.Count, byType, bySeverity, percent, avgResponseMinutes, availability, start, end, config?.TimeZone ?? "America/Lima"));
    }

    [HttpGet("trends")]
    public async Task<IActionResult> Trends(string interval = "day", DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default)
    {
        if (!TryGetRange(from, to, out var start, out var end, out var error)) return BadRequest(new ProblemDetails { Title = error });
        if (interval is not ("day" or "week" or "month")) return BadRequest(new ProblemDetails { Title = "interval must be day, week, or month." });
        var deviceIds = await access.DeviceIdsAsync(User, ct);
        var rows = await db.RiskSituations.AsNoTracking()
            .Where(item => item.DetectedAt >= start && item.DetectedAt < end && (item.DeviceId == null || deviceIds.Contains(item.DeviceId.Value)))
            .Select(item => new { item.DetectedAt, item.Status, item.ResolvedAt })
            .ToListAsync(ct);
        var timezoneId = await db.SafetyConfigurations.AsNoTracking().Where(item => item.SingletonKey == 1).Select(item => item.TimeZone).SingleOrDefaultAsync(ct) ?? "America/Lima";
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.Utc; }
        DateTime Bucket(DateTime utcDate)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDate, DateTimeKind.Utc), zone);
            return interval switch
            {
                "week" => local.Date.AddDays(-(((int)local.DayOfWeek + 6) % 7)),
                "month" => new DateTime(local.Year, local.Month, 1),
                _ => local.Date
            };
        }
        return Ok(rows.GroupBy(item => Bucket(item.DetectedAt))
            .OrderBy(group => group.Key)
            .Select(group => new AnalyticsTrendPoint(group.Key, group.Count(), group.Count(item => item.Status is "Acknowledged" or "Resolved")))
            .ToArray());
    }

    [HttpGet("reports")]
    public IActionResult Reports() => Ok(new[]
    {
        new AnalyticsReportDescriptor("alerts-summary", "Resumen de alertas", "Riesgos por tipo, severidad y estado para un periodo.", "json"),
        new AnalyticsReportDescriptor("device-status", "Estado de dispositivos", "Estado actual y última comunicación de los nodos visibles.", "json"),
        new AnalyticsReportDescriptor("operations", "Actividad operativa", "Tendencia temporal de alertas registradas y atendidas.", "json")
    });

    private static bool TryGetRange(DateTimeOffset? from, DateTimeOffset? to, out DateTime start, out DateTime end, out string error)
    {
        end = (to ?? DateTimeOffset.UtcNow).UtcDateTime;
        start = (from ?? DateTimeOffset.UtcNow.AddDays(-30)).UtcDateTime;
        if (start >= end) { error = "from must be earlier than to."; return false; }
        if (end - start > TimeSpan.FromDays(366)) { error = "The requested date range cannot exceed 366 days."; return false; }
        error = string.Empty; return true;
    }

    private async Task<decimal> CalculateAvailabilityAsync(IReadOnlyCollection<Guid> deviceIds, DateTime start, DateTime end, CancellationToken ct)
    {
        if (deviceIds.Count == 0) return 0m;
        var events = await db.DeviceConnectivityEvents.AsNoTracking()
            .Where(item => deviceIds.Contains(item.DeviceId) && item.OccurredAt < end)
            .OrderBy(item => item.OccurredAt)
            .Select(item => new { item.DeviceId, item.ConnectivityStatus, item.OccurredAt })
            .ToListAsync(ct);
        var availableSeconds = 0d;
        foreach (var deviceId in deviceIds)
        {
            var deviceEvents = events.Where(item => item.DeviceId == deviceId).ToArray();
            var previous = deviceEvents.LastOrDefault(item => item.OccurredAt <= start);
            var isOnline = previous?.ConnectivityStatus == "Online";
            var cursor = start;
            foreach (var item in deviceEvents.Where(item => item.OccurredAt > start))
            {
                var at = item.OccurredAt > end ? end : item.OccurredAt;
                if (isOnline && at > cursor) availableSeconds += (at - cursor).TotalSeconds;
                isOnline = item.ConnectivityStatus == "Online";
                cursor = at;
                if (cursor >= end) break;
            }
            if (isOnline && cursor < end) availableSeconds += (end - cursor).TotalSeconds;
        }
        var totalSeconds = (end - start).TotalSeconds * deviceIds.Count;
        return totalSeconds <= 0 ? 0m : Math.Round((decimal)(availableSeconds * 100d / totalSeconds), 2);
    }
}
