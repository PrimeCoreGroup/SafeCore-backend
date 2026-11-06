using SafeCore.SensorDataIngestion.Domain;

namespace SafeCore.SensorDataIngestion.Application;

public interface ISensorReadingHistory
{
    Task<SensorReading?> GetPreviousAsync(Guid deviceId, string sensorType, DateTime capturedAtUtc, CancellationToken cancellationToken = default);
}
