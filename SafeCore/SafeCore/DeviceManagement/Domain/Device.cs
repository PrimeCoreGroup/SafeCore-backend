using SafeCore.Shared.Kernel;

namespace SafeCore.DeviceManagement.Domain;

public class Device : Entity
{
    public string SerialNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ConnectivityStatus { get; set; } = string.Empty;
}