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
    public SensorReadingsController(IRepository<SensorReading> readings, IRepository<Device> devices, ISensorReadingHistory history, ISafetyAssessmentService safetyAssessment)
    {
        _readings = readings;
        _devices = devices;
        _history = history;
        _safetyAssessment = safetyAssessment;
    }

    [HttpGet]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<IActionResult> GetAll(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var readings = await _readings.ListAsync(page, pageSize, cancellationToken);
        return Ok(readings);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var reading = await _readings.GetByIdAsync(id);
        if (reading == null) return NotFound();
        return Ok(reading);
    }

    [HttpPost]
    [Authorize(Policy = "DeviceOrOperator")]
    public async Task<IActionResult> Create(SensorReadingDto dto, CancellationToken cancellationToken)
    {
        var deviceClaim = User.FindFirstValue("device_id");
        if (deviceClaim is not null && (!Guid.TryParse(deviceClaim, out var authenticatedDeviceId) || authenticatedDeviceId != dto.DeviceId))
            return Forbid();
        if (!await _devices.AnyAsync(device => device.Id == dto.DeviceId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.DeviceId)] = ["DeviceId does not identify a registered device."] }));

        var capturedAtUtc = dto.CapturedAt.UtcDateTime;
        var previousReading = await _history.GetPreviousAsync(dto.DeviceId, dto.SensorType, capturedAtUtc, cancellationToken);

        var reading = new SensorReading
        {
            DeviceId = dto.DeviceId,
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
        if (!await _devices.AnyAsync(device => device.Id == dto.DeviceId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.DeviceId)] = ["DeviceId does not identify a registered device."] }));

        reading.DeviceId = dto.DeviceId;
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
        reading.SensorType,
        reading.MeasuredValue,
        reading.AxisX,
        reading.AxisY,
        reading.AxisZ,
        reading.DigitalAlarm,
        reading.CapturedAt);
}
