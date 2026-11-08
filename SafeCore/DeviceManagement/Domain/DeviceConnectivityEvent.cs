using SafeCore.Shared.Kernel;

namespace SafeCore.DeviceManagement.Domain;

public sealed class DeviceConnectivityEvent : Entity
{
    public Guid DeviceId { get; set; }
    public string ConnectivityStatus { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
