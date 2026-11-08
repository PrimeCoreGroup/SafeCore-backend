using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeCore.Locations.Application;
using SafeCore.Locations.Domain;
using SafeCore.DeviceManagement.Domain;
using SafeCore.Shared.Data;

namespace SafeCore.Locations.API;

[ApiController]
[Authorize]
[Route("api/properties")]
public sealed class PropertiesController(SafeCoreDbContext db, LocationAccessService access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(int page = 1, int pageSize = 100, CancellationToken ct = default)
    {
        var ids = await access.PropertyIdsAsync(User, ct);
        var safePage = Math.Max(page, 1); var safeSize = Math.Clamp(pageSize, 1, 250);
        var offset = (int)Math.Min((long)(safePage - 1) * safeSize, int.MaxValue);
        return Ok(await db.Properties.AsNoTracking().Where(item => ids.Contains(item.Id)).OrderBy(item => item.Name)
            .Skip(offset).Take(safeSize).ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!await access.CanViewPropertyAsync(User, id, ct)) return Forbid();
        var item = await db.Properties.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Create(PropertyDto dto, CancellationToken ct)
    {
        var item = new PropertySite { Name = dto.Name.Trim(), Address = dto.Address.Trim() };
        db.Properties.Add(item);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Update(Guid id, PropertyDto dto, CancellationToken ct)
    {
        var item = await db.Properties.FindAsync([id], ct);
        if (item is null) return NotFound();
        item.Name = dto.Name.Trim(); item.Address = dto.Address.Trim();
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var item = await db.Properties.FindAsync([id], ct);
        if (item is null) return NotFound();
        if (await db.Zones.AnyAsync(zone => zone.PropertyId == id, ct) || await db.UserAssignments.AnyAsync(a => a.PropertyId == id, ct))
            return Conflict(new ProblemDetails { Title = "A property with zones or user assignments cannot be deleted." });
        db.Properties.Remove(item); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpGet("{propertyId:guid}/zones")]
    public async Task<IActionResult> GetZones(Guid propertyId, int page = 1, int pageSize = 100, CancellationToken ct = default)
    {
        if (!await access.CanViewPropertyAsync(User, propertyId, ct)) return Forbid();
        var zoneIds = await access.ZoneIdsAsync(User, ct);
        var safePage = Math.Max(page, 1); var safeSize = Math.Clamp(pageSize, 1, 250);
        var offset = (int)Math.Min((long)(safePage - 1) * safeSize, int.MaxValue);
        var query = db.Zones.AsNoTracking().Where(zone => zone.PropertyId == propertyId && zoneIds.Contains(zone.Id)).OrderBy(zone => zone.Name);
        return Ok(await query.Skip(offset).Take(safeSize).ToListAsync(ct));
    }

    [HttpPost("{propertyId:guid}/zones")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> CreateZone(Guid propertyId, ZoneDto dto, CancellationToken ct)
    {
        if (!await db.Properties.AnyAsync(item => item.Id == propertyId, ct)) return NotFound();
        var zone = new Zone { PropertyId = propertyId, Name = dto.Name.Trim() };
        db.Zones.Add(zone);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict(new ProblemDetails { Title = "A zone with this name already exists in the property." }); }
        return Created($"/api/zones/{zone.Id}", zone);
    }
}
