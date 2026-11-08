using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeCore.MonitoringConfiguration.Application;
using SafeCore.MonitoringConfiguration.Domain;

namespace SafeCore.MonitoringConfiguration.API;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController(SafetyConfigurationStore store) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<ActionResult<EffectiveSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var item = await store.GetAsync(cancellationToken);
        return Ok(ToResponse(item));
    }

    [HttpPut]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<EffectiveSettingsResponse>> Update(SystemSettingsDto dto, CancellationToken cancellationToken)
    {
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(dto.TimeZone); }
        catch (TimeZoneNotFoundException) { ModelState.AddModelError(nameof(dto.TimeZone), "TimeZone must be a valid IANA or system time zone identifier."); return ValidationProblem(ModelState); }
        catch (InvalidTimeZoneException) { ModelState.AddModelError(nameof(dto.TimeZone), "TimeZone identifier is invalid."); return ValidationProblem(ModelState); }
        var item = await store.GetAsync(cancellationToken);
        item.SystemName = dto.SystemName.Trim();
        item.TimeZone = dto.TimeZone.Trim();
        item.CriticalAlertsEnabled = dto.CriticalAlertsEnabled;
        item.NotifyResponders = dto.NotifyResponders;
        item.ManualConfirmation = dto.ManualConfirmation;
        await store.SaveAsync(cancellationToken);
        return Ok(ToResponse(item));
    }

    [HttpGet("thresholds")]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<ActionResult<SafetyThresholdsDto>> GetThresholds(CancellationToken cancellationToken)
    {
        var item = await store.GetAsync(cancellationToken);
        return Ok(ToThresholds(item));
    }

    [HttpPut("thresholds")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SafetyThresholdsDto>> UpdateThresholds(SafetyThresholdsDto dto, CancellationToken cancellationToken)
    {
        var item = await store.GetAsync(cancellationToken);
        item.TemperatureCelsius = dto.TemperatureCelsius;
        item.GasAdcRiseCounts = dto.GasAdcRiseCounts;
        item.AccelerationMagnitudeDeltaCounts = dto.AccelerationMagnitudeDeltaCounts;
        item.ImpactAlertMilliseconds = dto.ImpactAlertMilliseconds;
        await store.SaveAsync(cancellationToken);
        return Ok(ToThresholds(item));
    }

    private static SafetyThresholdsDto ToThresholds(SafetyConfiguration item) => new()
    {
        TemperatureCelsius = item.TemperatureCelsius,
        GasAdcRiseCounts = item.GasAdcRiseCounts,
        AccelerationMagnitudeDeltaCounts = item.AccelerationMagnitudeDeltaCounts,
        ImpactAlertMilliseconds = item.ImpactAlertMilliseconds
    };

    private static EffectiveSettingsResponse ToResponse(SafetyConfiguration item) => new(
        item.SystemName, item.TimeZone, item.CriticalAlertsEnabled, item.NotifyResponders, item.ManualConfirmation, ToThresholds(item));
}
