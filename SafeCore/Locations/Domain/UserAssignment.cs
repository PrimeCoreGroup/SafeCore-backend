using SafeCore.Shared.Kernel;

namespace SafeCore.Locations.Domain;

public sealed class UserAssignment : Entity
{
    public Guid UserId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid? ZoneId { get; set; }
}
