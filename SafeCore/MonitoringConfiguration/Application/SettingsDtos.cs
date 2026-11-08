using System.ComponentModel.DataAnnotations;

namespace SafeCore.MonitoringConfiguration.Application;

public sealed class SafetyThresholdsDto
{
    [Range(typeof(decimal), "-54.999999", "125")] public decimal TemperatureCelsius { get; init; } = 40m;
    [Range(0.000001, 4095)] public decimal GasAdcRiseCounts { get; init; } = 100m;
    [Range(0.000001, 56755)] public double AccelerationMagnitudeDeltaCounts { get; init; } = 200d;
    [Range(100, 10000)] public int ImpactAlertMilliseconds { get; init; } = 1500;
}

public sealed class SystemSettingsDto
{
    [Required, StringLength(100, MinimumLength = 1)] public string SystemName { get; init; } = "SafeCore";
    [Required, StringLength(100)] public string TimeZone { get; init; } = "America/Lima";
    public bool CriticalAlertsEnabled { get; init; } = true;
    public bool NotifyResponders { get; init; } = true;
    public bool ManualConfirmation { get; init; }
}

public sealed record EffectiveSettingsResponse(
    string SystemName,
    string TimeZone,
    bool CriticalAlertsEnabled,
    bool NotifyResponders,
    bool ManualConfirmation,
    SafetyThresholdsDto Thresholds);
