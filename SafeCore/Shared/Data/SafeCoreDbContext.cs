using Microsoft.EntityFrameworkCore;
using SafeCore.IdentityAccess.Domain;
using SafeCore.DeviceManagement.Domain;
using SafeCore.SensorDataIngestion.Domain;
using SafeCore.RiskDetection.Domain;
using SafeCore.Emergency.Domain;
using SafeCore.Notifications.Domain;
using SafeCore.MonitoringConfiguration.Domain;

namespace SafeCore.Shared.Data;

public class SafeCoreDbContext : DbContext
{
    public SafeCoreDbContext(DbContextOptions<SafeCoreDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<SensorReading> SensorReadings { get; set; }
    public DbSet<RiskSituation> RiskSituations { get; set; }
    public DbSet<EmergencyProtocol> EmergencyProtocols { get; set; }
    public DbSet<EmergencyResponse> EmergencyResponses { get; set; }
    public DbSet<ResponseAction> ResponseActions { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<SystemConfiguration> SystemConfigurations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}