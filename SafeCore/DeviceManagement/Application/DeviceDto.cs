using System.ComponentModel.DataAnnotations;

namespace SafeCore.DeviceManagement.Application;

public class DeviceDto
{
    [Required, StringLength(100)] public string SerialNumber { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Category { get; set; } = string.Empty;
    [Required, RegularExpression("^(Online|Offline|Degraded|Maintenance)$")] public string ConnectivityStatus { get; set; } = "Offline";
}

public sealed record DeviceResponse(Guid Id, string SerialNumber, string Name, string Category, string ConnectivityStatus, DateTime CreatedAt,
    Guid? ZoneId, string? ZoneName, Guid? PropertyId, string? PropertyName, Guid? CreatedByUserId, string? CreatedByName, DateTime? LastSeenAt);
public sealed record ProvisionedDeviceResponse(DeviceResponse Device, string DeviceSecret);
