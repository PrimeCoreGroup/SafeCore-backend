using System.ComponentModel.DataAnnotations;

namespace SafeCore.Locations.Application;

public sealed class PropertyDto
{
    [Required, StringLength(100)] public string Name { get; init; } = string.Empty;
    [Required, StringLength(200)] public string Address { get; init; } = string.Empty;
}

public sealed record PropertyResponse(Guid Id, string Name, string Address, DateTime CreatedAt);

public sealed class ZoneDto
{
    [Required, StringLength(100)] public string Name { get; init; } = string.Empty;
}

public sealed record ZoneResponse(Guid Id, Guid PropertyId, string Name, DateTime CreatedAt);

public sealed class UserAssignmentsDto
{
    public List<UserAssignmentInput> Assignments { get; init; } = [];
}

public sealed class UserAssignmentInput
{
    [Required] public Guid PropertyId { get; init; }
    public Guid? ZoneId { get; init; }
}

public sealed record UserAssignmentResponse(Guid Id, Guid UserId, Guid PropertyId, string PropertyName, Guid? ZoneId, string? ZoneName);

public sealed class DeviceZoneDto
{
    public Guid? ZoneId { get; init; }
}
