using System.ComponentModel.DataAnnotations;

namespace SafeCore.DeviceManagement.Application;

public sealed class DeviceComponentDto
{
    [Required, RegularExpression("^(Sensor|Actuator)$")] public string Kind { get; init; } = "Sensor";
    [Required, StringLength(50)] public string Type { get; init; } = string.Empty;
    [Required, StringLength(100)] public string Label { get; init; } = string.Empty;
    [StringLength(100)] public string? Channel { get; init; }
    [Required, RegularExpression("^(Ok|Fault|Disconnected|Maintenance|Disabled)$")] public string Status { get; init; } = "Disconnected";
    public DateTime? InstalledAt { get; init; }
    public DateTime? ReplacedAt { get; init; }
}

public sealed class DeviceManifestDto
{
    [Required] public List<DeviceManifestComponentDto> Components { get; init; } = [];
}

public sealed class DeviceManifestComponentDto
{
    [Required, RegularExpression("^(Sensor|Actuator)$")] public string Kind { get; init; } = "Sensor";
    [Required, StringLength(50)] public string Type { get; init; } = string.Empty;
    [Required, StringLength(100)] public string Label { get; init; } = string.Empty;
    [StringLength(100)] public string? Channel { get; init; }
    public DateTime? InstalledAt { get; init; }
}

public sealed class DeviceHealthDto
{
    [Required] public List<ComponentHealthInput> Components { get; init; } = [];
}

public sealed class ComponentHealthInput
{
    [Required] public Guid ComponentId { get; init; }
    [Required, RegularExpression("^(Ok|Fault|Disconnected|Maintenance|Disabled)$")] public string Status { get; init; } = string.Empty;
}

public sealed class DeviceCommandResultDto
{
    [Required, RegularExpression("^(Completed|Failed)$")] public string Status { get; init; } = string.Empty;
    [StringLength(500)] public string? Result { get; init; }
}

public sealed record DeviceComponentResponse(Guid Id, Guid DeviceId, string Kind, string Type, string Label, string? Channel, string Status, DateTime? LastSeenAt, DateTime? InstalledAt, DateTime? ReplacedAt);
public sealed record ComponentTestResponse(Guid CommandId, Guid ComponentId, string Status, string Message, DateTime RequestedAt);
