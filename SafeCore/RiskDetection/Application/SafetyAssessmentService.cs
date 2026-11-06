using Microsoft.Extensions.Options;
using SafeCore.RiskDetection.Domain;
using SafeCore.Shared.Contracts;

namespace SafeCore.RiskDetection.Application;

public sealed class SafetyAssessmentService(IRepository<RiskSituation> risks, IOptions<SafetyThresholds> options) : ISafetyAssessmentService
{
    private readonly SafetyThresholds _thresholds = options.Value;

    public async Task AssessAsync(SensorSignal reading, SensorSignal? previousReading, CancellationToken cancellationToken = default)
    {
        var riskType = DetermineRisk(reading, previousReading);
        if (riskType is null)
        {
            await ResolveClearedRisksAsync(reading, cancellationToken);
            return;
        }

        DateTime? alertUntil = riskType == "Earthquake"
            ? reading.CapturedAtUtc.AddMilliseconds(_thresholds.ImpactAlertMilliseconds)
            : null;
        var openRisks = await risks.ListWhereAsync(
            risk => risk.DeviceId == reading.DeviceId && risk.Origin == "Sensor" && risk.RiskType == riskType && risk.Status != "Resolved",
            page: 1,
            pageSize: 250,
            cancellationToken);

        if (riskType == "Earthquake")
        {
            var currentAlert = openRisks.FirstOrDefault(risk => risk.AlertUntil > reading.CapturedAtUtc);
            if (currentAlert is not null)
            {
                currentAlert.AlertUntil = alertUntil;
                await risks.SaveChangesAsync(cancellationToken);
                return;
            }
        }
        else if (openRisks.Count > 0)
        {
            return;
        }

        risks.Add(new RiskSituation
        {
            DeviceId = reading.DeviceId,
            Origin = "Sensor",
            RiskType = riskType,
            Status = "Open",
            Severity = "Critical",
            DetectedAt = reading.CapturedAtUtc,
            AlertUntil = alertUntil
        });
        await risks.SaveChangesAsync(cancellationToken);
    }

    private async Task ResolveClearedRisksAsync(SensorSignal reading, CancellationToken cancellationToken)
    {
        var typesToResolve = reading.SensorType switch
        {
            "Gas" => new[] { "GasOrSmoke" },
            "Temperature" => new[] { "Fire", "SensorFault" },
            "Accelerometer" => new[] { "Earthquake" },
            _ => Array.Empty<string>()
        };
        if (typesToResolve.Length == 0)
            return;

        var activeRisks = await risks.ListWhereAsync(
            risk => risk.DeviceId == reading.DeviceId && risk.Origin == "Sensor" && risk.Status != "Resolved" && typesToResolve.Contains(risk.RiskType),
            page: 1,
            pageSize: 250,
            cancellationToken);

        var changed = false;
        foreach (var risk in activeRisks)
        {
            if (risk.RiskType == "Earthquake" && risk.AlertUntil > reading.CapturedAtUtc)
                continue;
            risk.Status = "Resolved";
            changed = true;
        }
        if (changed)
            await risks.SaveChangesAsync(cancellationToken);
    }

    private string? DetermineRisk(SensorSignal reading, SensorSignal? previousReading)
    {
        if (reading.SensorType == "Temperature")
        {
            if (reading.MeasuredValue is null or < -55 or > 125)
                return "SensorFault";
            return reading.MeasuredValue >= _thresholds.TemperatureCelsius ? "Fire" : null;
        }

        if (reading.SensorType == "Gas")
        {
            var digitalAlarmActive = reading.DigitalAlarm == true;
            var rapidRise = previousReading?.MeasuredValue is { } previousValue
                && reading.MeasuredValue is { } currentValue
                && currentValue - previousValue > _thresholds.GasAdcRiseCounts;
            return digitalAlarmActive || rapidRise ? "GasOrSmoke" : null;
        }

        if (reading.SensorType == "Accelerometer" && previousReading is not null
            && reading.AxisX is { } x && reading.AxisY is { } y && reading.AxisZ is { } z
            && previousReading.AxisX is { } previousX && previousReading.AxisY is { } previousY && previousReading.AxisZ is { } previousZ)
        {
            var currentMagnitude = Math.Sqrt((double)(x * x + y * y + z * z));
            var previousMagnitude = Math.Sqrt((double)(previousX * previousX + previousY * previousY + previousZ * previousZ));
            return Math.Abs(currentMagnitude - previousMagnitude) > _thresholds.AccelerationMagnitudeDeltaCounts
                ? "Earthquake"
                : null;
        }

        return null;
    }
}
