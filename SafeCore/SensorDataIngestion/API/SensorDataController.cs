using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SafeCore.SensorDataIngestion.Application;
using SafeCore.SensorDataIngestion.Domain;
using SafeCore.DeviceManagement.Domain;
using SafeCore.Shared.Contracts;
using System.Security.Claims;
using SafeCore.RiskDetection.Application;
using SafeCore.Shared.Data;
using SafeCore.Locations.Application;
using Microsoft.EntityFrameworkCore;

namespace SafeCore.SensorDataIngestion.API;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SensorReadingsController : ControllerBase
{
    private readonly IRepository<SensorReading> _readings;
    private readonly IRepository<Device> _devices;
    private readonly ISensorReadingHistory _history;
    private readonly ISafetyAssessmentService _safetyAssessment;
    private readonly SafeCoreDbContext _db;
    private readonly LocationAccessService _access;
    public SensorReadingsController(IRepository<SensorReading> readings, IRepository<Device> devices, ISensorReadingHistory history, ISafetyAssessmentService safetyAssessment, SafeCoreDbContext db, LocationAccessService access)
    {
        _readings = readings;
        _devices = devices;
        _history = history;
        _safetyAssessment = safetyAssessment;
        _db = db; _access = access;
    }

    [HttpGet]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<IActionResult> GetAll(int page = 1, int pageSize = 100, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
    {
        if (from >= to && from is not null && to is not null) return BadRequest(new ProblemDetails { Title = "from must be earlier than to." });
        var deviceIds = await _access.DeviceIdsAsync(User, cancellationToken);
        var query = _db.SensorReadings.AsNoTracking().Where(item => deviceIds.Contains(item.DeviceId));
        if (from is not null) query = query.Where(item => item.CapturedAt >= from.Value.UtcDateTime);
        if (to is not null) query = query.Where(item => item.CapturedAt < to.Value.UtcDateTime);
        var safePage = Math.Max(page, 1); var safeSize = Math.Clamp(pageSize, 1, 250);
        var readings = await query.OrderByDescending(item => item.CreatedAt).Skip((int)Math.Min((long)(safePage - 1) * safeSize, int.MaxValue)).Take(safeSize).ToListAsync(cancellationToken);
        return Ok(readings);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var reading = await _readings.GetByIdAsync(id);
        if (reading == null) return NotFound();
        if (!(await _access.DeviceIdsAsync(User)).Contains(reading.DeviceId)) return Forbid();
        return Ok(reading);
    }

    [HttpPost]
    [Authorize(Policy = "DeviceOrOperator")]
    public async Task<IActionResult> Create(SensorReadingDto dto, CancellationToken cancellationToken)
    {
        var deviceClaim = User.FindFirstValue("device_id");
        if (deviceClaim is not null && (!Guid.TryParse(deviceClaim, out var authenticatedDeviceId) || authenticatedDeviceId != dto.DeviceId))
            return Forbid();
        var device = await _db.Devices.SingleOrDefaultAsync(device => device.Id == dto.DeviceId, cancellationToken);
        if (device is null)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.DeviceId)] = ["DeviceId does not identify a registered device."] }));
        if (deviceClaim is null && !User.IsInRole("Administrator") && !(await _access.DeviceIdsAsync(User, cancellationToken)).Contains(dto.DeviceId)) return Forbid();
        var component = dto.ComponentId is { } componentId
            ? await _db.DeviceComponents.SingleOrDefaultAsync(item => item.Id == componentId && item.DeviceId == dto.DeviceId && item.Kind == "Sensor", cancellationToken)
            : null;
        if (dto.ComponentId is not null && component is null)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.ComponentId)] = ["ComponentId must identify a sensor belonging to DeviceId."] }));
        if (component is not null && !ComponentMatchesSensorType(component.Type, dto.SensorType))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.ComponentId)] = ["ComponentId type does not match SensorType."] }));

        var capturedAtUtc = dto.CapturedAt.UtcDateTime;
        var previousReading = await _history.GetPreviousAsync(dto.DeviceId, dto.ComponentId, dto.SensorType, capturedAtUtc, cancellationToken);

        var reading = new SensorReading
        {
            DeviceId = dto.DeviceId,
            ComponentId = dto.ComponentId,
            SensorType = dto.SensorType,
            MeasuredValue = dto.MeasuredValue,
            AxisX = dto.AxisX,
            AxisY = dto.AxisY,
            AxisZ = dto.AxisZ,
            DigitalAlarm = dto.DigitalAlarm,
            Unit = dto.Unit,
            CapturedAt = capturedAtUtc
        };

        _readings.Add(reading);
        MarkDeviceOnline(device);
        if (component is not null)
        {
            component.LastSeenAt = DateTime.UtcNow;
            component.Status = dto.SensorType == "Temperature" && (dto.MeasuredValue is null or < -55 or > 125) ? "Fault" : component.Status == "Fault" ? "Ok" : component.Status;
        }
        await _readings.SaveChangesAsync(cancellationToken);
        await _safetyAssessment.AssessAsync(ToSignal(reading), previousReading is null ? null : ToSignal(previousReading), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = reading.Id }, reading);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> Update(Guid id, SensorReadingDto dto, CancellationToken cancellationToken)
    {
        var reading = await _readings.GetByIdAsync(id);
        if (reading == null) return NotFound();
        if (!(await _access.DeviceIdsAsync(User)).Contains(reading.DeviceId)) return Forbid();
        if (!await _devices.AnyAsync(device => device.Id == dto.DeviceId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.DeviceId)] = ["DeviceId does not identify a registered device."] }));
        if (!User.IsInRole("Administrator") && !(await _access.DeviceIdsAsync(User, cancellationToken)).Contains(dto.DeviceId)) return Forbid();
        if (dto.ComponentId is { } componentId && !await _db.DeviceComponents.AnyAsync(component => component.Id == componentId && component.DeviceId == dto.DeviceId && component.Kind == "Sensor", cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.ComponentId)] = ["ComponentId must identify a sensor belonging to DeviceId."] }));
        if (dto.ComponentId is { } checkedComponentId)
        {
            var componentType = await _db.DeviceComponents.Where(component => component.Id == checkedComponentId).Select(component => component.Type).SingleAsync(cancellationToken);
            if (!ComponentMatchesSensorType(componentType, dto.SensorType))
                return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.ComponentId)] = ["ComponentId type does not match SensorType."] }));
        }

        reading.DeviceId = dto.DeviceId;
        reading.ComponentId = dto.ComponentId;
        reading.SensorType = dto.SensorType;
        reading.MeasuredValue = dto.MeasuredValue;
        reading.AxisX = dto.AxisX;
        reading.AxisY = dto.AxisY;
        reading.AxisZ = dto.AxisZ;
        reading.DigitalAlarm = dto.DigitalAlarm;
        reading.Unit = dto.Unit;
        reading.CapturedAt = dto.CapturedAt.UtcDateTime;

        await _readings.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var reading = await _readings.GetByIdAsync(id);
        if (reading == null) return NotFound();

        _readings.Remove(reading);
        await _readings.SaveChangesAsync();
        return NoContent();
    }

    private static SensorSignal ToSignal(SensorReading reading) => new(
        reading.DeviceId,
        reading.ComponentId,
        reading.SensorType,
        reading.MeasuredValue,
        reading.AxisX,
        reading.AxisY,
        reading.AxisZ,
        reading.DigitalAlarm,
        reading.CapturedAt);

    private void MarkDeviceOnline(Device device)
    {
        device.LastSeenAt = DateTime.UtcNow;
        if (device.ConnectivityStatus == "Online") return;
        device.ConnectivityStatus = "Online";
        _db.DeviceConnectivityEvents.Add(new SafeCore.DeviceManagement.Domain.DeviceConnectivityEvent { DeviceId = device.Id, ConnectivityStatus = "Online" });
    }

    private static bool ComponentMatchesSensorType(string componentType, string sensorType)
    {
        var value = componentType.Trim().ToLowerInvariant();
        return sensorType switch
        {
            "Gas" => value.Contains("gas") || value.Contains("mq"),
            "Temperature" => value.Contains("temp") || value.Contains("ds18"),
            "Accelerometer" => value.Contains("accel") || value.Contains("motion") || value.Contains("movement") || value.Contains("mpu6050"),
            "Gyroscope" => value.Contains("gyro") || value.Contains("mpu6050"),
            _ => string.Equals(componentType, sensorType, StringComparison.OrdinalIgnoreCase)
        };
    }
}
