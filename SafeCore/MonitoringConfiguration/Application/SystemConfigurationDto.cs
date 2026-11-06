using System;
using System.ComponentModel.DataAnnotations;

namespace SafeCore.MonitoringConfiguration.Application;

public class SystemConfigurationDto
{
    [Range(1, int.MaxValue)] public int Version { get; set; }
    [Required, RegularExpression("^(Pending|Synced|Failed)$")] public string SyncStatus { get; set; } = "Pending";
    public DateTime? LastSyncedAt { get; set; }
}
