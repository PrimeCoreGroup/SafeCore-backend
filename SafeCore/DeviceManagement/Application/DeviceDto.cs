namespace SafeCore.DeviceManagement.Application;

public class DeviceDto
{
    public string SerialNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ConnectivityStatus { get; set; } = string.Empty;
}