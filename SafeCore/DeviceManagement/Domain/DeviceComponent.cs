using SafeCore.Shared.Kernel;

namespace SafeCore.DeviceManagement.Domain;

public sealed class DeviceComponent : Entity
{
    public Guid DeviceId { get; set; }
    public string Kind { get; set; } = "Sensor";
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Channel { get; set; }
    public string Status { get; set; } = "Disconnected";
    public DateTime? LastSeenAt { get; set; }
    public DateTime? InstalledAt { get; set; }
    public DateTime? ReplacedAt { get; set; }
}
