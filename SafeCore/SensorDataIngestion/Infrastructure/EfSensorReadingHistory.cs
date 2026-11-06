using Microsoft.EntityFrameworkCore;
using SafeCore.SensorDataIngestion.Application;
using SafeCore.SensorDataIngestion.Domain;
using SafeCore.Shared.Data;

namespace SafeCore.SensorDataIngestion.Infrastructure;

public sealed class EfSensorReadingHistory(SafeCoreDbContext context) : ISensorReadingHistory
{
    public Task<SensorReading?> GetPreviousAsync(Guid deviceId, string sensorType, DateTime capturedAtUtc, CancellationToken cancellationToken = default) =>
        context.SensorReadings.AsNoTracking()
            .Where(reading => reading.DeviceId == deviceId && reading.SensorType == sensorType && reading.CapturedAt < capturedAtUtc)
            .OrderByDescending(reading => reading.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
