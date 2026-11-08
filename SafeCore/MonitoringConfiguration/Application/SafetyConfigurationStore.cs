using Microsoft.EntityFrameworkCore;
using SafeCore.MonitoringConfiguration.Domain;
using SafeCore.Shared.Data;
using Microsoft.Extensions.Options;
using SafeCore.RiskDetection.Application;

namespace SafeCore.MonitoringConfiguration.Application;

public sealed class SafetyConfigurationStore(SafeCoreDbContext db, IOptions<SafetyThresholds> defaultThresholds)
{
    public async Task<SafetyConfiguration> GetAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await db.SafetyConfigurations.SingleOrDefaultAsync(item => item.SingletonKey == 1, cancellationToken);
        if (configuration is not null) return configuration;
        configuration = new SafetyConfiguration
        {
            TemperatureCelsius = defaultThresholds.Value.TemperatureCelsius,
            GasAdcRiseCounts = defaultThresholds.Value.GasAdcRiseCounts,
            AccelerationMagnitudeDeltaCounts = defaultThresholds.Value.AccelerationMagnitudeDeltaCounts,
            ImpactAlertMilliseconds = defaultThresholds.Value.ImpactAlertMilliseconds
        };
        db.SafetyConfigurations.Add(configuration);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return configuration;
        }
        catch (DbUpdateException)
        {
            db.Entry(configuration).State = EntityState.Detached;
            var concurrentInsert = await db.SafetyConfigurations.SingleOrDefaultAsync(item => item.SingletonKey == 1, cancellationToken);
            if (concurrentInsert is not null) return concurrentInsert;
            throw;
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
