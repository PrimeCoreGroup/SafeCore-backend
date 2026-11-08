using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeCore.DeviceManagement.Domain;
using SafeCore.Locations.Application;
using SafeCore.Locations.Domain;
using SafeCore.Shared.Data;

namespace SafeCore.Locations.API;

[ApiController]
[Authorize]
[Route("api/zones")]
public sealed class ZonesController(SafeCoreDbContext db, LocationAccessService access) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetZone(Guid id, CancellationToken ct)
    {
        if (!await access.CanViewZoneAsync(User, id, ct)) return Forbid();
        var zone = await db.Zones.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        return zone is null ? NotFound() : Ok(zone);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Update(Guid id, ZoneDto dto, CancellationToken ct)
    {
        var zone = await db.Zones.FindAsync([id], ct);
        if (zone is null) return NotFound();
        zone.Name = dto.Name.Trim();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict(new ProblemDetails { Title = "A zone with this name already exists in the property." }); }
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var zone = await db.Zones.FindAsync([id], ct);
        if (zone is null) return NotFound();
        if (await db.Devices.AnyAsync(device => device.ZoneId == id, ct) || await db.UserAssignments.AnyAsync(a => a.ZoneId == id, ct))
            return Conflict(new ProblemDetails { Title = "A zone with assigned devices or users cannot be deleted." });
        db.Zones.Remove(zone); await db.SaveChangesAsync(ct); return NoContent();
    }
}
