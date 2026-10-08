using SafeCore.Shared.Kernel;

namespace SafeCore.Emergency.Domain;

public class EmergencyProtocol : Entity
{
    public string Name { get; set; } = string.Empty;
    public string EmergencyType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}