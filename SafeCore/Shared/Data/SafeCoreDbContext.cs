using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SafeCore.IdentityAccess.Domain;
using SafeCore.DeviceManagement.Domain;
using SafeCore.SensorDataIngestion.Domain;
using SafeCore.RiskDetection.Domain;
using SafeCore.Emergency.Domain;
using SafeCore.Notifications.Domain;
using SafeCore.MonitoringConfiguration.Domain;
using SafeCore.Locations.Domain;

namespace SafeCore.Shared.Data;

public class SafeCoreDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public SafeCoreDbContext(DbContextOptions<SafeCoreDbContext> options) : base(options)
    {
    }

    public DbSet<Device> Devices { get; set; }
    public DbSet<SensorReading> SensorReadings { get; set; }
    public DbSet<RiskSituation> RiskSituations { get; set; }
    public DbSet<EmergencyProtocol> EmergencyProtocols { get; set; }
    public DbSet<EmergencyResponse> EmergencyResponses { get; set; }
    public DbSet<ResponseAction> ResponseActions { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<SystemConfiguration> SystemConfigurations { get; set; }
    public DbSet<SafetyConfiguration> SafetyConfigurations { get; set; }
    public DbSet<PropertySite> Properties { get; set; }
    public DbSet<Zone> Zones { get; set; }
    public DbSet<UserAssignment> UserAssignments { get; set; }
    public DbSet<DeviceComponent> DeviceComponents { get; set; }
    public DbSet<DeviceConnectivityEvent> DeviceConnectivityEvents { get; set; }
    public DbSet<DeviceCommand> DeviceCommands { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>().Property(user => user.FirstName).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<User>().Property(user => user.LastName).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<User>().Property(user => user.Status).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<User>().HasIndex(user => user.Email).IsUnique();

        modelBuilder.Entity<Device>().HasIndex(device => device.SerialNumber).IsUnique();
        modelBuilder.Entity<Device>().HasIndex(device => device.ZoneId);
        modelBuilder.Entity<Device>().HasOne<Zone>().WithMany().HasForeignKey(device => device.ZoneId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Device>().HasOne<User>().WithMany().HasForeignKey(device => device.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Device>().Property(device => device.SerialNumber).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<Device>().Property(device => device.Name).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<Device>().Property(device => device.Category).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<Device>().Property(device => device.ConnectivityStatus).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<Device>().Property(device => device.CredentialHash).HasMaxLength(64);
        modelBuilder.Entity<SensorReading>().Property(reading => reading.SensorType).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<SensorReading>().Property(reading => reading.Unit).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<SensorReading>().HasIndex(reading => new { reading.DeviceId, reading.SensorType, reading.CapturedAt });
        modelBuilder.Entity<SensorReading>().HasIndex(reading => new { reading.DeviceId, reading.ComponentId, reading.SensorType, reading.CapturedAt });
        modelBuilder.Entity<SensorReading>().HasIndex(reading => reading.ComponentId);
        modelBuilder.Entity<SensorReading>().HasOne<DeviceComponent>().WithMany().HasForeignKey(reading => reading.ComponentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SensorReading>().ToTable(table => table.HasCheckConstraint(
            "CK_SensorReading_ValueShape",
            "((SensorType IN ('Accelerometer', 'Gyroscope') AND MeasuredValue IS NULL AND AxisX BETWEEN -32768 AND 32767 AND AxisY BETWEEN -32768 AND 32767 AND AxisZ BETWEEN -32768 AND 32767 AND DigitalAlarm IS NULL) OR (SensorType = 'Gas' AND MeasuredValue BETWEEN 0 AND 4095 AND AxisX IS NULL AND AxisY IS NULL AND AxisZ IS NULL AND DigitalAlarm IS NOT NULL) OR (SensorType = 'Temperature' AND MeasuredValue BETWEEN -127 AND 125 AND AxisX IS NULL AND AxisY IS NULL AND AxisZ IS NULL AND DigitalAlarm IS NULL))"));
        modelBuilder.Entity<SensorReading>()
            .HasOne<Device>()
            .WithMany()
            .HasForeignKey(reading => reading.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EmergencyResponse>()
            .HasOne<EmergencyProtocol>()
            .WithMany()
            .HasForeignKey(response => response.ProtocolId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EmergencyResponse>()
            .HasOne<RiskSituation>()
            .WithMany()
            .HasForeignKey(response => response.RiskSituationId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResponseAction>()
            .HasOne<EmergencyResponse>()
            .WithMany()
            .HasForeignKey(action => action.EmergencyResponseId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Notification>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(notification => notification.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RiskSituation>().HasIndex(situation => situation.DetectedAt);
        modelBuilder.Entity<RiskSituation>().HasIndex(situation => new { situation.DeviceId, situation.RiskType, situation.Status, situation.DetectedAt });
        modelBuilder.Entity<RiskSituation>().Property(item => item.RiskType).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<RiskSituation>().Property(item => item.Origin).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<RiskSituation>().Property(item => item.Status).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<RiskSituation>().Property(item => item.Severity).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<RiskSituation>()
            .HasOne<Device>()
            .WithMany()
            .HasForeignKey(situation => situation.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RiskSituation>().HasOne<DeviceComponent>().WithMany().HasForeignKey(situation => situation.ComponentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EmergencyProtocol>().Property(item => item.Name).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<EmergencyProtocol>().Property(item => item.EmergencyType).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<EmergencyResponse>().Property(item => item.EmergencyType).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<EmergencyResponse>().Property(item => item.Status).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<ResponseAction>().Property(item => item.ActuatorType).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<ResponseAction>().Property(item => item.Instruction).HasMaxLength(1000).IsRequired();
        modelBuilder.Entity<ResponseAction>().Property(item => item.Status).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<ResponseAction>().HasOne<DeviceComponent>().WithMany().HasForeignKey(item => item.ComponentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Notification>().Property(item => item.Title).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<Notification>().Property(item => item.Body).HasMaxLength(4000).IsRequired();
        modelBuilder.Entity<Notification>().Property(item => item.Channel).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<Notification>().Property(item => item.Status).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<SystemConfiguration>().Property(item => item.SyncStatus).HasMaxLength(30).IsRequired();

        modelBuilder.Entity<SafetyConfiguration>().HasIndex(item => item.SingletonKey).IsUnique();
        modelBuilder.Entity<SafetyConfiguration>().Property(item => item.SystemName).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<SafetyConfiguration>().Property(item => item.TimeZone).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<PropertySite>().Property(item => item.Name).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<PropertySite>().Property(item => item.Address).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<PropertySite>().HasIndex(item => item.Name);
        modelBuilder.Entity<Zone>().Property(item => item.Name).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<Zone>().HasOne<PropertySite>().WithMany().HasForeignKey(item => item.PropertyId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Zone>().HasIndex(item => new { item.PropertyId, item.Name }).IsUnique();
        modelBuilder.Entity<UserAssignment>().HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserAssignment>().HasOne<PropertySite>().WithMany().HasForeignKey(item => item.PropertyId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserAssignment>().HasOne<Zone>().WithMany().HasForeignKey(item => item.ZoneId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserAssignment>().HasIndex(item => new { item.UserId, item.PropertyId, item.ZoneId }).IsUnique();
        modelBuilder.Entity<DeviceComponent>().Property(item => item.Kind).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<DeviceComponent>().Property(item => item.Type).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<DeviceComponent>().Property(item => item.Label).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<DeviceComponent>().Property(item => item.Channel).HasMaxLength(100);
        modelBuilder.Entity<DeviceComponent>().Property(item => item.Status).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<DeviceComponent>().HasOne<Device>().WithMany().HasForeignKey(item => item.DeviceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DeviceComponent>().HasIndex(item => new { item.DeviceId, item.Kind, item.Type, item.Channel }).IsUnique();
        modelBuilder.Entity<DeviceConnectivityEvent>().Property(item => item.ConnectivityStatus).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<DeviceConnectivityEvent>().HasOne<Device>().WithMany().HasForeignKey(item => item.DeviceId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DeviceConnectivityEvent>().HasIndex(item => new { item.DeviceId, item.OccurredAt });
        modelBuilder.Entity<DeviceCommand>().Property(item => item.CommandType).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<DeviceCommand>().Property(item => item.Status).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<DeviceCommand>().Property(item => item.Result).HasMaxLength(500);
        modelBuilder.Entity<DeviceCommand>().HasOne<Device>().WithMany().HasForeignKey(item => item.DeviceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DeviceCommand>().HasOne<DeviceComponent>().WithMany().HasForeignKey(item => item.ComponentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DeviceCommand>().HasIndex(item => new { item.DeviceId, item.Status, item.RequestedAt });
    }
}
