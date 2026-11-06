using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SafeCore.MonitoringConfiguration.Application;
using SafeCore.MonitoringConfiguration.Domain;
using SafeCore.Shared.Contracts;

namespace SafeCore.MonitoringConfiguration.API;

[ApiController]
[Authorize(Roles = "Administrator,Operator")]
[Route("api/[controller]")]
public class SystemConfigurationsController : ControllerBase
{
    private readonly IRepository<SystemConfiguration> _configurations;
    public SystemConfigurationsController(IRepository<SystemConfiguration> configurations) => _configurations = configurations;

    [HttpGet]
    public async Task<IActionResult> GetAll(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var configurations = await _configurations.ListAsync(page, pageSize, cancellationToken);
        return Ok(configurations);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var configuration = await _configurations.GetByIdAsync(id);
        if (configuration == null) return NotFound();
        return Ok(configuration);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Create(SystemConfigurationDto dto)
    {
        var configuration = new SystemConfiguration
        {
            Version = dto.Version,
            SyncStatus = dto.SyncStatus,
            LastSyncedAt = dto.LastSyncedAt
        };

        _configurations.Add(configuration);
        await _configurations.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = configuration.Id }, configuration);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Update(Guid id, SystemConfigurationDto dto)
    {
        var configuration = await _configurations.GetByIdAsync(id);
        if (configuration == null) return NotFound();

        configuration.Version = dto.Version;
        configuration.SyncStatus = dto.SyncStatus;
        configuration.LastSyncedAt = dto.LastSyncedAt;

        await _configurations.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var configuration = await _configurations.GetByIdAsync(id);
        if (configuration == null) return NotFound();

        _configurations.Remove(configuration);
        await _configurations.SaveChangesAsync();
        return NoContent();
    }
}
