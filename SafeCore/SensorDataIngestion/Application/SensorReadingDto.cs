using System.ComponentModel.DataAnnotations;

namespace SafeCore.SensorDataIngestion.Application;

public sealed class SensorReadingDto : IValidatableObject
{
    [Required] public Guid DeviceId { get; set; }
    public Guid? ComponentId { get; set; }
    [Required, RegularExpression("^(Accelerometer|Gyroscope|Gas|Temperature)$")] public string SensorType { get; set; } = string.Empty;
    public decimal? MeasuredValue { get; set; }
    public decimal? AxisX { get; set; }
    public decimal? AxisY { get; set; }
    public decimal? AxisZ { get; set; }
    public bool? DigitalAlarm { get; set; }
    [Required, StringLength(30)] public string Unit { get; set; } = string.Empty;
    [Required] public DateTimeOffset CapturedAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DeviceId == Guid.Empty)
            yield return new ValidationResult("DeviceId is required.", [nameof(DeviceId)]);
        if (CapturedAt == default)
            yield return new ValidationResult("CapturedAt is required.", [nameof(CapturedAt)]);

        var usesAxes = string.Equals(SensorType, "Accelerometer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(SensorType, "Gyroscope", StringComparison.OrdinalIgnoreCase);
        var axes = new[] { AxisX, AxisY, AxisZ };
        if (usesAxes && (MeasuredValue.HasValue || axes.Any(value => !value.HasValue) || DigitalAlarm.HasValue))
            yield return new ValidationResult("Accelerometer and gyroscope readings require all three axes and no scalar or digital-alarm value.", [nameof(MeasuredValue), nameof(AxisX), nameof(AxisY), nameof(AxisZ), nameof(DigitalAlarm)]);
        if (usesAxes && axes.Any(value => value is < -32768 or > 32767))
            yield return new ValidationResult("MPU6050 raw axes must fit its signed 16-bit output range.", [nameof(AxisX), nameof(AxisY), nameof(AxisZ)]);
        if (!usesAxes && (!MeasuredValue.HasValue || axes.Any(value => value.HasValue)))
            yield return new ValidationResult("Gas and temperature readings require one scalar value and no axis values.", [nameof(MeasuredValue), nameof(AxisX), nameof(AxisY), nameof(AxisZ)]);
        if (SensorType == "Gas" && !DigitalAlarm.HasValue)
            yield return new ValidationResult("Gas readings must include the MQ-2 digital alarm state (true when its active-low output is LOW).", [nameof(DigitalAlarm)]);
        if (SensorType == "Temperature" && DigitalAlarm.HasValue)
            yield return new ValidationResult("Temperature readings do not include a digital-alarm value.", [nameof(DigitalAlarm)]);
        if (SensorType == "Gas" && MeasuredValue is < 0 or > 4095)
            yield return new ValidationResult("MQ-2 ADC counts must be in the ESP32 12-bit range 0-4095.", [nameof(MeasuredValue)]);
        if (SensorType == "Temperature" && MeasuredValue is < -127 or > 125)
            yield return new ValidationResult("The DS18B20 value must be in its supported range or use -127 to report a disconnected sensor.", [nameof(MeasuredValue)]);
        if (CapturedAt > DateTimeOffset.UtcNow.AddMinutes(5))
            yield return new ValidationResult("CapturedAt cannot be more than five minutes in the future.", [nameof(CapturedAt)]);
    }
}

public sealed record SensorReadingResponse(
    Guid Id,
    Guid DeviceId,
    Guid? ComponentId,
    string SensorType,
    decimal? MeasuredValue,
    decimal? AxisX,
    decimal? AxisY,
    decimal? AxisZ,
    bool? DigitalAlarm,
    string Unit,
    DateTime CapturedAt,
    DateTime CreatedAt);
