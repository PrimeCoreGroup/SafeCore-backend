using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeCore.SensorDataIngestion.Application;
using SafeCore.SensorDataIngestion.Domain;
using SafeCore.Shared.Data;

namespace SafeCore.SensorDataIngestion.API;

[ApiController]
[Route("api/[controller]")]
public class SensorReadingsController : ControllerBase
{
    private readonly SafeCoreDbContext _context;

    public SensorReadingsController(SafeCoreDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var readings = await _context.SensorReadings.ToListAsync();
        return Ok(readings);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var reading = await _context.SensorReadings.FindAsync(id);
        if (reading == null) return NotFound();
        return Ok(reading);
    }

    [HttpPost]
    public async Task<IActionResult> Create(SensorReadingDto dto)
    {
        var reading = new SensorReading
        {
            DeviceId = dto.DeviceId,
            SensorType = dto.SensorType,
            MeasuredValue = dto.MeasuredValue,
            Unit = dto.Unit,
            CapturedAt = dto.CapturedAt
        };

        _context.SensorReadings.Add(reading);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = reading.Id }, reading);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, SensorReadingDto dto)
    {
        var reading = await _context.SensorReadings.FindAsync(id);
        if (reading == null) return NotFound();

        reading.DeviceId = dto.DeviceId;
        reading.SensorType = dto.SensorType;
        reading.MeasuredValue = dto.MeasuredValue;
        reading.Unit = dto.Unit;
        reading.CapturedAt = dto.CapturedAt;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var reading = await _context.SensorReadings.FindAsync(id);
        if (reading == null) return NotFound();

        _context.SensorReadings.Remove(reading);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}