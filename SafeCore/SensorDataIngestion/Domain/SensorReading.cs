using System;
using SafeCore.Shared.Kernel;

namespace SafeCore.SensorDataIngestion.Domain;

public class SensorReading : Entity
{
    public Guid DeviceId { get; set; }
    public string SensorType { get; set; } = string.Empty;
    public decimal? MeasuredValue { get; set; }
    public decimal? AxisX { get; set; }
    public decimal? AxisY { get; set; }
    public decimal? AxisZ { get; set; }
    public bool? DigitalAlarm { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
}
