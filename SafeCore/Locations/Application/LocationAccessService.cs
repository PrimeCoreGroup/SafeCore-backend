using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SafeCore.Locations.Domain;
using SafeCore.Shared.Data;

namespace SafeCore.Locations.Application;

public sealed class LocationAccessService(SafeCoreDbContext db)
{
    public static Guid? UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public async Task<List<Guid>> PropertyIdsAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        if (principal.IsInRole("Administrator")) return await db.Properties.Select(item => item.Id).ToListAsync(ct);
        var userId = UserId(principal);
        if (userId is null) return [];
        var assignments = db.UserAssignments.Where(item => item.UserId == userId);
        if (principal.IsInRole("Resident"))
        {
            var zonePropertyIds = db.Zones.Where(zone => assignments.Any(a => a.ZoneId == zone.Id)).Select(zone => zone.PropertyId);
            return await assignments.Select(item => item.PropertyId).Union(zonePropertyIds).Distinct().ToListAsync(ct);
        }
        return await assignments.Select(item => item.PropertyId).Distinct().ToListAsync(ct);
    }

    public async Task<List<Guid>> ZoneIdsAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        if (principal.IsInRole("Administrator")) return await db.Zones.Select(item => item.Id).ToListAsync(ct);
        var userId = UserId(principal);
        if (userId is null) return [];
        if (principal.IsInRole("Resident"))
            return await db.UserAssignments.Where(item => item.UserId == userId && item.ZoneId != null).Select(item => item.ZoneId!.Value).Distinct().ToListAsync(ct);
        var assigned = db.UserAssignments.Where(item => item.UserId == userId);
        if (principal.IsInRole("Operator"))
        {
            var allZonesPropertyIds = assigned.Where(item => item.ZoneId == null).Select(item => item.PropertyId);
            var allZones = db.Zones.Where(zone => allZonesPropertyIds.Contains(zone.PropertyId)).Select(zone => zone.Id);
            var selectedZones = assigned.Where(item => item.ZoneId != null).Select(item => item.ZoneId!.Value);
            return await allZones.Union(selectedZones).Distinct().ToListAsync(ct);
        }
        return await assigned.Select(item => item.PropertyId).Distinct()
            .Join(db.Zones, propertyId => propertyId, zone => zone.PropertyId, (_, zone) => zone.Id).Distinct().ToListAsync(ct);
    }

    public async Task<bool> CanViewPropertyAsync(ClaimsPrincipal principal, Guid propertyId, CancellationToken ct = default) =>
        (await PropertyIdsAsync(principal, ct)).Contains(propertyId);

    public async Task<bool> CanViewZoneAsync(ClaimsPrincipal principal, Guid zoneId, CancellationToken ct = default) =>
        (await ZoneIdsAsync(principal, ct)).Contains(zoneId);

    public async Task<List<Guid>> DeviceIdsAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        if (principal.IsInRole("Administrator")) return await db.Devices.Select(item => item.Id).ToListAsync(ct);
        var zoneIds = await ZoneIdsAsync(principal, ct);
        return await db.Devices.Where(item => item.ZoneId != null && zoneIds.Contains(item.ZoneId.Value)).Select(item => item.Id).ToListAsync(ct);
    }
}
