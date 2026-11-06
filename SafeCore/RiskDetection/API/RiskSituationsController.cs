using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SafeCore.Shared.Contracts;
using SafeCore.Emergency.Domain;
using SafeCore.RiskDetection.Application;
using SafeCore.RiskDetection.Domain;

namespace SafeCore.RiskDetection.API;

[ApiController]
[Authorize(Roles = "Administrator,Operator,Responder")]
[Route("api/[controller]")]
public class RiskSituationsController : ControllerBase
{
    private readonly IRepository<RiskSituation> _situations;
    private readonly IRepository<EmergencyResponse> _responses;
    public RiskSituationsController(IRepository<RiskSituation> situations, IRepository<EmergencyResponse> responses)
    {
        _situations = situations;
        _responses = responses;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var situations = await _situations.ListAsync(page, pageSize, cancellationToken);
        return Ok(situations);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var situation = await _situations.GetByIdAsync(id);
        if (situation == null) return NotFound();
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
            DetectedAt = dto.DetectedAt
        };

        _situations.Add(situation);
        await _situations.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = situation.Id }, situation);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> Update(Guid id, RiskSituationDto dto)
    {
        var situation = await _situations.GetByIdAsync(id);
        if (situation == null) return NotFound();

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
