using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeCore.DeviceManagement.Application;
using SafeCore.DeviceManagement.Domain;
using SafeCore.SensorDataIngestion.Domain;
using SafeCore.Shared.Contracts;
using SafeCore.Shared.Data;
using SafeCore.Locations.Application;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SafeCore.IdentityAccess.Domain;

namespace SafeCore.DeviceManagement.API;

[ApiController]
[Route("api/devices")]
public sealed class DevicesController(IRepository<Device> devices, IRepository<SensorReading> readings, SafeCoreDbContext db, LocationAccessService access) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<ActionResult<IReadOnlyCollection<DeviceResponse>>> GetAll(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var allowed = await access.DeviceIdsAsync(User, cancellationToken);
        var safePage = Math.Max(page, 1); var safeSize = Math.Clamp(pageSize, 1, 250);
        var offset = (int)Math.Min((long)(safePage - 1) * safeSize, int.MaxValue);
        var items = await db.Devices.AsNoTracking().Where(device => allowed.Contains(device.Id)).OrderBy(device => device.SerialNumber)
            .Skip(offset).Take(safeSize).ToListAsync(cancellationToken);
        return Ok(await ToResponsesAsync(items, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<ActionResult<DeviceResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!(await access.DeviceIdsAsync(User, cancellationToken)).Contains(id)) return Forbid();
        var device = await db.Devices.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return device is null ? NotFound() : Ok((await ToResponsesAsync([device], cancellationToken))[0]);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<ActionResult<ProvisionedDeviceResponse>> Create(DeviceDto dto, CancellationToken cancellationToken)
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var device = new Device
        {
            SerialNumber = dto.SerialNumber.Trim(),
            Name = dto.Name.Trim(),
            Category = dto.Category.Trim(),
            ConnectivityStatus = dto.ConnectivityStatus.Trim(),
            CredentialHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))),
            CreatedByUserId = LocationAccessService.UserId(User)
        };

        if (await devices.AnyAsync(item => item.SerialNumber == device.SerialNumber, cancellationToken))
            return Conflict(new ProblemDetails { Title = "A device with this serial number already exists." });
        db.Devices.Add(device);
        db.DeviceConnectivityEvents.Add(new DeviceConnectivityEvent { DeviceId = device.Id, ConnectivityStatus = device.ConnectivityStatus });
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = device.Id }, new ProvisionedDeviceResponse((await ToResponsesAsync([device], cancellationToken))[0], secret));
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
        var previousStatus = device.ConnectivityStatus;
        device.ConnectivityStatus = dto.ConnectivityStatus.Trim();
        if (await devices.AnyAsync(item => item.Id != id && item.SerialNumber == device.SerialNumber, cancellationToken))
            return Conflict(new ProblemDetails { Title = "A device with this serial number already exists." });
        if (previousStatus != device.ConnectivityStatus)
        {
            device.LastSeenAt = DateTime.UtcNow;
            db.DeviceConnectivityEvents.Add(new DeviceConnectivityEvent { DeviceId = id, ConnectivityStatus = device.ConnectivityStatus });
        }
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var device = await devices.GetByIdAsync(id, cancellationToken);
        if (device is null)
            return NotFound();
        if (await readings.AnyAsync(reading => reading.DeviceId == id, cancellationToken)
            || await db.RiskSituations.AnyAsync(risk => risk.DeviceId == id, cancellationToken))
            return Conflict(new ProblemDetails { Title = "A device with recorded sensor readings or risks cannot be deleted." });

        if (await db.DeviceComponents.AnyAsync(component => component.DeviceId == id, cancellationToken)
            || await db.DeviceCommands.AnyAsync(item => item.DeviceId == id, cancellationToken))
            return Conflict(new ProblemDetails { Title = "A device with components or queued test commands cannot be deleted; disable it instead." });
        db.Devices.Remove(device);
        await db.SaveChangesAsync(cancellationToken);
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
        return Ok(new ProvisionedDeviceResponse((await ToResponsesAsync([device], cancellationToken))[0], secret));
    }

    [HttpPut("{id:guid}/zone")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> AssignZone(Guid id, DeviceZoneDto dto, CancellationToken ct)
    {
        var device = await devices.GetByIdAsync(id, ct);
        if (device is null) return NotFound();
        if (dto.ZoneId is { } zoneId)
        {
            var zone = await db.Zones.AsNoTracking().SingleOrDefaultAsync(item => item.Id == zoneId, ct);
            if (zone is null) return BadRequest(new { errors = new[] { "ZoneId does not identify a registered zone." } });
            if (!User.IsInRole("Administrator") && !(await access.ZoneIdsAsync(User, ct)).Contains(zoneId)) return Forbid();
        }
        device.ZoneId = dto.ZoneId;
        await db.SaveChangesAsync(ct);
        return Ok((await ToResponsesAsync([device], ct))[0]);
    }

    [HttpGet("{id:guid}/components")]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<IActionResult> GetComponents(Guid id, CancellationToken ct)
    {
        if (!(await access.DeviceIdsAsync(User, ct)).Contains(id)) return Forbid();
        if (!await db.Devices.AnyAsync(item => item.Id == id, ct)) return NotFound();
        return Ok(await db.DeviceComponents.AsNoTracking().Where(item => item.DeviceId == id).OrderBy(item => item.Kind).ThenBy(item => item.Label).ToListAsync(ct));
    }

    [HttpGet("{id:guid}/components/{componentId:guid}")]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<IActionResult> GetComponent(Guid id, Guid componentId, CancellationToken ct)
    {
        if (!(await access.DeviceIdsAsync(User, ct)).Contains(id)) return Forbid();
        var component = await db.DeviceComponents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == componentId && item.DeviceId == id, ct);
        return component is null ? NotFound() : Ok(component);
    }

    [HttpPost("{id:guid}/components")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> CreateComponent(Guid id, DeviceComponentDto dto, CancellationToken ct)
    {
        if (!await CanManageDeviceAsync(id, ct)) return NotFound();
        var channel = dto.Channel is null ? null : dto.Channel.Trim();
        if (await db.DeviceComponents.AnyAsync(item => item.DeviceId == id && item.Kind == dto.Kind && item.Type == dto.Type.Trim() && item.Channel == channel, ct))
            return Conflict(new ProblemDetails { Title = "This component is already registered on the device." });
        var component = new DeviceComponent { DeviceId = id, Kind = dto.Kind, Type = dto.Type.Trim(), Label = dto.Label.Trim(), Channel = channel, Status = dto.Status, InstalledAt = dto.InstalledAt, ReplacedAt = dto.ReplacedAt };
        db.DeviceComponents.Add(component);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict(new ProblemDetails { Title = "This component is already registered on the device." }); }
        return CreatedAtAction(nameof(GetComponents), new { id }, component);
    }

    [HttpPut("{id:guid}/components/{componentId:guid}")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> UpdateComponent(Guid id, Guid componentId, DeviceComponentDto dto, CancellationToken ct)
    {
        if (!await CanManageDeviceAsync(id, ct)) return NotFound();
        var component = await db.DeviceComponents.SingleOrDefaultAsync(item => item.Id == componentId && item.DeviceId == id, ct);
        if (component is null) return NotFound();
        var channel = dto.Channel is null ? null : dto.Channel.Trim();
        if (await db.DeviceComponents.AnyAsync(item => item.Id != componentId && item.DeviceId == id && item.Kind == dto.Kind && item.Type == dto.Type.Trim() && item.Channel == channel, ct))
            return Conflict(new ProblemDetails { Title = "This component is already registered on the device." });
        component.Kind = dto.Kind; component.Type = dto.Type.Trim(); component.Label = dto.Label.Trim(); component.Channel = channel;
        component.Status = dto.Status; component.InstalledAt = dto.InstalledAt; component.ReplacedAt = dto.ReplacedAt;
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict(new ProblemDetails { Title = "This component is already registered on the device." }); }
        return NoContent();
    }

    [HttpDelete("{id:guid}/components/{componentId:guid}")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> DeleteComponent(Guid id, Guid componentId, CancellationToken ct)
    {
        if (!await CanManageDeviceAsync(id, ct)) return NotFound();
        var component = await db.DeviceComponents.SingleOrDefaultAsync(item => item.Id == componentId && item.DeviceId == id, ct);
        if (component is null) return NotFound();
        if (await db.SensorReadings.AnyAsync(item => item.ComponentId == componentId, ct)
            || await db.RiskSituations.AnyAsync(item => item.ComponentId == componentId, ct)
            || await db.ResponseActions.AnyAsync(item => item.ComponentId == componentId, ct)
            || await db.DeviceCommands.AnyAsync(item => item.ComponentId == componentId, ct))
            return Conflict(new ProblemDetails { Title = "A component with recorded readings, actions, or tests cannot be deleted; disable it instead." });
        db.DeviceComponents.Remove(component); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPut("{id:guid}/manifest")]
    [Authorize]
    public async Task<IActionResult> ApplyManifest(Guid id, DeviceManifestDto dto, CancellationToken ct)
    {
        if (!IsDeviceTokenFor(id)) return Forbid();
        var device = await db.Devices.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (device is null) return NotFound();
        if (dto.Components.Count > 100) return BadRequest(new { errors = new[] { "A device manifest cannot contain more than 100 components." } });
        var existing = await db.DeviceComponents.Where(item => item.DeviceId == id).ToListAsync(ct);
        foreach (var input in dto.Components)
        {
            var component = existing.FirstOrDefault(item => item.Kind == input.Kind && item.Type == input.Type && item.Channel == input.Channel);
            if (component is null)
            {
                component = new DeviceComponent { DeviceId = id, Kind = input.Kind, Type = input.Type.Trim(), Channel = input.Channel?.Trim(), Label = input.Label.Trim(), Status = "Disconnected", InstalledAt = input.InstalledAt ?? DateTime.UtcNow };
                db.DeviceComponents.Add(component); existing.Add(component);
            }
            else { component.Label = input.Label.Trim(); component.Channel = input.Channel?.Trim(); component.Type = input.Type.Trim(); if (component.Status == "Disabled") component.Status = "Disconnected"; }
        }
        var declaredKeys = dto.Components.Select(input => (input.Kind, Type: input.Type.Trim(), Channel: input.Channel?.Trim())).ToHashSet();
        foreach (var component in existing.Where(item => !declaredKeys.Contains((item.Kind, item.Type, item.Channel)))) component.Status = "Disabled";
        await MarkDeviceSeenAsync(device, ct);
        await db.SaveChangesAsync(ct);
        return Ok(existing.Select(ToComponentResponse));
    }

    [HttpPost("{id:guid}/health")]
    [Authorize]
    public async Task<IActionResult> ReportHealth(Guid id, DeviceHealthDto dto, CancellationToken ct)
    {
        if (!IsDeviceTokenFor(id)) return Forbid();
        var device = await db.Devices.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (device is null) return NotFound();
        var ids = dto.Components.Select(item => item.ComponentId).Distinct().ToArray();
        var components = await db.DeviceComponents.Where(item => item.DeviceId == id && ids.Contains(item.Id)).ToListAsync(ct);
        if (components.Count != ids.Length) return BadRequest(new { errors = new[] { "Every ComponentId must belong to this device." } });
        var now = DateTime.UtcNow;
        foreach (var input in dto.Components)
        {
            var component = components.Single(item => item.Id == input.ComponentId);
            component.Status = input.Status; component.LastSeenAt = now;
        }
        await MarkDeviceSeenAsync(device, ct);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/components/{componentId:guid}/test")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> TestComponent(Guid id, Guid componentId, CancellationToken ct)
    {
        if (!await CanManageDeviceAsync(id, ct)) return NotFound();
        var component = await db.DeviceComponents.SingleOrDefaultAsync(item => item.Id == componentId && item.DeviceId == id, ct);
        if (component is null) return NotFound();
        if (component.Kind != "Actuator") return BadRequest(new ProblemDetails { Title = "Only actuator components can be tested." });
        var device = await db.Devices.FindAsync([id], ct);
        if (device!.ConnectivityStatus != "Online") return Conflict(new ProblemDetails { Title = "The device must be online to queue a component test." });
        var command = new DeviceCommand { DeviceId = id, ComponentId = componentId, CommandType = "Test", Status = "Pending" };
        db.DeviceCommands.Add(command); await db.SaveChangesAsync(ct);
        return AcceptedAtAction(nameof(GetCommand), new { id, commandId = command.Id }, new ComponentTestResponse(command.Id, componentId, "Pending", "Test queued. The device must poll its pending commands and report the result.", command.RequestedAt));
    }

    [HttpGet("{id:guid}/commands/pending")]
    [Authorize]
    public async Task<IActionResult> GetPendingCommands(Guid id, CancellationToken ct)
    {
        if (!IsDeviceTokenFor(id)) return Forbid();
        var device = await db.Devices.FindAsync([id], ct);
        if (device is null) return NotFound();
        await MarkDeviceSeenAsync(device, ct);
        await db.SaveChangesAsync(ct);
        return Ok(await db.DeviceCommands.AsNoTracking().Where(item => item.DeviceId == id && item.Status == "Pending").OrderBy(item => item.RequestedAt).ToListAsync(ct));
    }

    [HttpPost("{id:guid}/commands/{commandId:guid}/result")]
    [Authorize]
    public async Task<IActionResult> ReportCommandResult(Guid id, Guid commandId, DeviceCommandResultDto dto, CancellationToken ct)
    {
        if (!IsDeviceTokenFor(id)) return Forbid();
        var command = await db.DeviceCommands.SingleOrDefaultAsync(item => item.Id == commandId && item.DeviceId == id, ct);
        if (command is null) return NotFound();
        if (command.Status != "Pending") return Conflict(new ProblemDetails { Title = "The command has already been completed." });
        command.Status = dto.Status; command.Result = dto.Result?.Trim(); command.CompletedAt = DateTime.UtcNow;
        var component = await db.DeviceComponents.SingleOrDefaultAsync(item => item.Id == command.ComponentId && item.DeviceId == id, ct);
        if (component is not null) { component.Status = dto.Status == "Completed" ? "Ok" : "Fault"; component.LastSeenAt = command.CompletedAt; }
        var device = await db.Devices.FindAsync([id], ct);
        if (device is not null) await MarkDeviceSeenAsync(device, ct);
        await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpGet("{id:guid}/commands/{commandId:guid}")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> GetCommand(Guid id, Guid commandId, CancellationToken ct)
    {
        if (!await CanManageDeviceAsync(id, ct)) return NotFound();
        var command = await db.DeviceCommands.AsNoTracking().SingleOrDefaultAsync(item => item.Id == commandId && item.DeviceId == id, ct);
        return command is null ? NotFound() : Ok(command);
    }

    private bool IsDeviceTokenFor(Guid id) => Guid.TryParse(User.FindFirstValue("device_id"), out var authenticatedDeviceId) && authenticatedDeviceId == id;

    private async Task<bool> CanManageDeviceAsync(Guid id, CancellationToken ct) =>
        (await access.DeviceIdsAsync(User, ct)).Contains(id) && await db.Devices.AnyAsync(item => item.Id == id, ct);

    private async Task MarkDeviceSeenAsync(Device device, CancellationToken ct)
    {
        device.LastSeenAt = DateTime.UtcNow;
        if (device.ConnectivityStatus != "Online")
        {
            device.ConnectivityStatus = "Online";
            db.DeviceConnectivityEvents.Add(new DeviceConnectivityEvent { DeviceId = device.Id, ConnectivityStatus = "Online" });
        }
    }

    private static DeviceComponentResponse ToComponentResponse(DeviceComponent item) =>
        new(item.Id, item.DeviceId, item.Kind, item.Type, item.Label, item.Channel, item.Status, item.LastSeenAt, item.InstalledAt, item.ReplacedAt);

    private async Task<IReadOnlyList<DeviceResponse>> ToResponsesAsync(IReadOnlyCollection<Device> items, CancellationToken ct)
    {
        var zoneIds = items.Where(item => item.ZoneId.HasValue).Select(item => item.ZoneId!.Value).Distinct().ToArray();
        var zones = await db.Zones.AsNoTracking().Where(item => zoneIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, ct);
        var propertyIds = zones.Values.Select(item => item.PropertyId).Distinct().ToArray();
        var properties = await db.Properties.AsNoTracking().Where(item => propertyIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, ct);
        var userIds = items.Where(item => item.CreatedByUserId.HasValue).Select(item => item.CreatedByUserId!.Value).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(item => userIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, ct);
        return items.Select(device =>
        {
            zones.TryGetValue(device.ZoneId ?? Guid.Empty, out var zone);
            properties.TryGetValue(zone?.PropertyId ?? Guid.Empty, out var property);
            users.TryGetValue(device.CreatedByUserId ?? Guid.Empty, out var user);
            return new DeviceResponse(device.Id, device.SerialNumber, device.Name, device.Category, device.ConnectivityStatus, device.CreatedAt,
                zone?.Id, zone?.Name, property?.Id, property?.Name, device.CreatedByUserId,
                user is null ? null : $"{user.FirstName} {user.LastName}".Trim(), device.LastSeenAt);
        }).ToArray();
    }

}
