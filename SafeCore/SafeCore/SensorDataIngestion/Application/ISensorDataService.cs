using System;

namespace SafeCore.SensorDataIngestion.Application;

public class SensorReadingDto
{
    public Guid DeviceId { get; set; }
    public string SensorType { get; set; } = string.Empty;
    public decimal MeasuredValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
}