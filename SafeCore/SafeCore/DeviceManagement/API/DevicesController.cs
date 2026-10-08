using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeCore.DeviceManagement.Application;
using SafeCore.DeviceManagement.Domain;
using SafeCore.Shared.Data;

namespace SafeCore.DeviceManagement.API;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly SafeCoreDbContext _context;

    public DevicesController(SafeCoreDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var devices = await _context.Devices.ToListAsync();
        return Ok(devices);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null) return NotFound();
        return Ok(device);
    }

    [HttpPost]
    public async Task<IActionResult> Create(DeviceDto dto)
    {
        var device = new Device
        {
            SerialNumber = dto.SerialNumber,
            Name = dto.Name,
            Category = dto.Category,
            ConnectivityStatus = dto.ConnectivityStatus
        };

        _context.Devices.Add(device);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, DeviceDto dto)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null) return NotFound();

        device.SerialNumber = dto.SerialNumber;
        device.Name = dto.Name;
        device.Category = dto.Category;
        device.ConnectivityStatus = dto.ConnectivityStatus;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null) return NotFound();

        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}