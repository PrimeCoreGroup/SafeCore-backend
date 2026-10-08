using System;
using SafeCore.Shared.Kernel;

namespace SafeCore.MonitoringConfiguration.Domain;

public class SystemConfiguration : Entity
{
    public int Version { get; set; }
    public string SyncStatus { get; set; } = string.Empty;
    public DateTime LastSyncedAt { get; set; }
}