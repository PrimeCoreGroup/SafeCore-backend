using SafeCore.Shared.Kernel;

namespace SafeCore.MonitoringConfiguration.Domain;

/// <summary>Singleton configuration persisted in the application database.</summary>
public sealed class SafetyConfiguration : Entity
{
    public int SingletonKey { get; private set; } = 1;
    public string SystemName { get; set; } = "SafeCore";
    public string TimeZone { get; set; } = "America/Lima";
    public bool CriticalAlertsEnabled { get; set; } = true;
    public bool NotifyResponders { get; set; } = true;
    public bool ManualConfirmation { get; set; }
    public decimal TemperatureCelsius { get; set; } = 40m;
    public decimal GasAdcRiseCounts { get; set; } = 100m;
    public double AccelerationMagnitudeDeltaCounts { get; set; } = 200d;
    public int ImpactAlertMilliseconds { get; set; } = 1500;
}
