using SafeCore.Shared.Kernel;

namespace SafeCore.DeviceManagement.Domain;

public sealed class DeviceCommand : Entity
{
    public Guid DeviceId { get; set; }
    public Guid ComponentId { get; set; }
    public string CommandType { get; set; } = "Test";
    public string Status { get; set; } = "Pending";
    public string? Result { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
