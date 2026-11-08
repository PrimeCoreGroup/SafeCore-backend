using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using SafeCore.Shared.Contracts;
using SafeCore.Emergency.Domain;
using SafeCore.RiskDetection.Domain;
using SafeCore.Shared.Data;
using SafeCore.Locations.Application;
using SafeCore.RiskDetection.Application;

namespace SafeCore.RiskDetection.API;

[ApiController]
[Authorize(Roles = "Administrator,Operator,Responder")]
[Route("api/[controller]")]
public class RiskSituationsController : ControllerBase
{
    private readonly IRepository<RiskSituation> _situations;
    private readonly IRepository<EmergencyResponse> _responses;
    private readonly SafeCoreDbContext _db;
    private readonly LocationAccessService _access;
    private readonly CriticalAlertNotifier _notifier;
    public RiskSituationsController(IRepository<RiskSituation> situations, IRepository<EmergencyResponse> responses, SafeCoreDbContext db, LocationAccessService access, CriticalAlertNotifier notifier)
    {
        _situations = situations;
        _responses = responses;
        _db = db; _access = access; _notifier = notifier;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(int page = 1, int pageSize = 100, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
    {
        if (from >= to && from is not null && to is not null) return BadRequest(new ProblemDetails { Title = "from must be earlier than to." });
        var query = _db.RiskSituations.AsNoTracking();
        if (from is not null) query = query.Where(item => item.DetectedAt >= from.Value.UtcDateTime);
        if (to is not null) query = query.Where(item => item.DetectedAt < to.Value.UtcDateTime);
        if (!User.IsInRole("Administrator"))
        {
            var ids = await _access.DeviceIdsAsync(User, cancellationToken);
            query = query.Where(item => item.DeviceId == null || ids.Contains(item.DeviceId.Value));
        }
        var safePage = Math.Max(page, 1); var safeSize = Math.Clamp(pageSize, 1, 250);
        var situations = await query.OrderByDescending(item => item.CreatedAt).Skip((int)Math.Min((long)(safePage - 1) * safeSize, int.MaxValue)).Take(safeSize).ToListAsync(cancellationToken);
        return Ok(situations);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var situation = await _situations.GetByIdAsync(id);
        if (situation == null) return NotFound();
        if (situation.DeviceId is { } deviceId && !User.IsInRole("Administrator") && !(await _access.DeviceIdsAsync(User)).Contains(deviceId)) return Forbid();
        return Ok(situation);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> Create(RiskSituationDto dto)
    {
        var situation = new RiskSituation
        {
            RiskType = dto.RiskType,
            Status = dto.Status,
            Severity = dto.Severity,
            DetectedAt = dto.DetectedAt,
            ResolvedAt = dto.Status == "Resolved" ? DateTime.UtcNow : null
        };

        _situations.Add(situation);
        await _situations.SaveChangesAsync();
        await _notifier.NotifyRespondersAsync(situation);
        return CreatedAtAction(nameof(GetById), new { id = situation.Id }, situation);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> Update(Guid id, RiskSituationDto dto)
    {
        var situation = await _situations.GetByIdAsync(id);
        if (situation == null) return NotFound();
        if (situation.DeviceId is { } scopedDeviceId && !User.IsInRole("Administrator") && !(await _access.DeviceIdsAsync(User)).Contains(scopedDeviceId)) return Forbid();

        if (dto.Status == "Resolved" && (situation.Status != "Resolved" || situation.ResolvedAt is null)) situation.ResolvedAt = DateTime.UtcNow;
        else if (dto.Status != "Resolved") situation.ResolvedAt = null;
        situation.RiskType = dto.RiskType;
        situation.Status = dto.Status;
        situation.Severity = dto.Severity;
        situation.DetectedAt = dto.DetectedAt;

        await _situations.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var situation = await _situations.GetByIdAsync(id);
        if (situation == null) return NotFound();
        if (await _responses.AnyAsync(response => response.RiskSituationId == id))
            return Conflict(new ProblemDetails { Title = "A risk referenced by an emergency response cannot be deleted." });

        _situations.Remove(situation);
        await _situations.SaveChangesAsync();
        return NoContent();
    }
}
