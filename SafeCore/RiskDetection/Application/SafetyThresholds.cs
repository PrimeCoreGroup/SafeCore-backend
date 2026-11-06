using SafeCore.Shared.Contracts;

namespace SafeCore.RiskDetection.Application;

public sealed record SafetyThresholds
{
    public const string SectionName = "SafetyThresholds";
    public decimal TemperatureCelsius { get; init; } = 40m;
    public decimal GasAdcRiseCounts { get; init; } = 100m;
    public double AccelerationMagnitudeDeltaCounts { get; init; } = 200d;
    public int ImpactAlertMilliseconds { get; init; } = 1500;
}

public interface ISafetyAssessmentService
{
    Task AssessAsync(SensorSignal reading, SensorSignal? previousReading, CancellationToken cancellationToken = default);
}
