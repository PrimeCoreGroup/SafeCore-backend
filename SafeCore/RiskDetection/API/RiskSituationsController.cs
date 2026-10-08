using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeCore.RiskDetection.Application;
using SafeCore.RiskDetection.Domain;
using SafeCore.Shared.Data;

namespace SafeCore.RiskDetection.API;

[ApiController]
[Route("api/[controller]")]
public class RiskSituationsController : ControllerBase
{
    private readonly SafeCoreDbContext _context;

    public RiskSituationsController(SafeCoreDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var situations = await _context.RiskSituations.ToListAsync();
        return Ok(situations);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var situation = await _context.RiskSituations.FindAsync(id);
        if (situation == null) return NotFound();
        return Ok(situation);
    }

    [HttpPost]
    public async Task<IActionResult> Create(RiskSituationDto dto)
    {
        var situation = new RiskSituation
        {
            RiskType = dto.RiskType,
            Status = dto.Status,
            Severity = dto.Severity,
            DetectedAt = dto.DetectedAt
        };

        _context.RiskSituations.Add(situation);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = situation.Id }, situation);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, RiskSituationDto dto)
    {
        var situation = await _context.RiskSituations.FindAsync(id);
        if (situation == null) return NotFound();

        situation.RiskType = dto.RiskType;
        situation.Status = dto.Status;
        situation.Severity = dto.Severity;
        situation.DetectedAt = dto.DetectedAt;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var situation = await _context.RiskSituations.FindAsync(id);
        if (situation == null) return NotFound();

        _context.RiskSituations.Remove(situation);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}