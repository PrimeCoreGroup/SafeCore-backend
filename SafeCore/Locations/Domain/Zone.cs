using SafeCore.Shared.Kernel;

namespace SafeCore.Locations.Domain;

public sealed class Zone : Entity
{
    public Guid PropertyId { get; set; }
    public string Name { get; set; } = string.Empty;
}
