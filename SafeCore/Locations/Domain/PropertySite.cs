using SafeCore.Shared.Kernel;

namespace SafeCore.Locations.Domain;

public sealed class PropertySite : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}
