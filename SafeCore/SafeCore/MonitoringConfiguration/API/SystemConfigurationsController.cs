using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeCore.MonitoringConfiguration.Application;
using SafeCore.MonitoringConfiguration.Domain;
using SafeCore.Shared.Data;

namespace SafeCore.MonitoringConfiguration.API;

[ApiController]
[Route("api/[controller]")]
public class SystemConfigurationsController : ControllerBase
{
    private readonly SafeCoreDbContext _context;

    public SystemConfigurationsController(SafeCoreDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var configurations = await _context.SystemConfigurations.ToListAsync();
        return Ok(configurations);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var configuration = await _context.SystemConfigurations.FindAsync(id);
        if (configuration == null) return NotFound();
        return Ok(configuration);
    }

    [HttpPost]
    public async Task<IActionResult> Create(SystemConfigurationDto dto)
    {
        var configuration = new SystemConfiguration
        {
            Version = dto.Version,
            SyncStatus = dto.SyncStatus,
            LastSyncedAt = dto.LastSyncedAt
        };

        _context.SystemConfigurations.Add(configuration);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = configuration.Id }, configuration);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, SystemConfigurationDto dto)
    {
        var configuration = await _context.SystemConfigurations.FindAsync(id);
        if (configuration == null) return NotFound();

        configuration.Version = dto.Version;
        configuration.SyncStatus = dto.SyncStatus;
        configuration.LastSyncedAt = dto.LastSyncedAt;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var configuration = await _context.SystemConfigurations.FindAsync(id);
        if (configuration == null) return NotFound();

        _context.SystemConfigurations.Remove(configuration);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}