using System;

namespace SafeCore.MonitoringConfiguration.Application;

public class SystemConfigurationDto
{
    public int Version { get; set; }
    public string SyncStatus { get; set; } = string.Empty;
    public DateTime LastSyncedAt { get; set; }
}