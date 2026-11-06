using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeCore.DeviceManagement.Application;
using SafeCore.DeviceManagement.Domain;
using SafeCore.SensorDataIngestion.Domain;
using SafeCore.Shared.Contracts;

namespace SafeCore.DeviceManagement.API;

[ApiController]
[Route("api/devices")]
public sealed class DevicesController(IRepository<Device> devices, IRepository<SensorReading> readings) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<ActionResult<IReadOnlyCollection<DeviceResponse>>> GetAll(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var items = await devices.ListAsync(page, pageSize, cancellationToken);
        return Ok(items.OrderBy(device => device.SerialNumber).Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<ActionResult<DeviceResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var device = await devices.GetByIdAsync(id, cancellationToken);
        return device is null ? NotFound() : Ok(ToResponse(device));
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ProvisionedDeviceResponse>> Create(DeviceDto dto, CancellationToken cancellationToken)
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var device = new Device
        {
            SerialNumber = dto.SerialNumber.Trim(),
            Name = dto.Name.Trim(),
            Category = dto.Category.Trim(),
            ConnectivityStatus = dto.ConnectivityStatus.Trim(),
            CredentialHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)))
        };

        if (await devices.AnyAsync(item => item.SerialNumber == device.SerialNumber, cancellationToken))
            return Conflict(new ProblemDetails { Title = "A device with this serial number already exists." });
        devices.Add(device);
        await devices.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = device.Id }, new ProvisionedDeviceResponse(ToResponse(device), secret));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Update(Guid id, DeviceDto dto, CancellationToken cancellationToken)
    {
        var device = await devices.GetByIdAsync(id, cancellationToken);
        if (device is null)
            return NotFound();

        device.SerialNumber = dto.SerialNumber.Trim();
        device.Name = dto.Name.Trim();
        device.Category = dto.Category.Trim();
        device.ConnectivityStatus = dto.ConnectivityStatus.Trim();
        if (await devices.AnyAsync(item => item.Id != id && item.SerialNumber == device.SerialNumber, cancellationToken))
            return Conflict(new ProblemDetails { Title = "A device with this serial number already exists." });
        await devices.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var device = await devices.GetByIdAsync(id, cancellationToken);
        if (device is null)
            return NotFound();
        if (await readings.AnyAsync(reading => reading.DeviceId == id, cancellationToken))
            return Conflict(new ProblemDetails { Title = "A device with recorded sensor readings cannot be deleted." });

        devices.Remove(device);
        await devices.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/rotate-secret")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ProvisionedDeviceResponse>> RotateSecret(Guid id, CancellationToken cancellationToken)
    {
        var device = await devices.GetByIdAsync(id, cancellationToken);
        if (device is null)
            return NotFound();

        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        device.CredentialHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        await devices.SaveChangesAsync(cancellationToken);
        return Ok(new ProvisionedDeviceResponse(ToResponse(device), secret));
    }

    private static DeviceResponse ToResponse(Device device) => new(device.Id, device.SerialNumber, device.Name, device.Category, device.ConnectivityStatus, device.CreatedAt);
}
