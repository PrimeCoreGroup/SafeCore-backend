namespace SafeCore.Shared.Contracts;

public sealed record SensorSignal(
    Guid DeviceId,
    string SensorType,
    decimal? MeasuredValue,
    decimal? AxisX,
    decimal? AxisY,
    decimal? AxisZ,
    bool? DigitalAlarm,
    DateTime CapturedAtUtc);
