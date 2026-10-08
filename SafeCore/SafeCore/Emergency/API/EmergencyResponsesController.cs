using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeCore.Emergency.Application;
using SafeCore.Emergency.Domain;
using SafeCore.Shared.Data;

namespace SafeCore.Emergency.API;

[ApiController]
[Route("api/[controller]")]
public class EmergencyResponsesController : ControllerBase
{
    private readonly SafeCoreDbContext _context;

    public EmergencyResponsesController(SafeCoreDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var responses = await _context.EmergencyResponses.ToListAsync();
        return Ok(responses);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _context.EmergencyResponses.FindAsync(id);
        if (response == null) return NotFound();
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create(EmergencyResponseDto dto)
    {
        var response = new EmergencyResponse
        {
            ProtocolId = dto.ProtocolId,
            EmergencyType = dto.EmergencyType,
            RiskSituationId = dto.RiskSituationId,
            Status = dto.Status
        };

        _context.EmergencyResponses.Add(response);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, EmergencyResponseDto dto)
    {
        var response = await _context.EmergencyResponses.FindAsync(id);
        if (response == null) return NotFound();

        response.ProtocolId = dto.ProtocolId;
        response.EmergencyType = dto.EmergencyType;
        response.RiskSituationId = dto.RiskSituationId;
        response.Status = dto.Status;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var response = await _context.EmergencyResponses.FindAsync(id);
        if (response == null) return NotFound();

        _context.EmergencyResponses.Remove(response);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}